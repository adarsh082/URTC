using LibGit2Sharp;
using System;
using UnityEngine;
using System.Linq;
using System.IO;

namespace URTC.Editor
{
    public class GitHelper
    {
        public string RepositoryPath { get; private set; }
        public Signature Author { get; private set; }
        
        public GitHelper(string authorName, string authorEmail)
        {
            Author = new Signature(authorName, authorEmail, DateTime.Now);
        }
        
        public bool InitializeRepository(string path)
        {
            try
            {
                // Repository.Discover traverses parent directories to find a .git folder.
                string discovered = null;
                try { discovered = Repository.Discover(path); } catch { }

                if (!string.IsNullOrEmpty(discovered))
                {
                    using (var tempRepo = new Repository(discovered))
                    {
                        // Normalize both paths for comparison
                        string repoRoot = tempRepo.Info.WorkingDirectory
                            .Replace("\\", "/").TrimEnd('/');
                        string projectRoot = path
                            .Replace("\\", "/").TrimEnd('/');

                        if (string.Equals(repoRoot, projectRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            // The git root IS our project folder — open it directly.
                            RepositoryPath = tempRepo.Info.WorkingDirectory;
                            Debug.Log($"[GitHelper] Opened existing repository at: {RepositoryPath}");
                        }
                        else
                        {
                            // The git root is a PARENT of our project folder
                            // (e.g. urtc-server/ when project is urtc-server/frontend/).
                            // Init a new repo specifically at our project path so
                            // pulled files land in the right place.
                            RepositoryPath = Repository.Init(path);
                            Debug.Log($"[GitHelper] Initialized new repository at project path: {RepositoryPath}");
                        }
                    }
                }
                else
                {
                    // No existing git repo found anywhere — init fresh.
                    RepositoryPath = Repository.Init(path);
                    Debug.Log($"[GitHelper] Initialized new repository at: {RepositoryPath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to initialize repository: {ex.Message}");
                return false;
            }
        }
        
        public bool StageAllFiles()
        {
            try
            {
                using (var repo = new Repository(RepositoryPath))
                {
                    var statusOptions = new StatusOptions 
                    { 
                        IncludeUntracked = true,
                        RecurseUntrackedDirs = true,
                        IncludeIgnored = false,
                        Show = StatusShowOption.IndexAndWorkDir
                    };

                    var status = repo.RetrieveStatus(statusOptions);
                    int count = 0;
                    
                    foreach (var entry in status)
                    {
                        string fullPath = Path.Combine(repo.Info.WorkingDirectory, entry.FilePath);
                        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                        {
                            repo.Index.Remove(entry.FilePath);
                            count++;
                        }
                        else if (entry.State != FileStatus.Ignored)
                        {
                            repo.Index.Add(entry.FilePath);
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        repo.Index.Write();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to stage files: {ex.Message}");
                return false;
            }
        }
        
        public bool CommitChanges(string message)
        {
            try
            {
                using (var repo = new Repository(RepositoryPath))
                {
                    try 
                    {
                        repo.Commit(message, Author, Author);
                        return true;
                    }
                    catch (Exception commitEx)
                    {
                        if (commitEx.Message.Contains("nothing to commit") || commitEx.Message.Contains("no changes"))
                        {
                            return true;
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to commit: {ex.Message}");
                return false;
            }
        }
        
        public bool CreateOrSwitchToMainBranch()
        {
            try
            {
                using (var repo = new Repository(RepositoryPath))
                {
                    if (repo.Head.Tip == null)
                    {
                        Debug.LogWarning("[GitHelper] Cannot create branch: No commits in repository yet.");
                        return false;
                    }

                    var mainBranch = repo.Branches["main"] ?? repo.CreateBranch("main", repo.Head.Tip);
                    try
                    {
                        Commands.Checkout(repo, "main");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GitHelper] Checkout to main failed. Your local files were left unchanged: {ex.Message}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to create/switch to main branch: {ex.Message}");
                return false;
            }
        }
        
        public bool AddRemote(string remoteName, string remoteUrl)
        {
            try
            {
                using (var repo = new Repository(RepositoryPath))
                {
                    var existingRemote = repo.Network.Remotes[remoteName];
                    if (existingRemote != null)
                    {
                        if (existingRemote.Url != remoteUrl)
                        {
                            repo.Network.Remotes.Update(remoteName, r => r.Url = remoteUrl);
                        }
                        return true;
                    }
                    repo.Network.Remotes.Add(remoteName, remoteUrl);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to add remote: {ex.Message}");
                return false;
            }
        }
        
        public bool PushToRemote(string remoteName, string branchName, string username, string password)
        {
            try
            {
                using (var repo = new Repository(RepositoryPath))
                {
                    var branch = repo.Branches[branchName];
                    var remote = repo.Network.Remotes[remoteName];
                    
                    if (branch == null || remote == null) return false;
                    
                    var pushOptions = new PushOptions
                    {
                        CredentialsProvider = (url, user, cred) =>
                            new UsernamePasswordCredentials { Username = username, Password = password }
                    };
                    
                    string refSpec = $"{branch.CanonicalName}:{branch.CanonicalName}";
                    repo.Network.Push(remote, refSpec, pushOptions);
                    
                    repo.Branches.Update(branch, b => {
                        b.Remote = remote.Name;
                        b.UpstreamBranch = branch.CanonicalName;
                    });
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to push: {ex.Message}");
                return false;
            }
        }

        public bool PullFromRemote(string remoteName, string branchName, string username, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(RepositoryPath))
                {
                    Debug.LogError("[GitHelper] RepositoryPath is null or empty.");
                    return false;
                }

                Debug.Log($"[GitHelper] Starting Pull from {remoteName}/{branchName}");

                using (var repo = new Repository(RepositoryPath))
                {
                    var fetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (url, user, cred) =>
                            new UsernamePasswordCredentials { Username = username, Password = password }
                    };

                    // Step 1 — Fetch latest from remote
                    Debug.Log($"[GitHelper] Fetching from {remoteName}...");
                    string refSpec = $"+refs/heads/{branchName}:refs/remotes/{remoteName}/{branchName}";
                    repo.Network.Fetch(remoteName, new[] { refSpec }, fetchOptions, null);

                    // Step 2 — Get the remote tracking branch
                    var remoteBranch = repo.Branches[$"{remoteName}/{branchName}"];
                    if (remoteBranch == null)
                    {
                        Debug.LogError($"[GitHelper] Remote branch {remoteName}/{branchName} not found after fetch.");
                        return false;
                    }

                    var signature = new Signature(Author.Name, Author.Email, DateTime.Now);

                    // Step 3 — If local branch doesn't exist yet, create and checkout it
                    var localBranch = repo.Branches[branchName];
                    if (localBranch == null)
                    {
                        Debug.Log($"[GitHelper] Creating local branch {branchName} from remote...");
                        localBranch = repo.CreateBranch(branchName, remoteBranch.Tip);
                        repo.Branches.Update(localBranch, b =>
                        {
                            b.Remote = remoteName;
                            b.UpstreamBranch = $"refs/heads/{branchName}";
                        });
                    }

                    // Step 4 — Checkout local branch (force to overwrite conflicting local files)
                    if (repo.Head.FriendlyName != branchName)
                    {
                        Debug.Log($"[GitHelper] Checking out branch {branchName} (force)...");
                        try
                        {
                            var checkoutOptions = new CheckoutOptions
                            {
                                CheckoutModifiers = CheckoutModifiers.Force
                            };
                            Commands.Checkout(repo, repo.Branches[branchName], checkoutOptions);
                        }
                        catch (Exception checkoutEx)
                        {
                            // "Access is denied" = a DLL locked by Unity (git2-*.dll) — safe to ignore.
                            if (checkoutEx.Message.Contains("Access is denied") ||
                                checkoutEx.Message.Contains("access is denied"))
                            {
                                Debug.LogWarning($"[GitHelper] Skipped locked file during checkout (DLL in use): {checkoutEx.Message}");
                            }
                            else
                            {
                                Debug.LogError($"[GitHelper] Checkout failed: {checkoutEx.Message}");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[GitHelper] Already on branch {branchName}, skipping checkout.");
                    }

                    // Step 5 — Auto-commit any local uncommitted changes so merge isn't blocked.
                    // Unity modifies tracked files (e.g. ProjectSettings) when opening a project.
                    try
                    {
                        var repoStatus = repo.RetrieveStatus(new StatusOptions
                        {
                            IncludeUntracked = false,
                            Show = StatusShowOption.IndexAndWorkDir
                        });
                        if (repoStatus.IsDirty)
                        {
                            Debug.Log("[GitHelper] Auto-committing local changes before merge...");
                            Commands.Stage(repo, "*");
                            repo.Commit("Auto-commit: local changes before pull", signature, signature,
                                new CommitOptions { AllowEmptyCommit = false });
                            Debug.Log("[GitHelper] Auto-commit done.");
                        }
                    }
                    catch (Exception autoEx)
                    {
                        Debug.LogWarning($"[GitHelper] Auto-commit skipped: {autoEx.Message}");
                    }

                    // Step 6 — Merge remote branch into local (explicit fetch+merge,
                    // no tracking info required unlike Commands.Pull).
                    Debug.Log("[GitHelper] Merging remote changes...");
                    var mergeOptions = new MergeOptions
                    {
                        // Always take remote (owner's) version on conflict.
                        FileConflictStrategy = CheckoutFileConflictStrategy.Theirs,
                        MergeFileFavor = MergeFileFavor.Theirs
                    };

                    try
                    {
                        var mergeResult = repo.Merge(remoteBranch, signature, mergeOptions);
                        if (mergeResult.Status == MergeStatus.Conflicts)
                        {
                            // Shouldn't happen with Theirs strategy, but handle just in case.
                            Debug.LogWarning("[GitHelper] Merge had conflicts — remote version was kept.");
                        }
                        Debug.Log($"[GitHelper] Pull completed. Status: {mergeResult.Status}");
                    }
                    catch (Exception mergeEx)
                    {
                        Debug.LogError($"[GitHelper] Merge failed: {mergeEx.Message}");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to pull: {ex.Message}");
                if (ex.InnerException != null)
                    Debug.LogError($"[GitHelper] Inner exception: {ex.InnerException.Message}");
                return false;
            }
        }


        public bool ExecuteFullGitWorkflow(string lp, string msg, string url, string user, string pass)
        {
            if (!InitializeRepository(lp)) return false;
            if (!StageAllFiles()) return false;
            if (!CommitChanges(msg)) return false;
            if (!CreateOrSwitchToMainBranch()) return false;
            if (!AddRemote("origin", url)) return false;
            return PushToRemote("origin", "main", user, pass);
        }
    }
}
