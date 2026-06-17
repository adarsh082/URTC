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
                Debug.Log($"[GitHelper] Initializing/Opening repository at: {path}");
                RepositoryPath = Repository.Init(path);
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
                        Commands.Checkout(repo, "main", new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GitHelper] Checkout to main failed ({ex.Message}). Applying Smart Sync...");
                        repo.Refs.UpdateTarget(repo.Refs.Head, mainBranch.CanonicalName);
                        repo.Reset(ResetMode.Mixed, mainBranch.Tip);
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
                    var pullOptions = new PullOptions
                    {
                        FetchOptions = new FetchOptions
                        {
                            CredentialsProvider = (url, user, cred) =>
                                new UsernamePasswordCredentials { Username = username, Password = password }
                        },
                        MergeOptions = new MergeOptions
                        {
                            FileConflictStrategy = CheckoutFileConflictStrategy.Normal
                        }
                    };

                    var localBranch = repo.Branches[branchName];
                    if (localBranch == null)
                    {
                        Debug.Log($"[GitHelper] Local branch {branchName} not found. Fetching from remote...");
                        repo.Network.Fetch(remoteName, new string[] { branchName }, pullOptions.FetchOptions, null);
                        
                        var remoteBranch = repo.Branches[$"{remoteName}/{branchName}"];
                        if (remoteBranch != null)
                        {
                            Debug.Log($"[GitHelper] Creating local branch {branchName} from {remoteBranch.CanonicalName}");
                            localBranch = repo.CreateBranch(branchName, remoteBranch.Tip);
                            repo.Branches.Update(localBranch, b => {
                                b.Remote = remoteName;
                                b.UpstreamBranch = $"refs/heads/{branchName}";
                            });
                        }
                        else 
                        {
                            Debug.LogError($"[GitHelper] Remote branch {remoteName}/{branchName} not found after fetch.");
                            return false;
                        }
                    }

                    if (repo.Head.FriendlyName != branchName)
                    {
                        Debug.Log($"[GitHelper] Checking out branch {branchName}");
                        try
                        {
                            Commands.Checkout(repo, branchName, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
                        }
                        catch (Exception checkoutEx)
                        {
                            Debug.LogWarning($"[GitHelper] Checkout failed ({checkoutEx.Message}). Attempting Smart Sync...");
                            repo.Refs.UpdateTarget(repo.Refs.Head, repo.Branches[branchName].CanonicalName);
                            repo.Reset(ResetMode.Mixed, repo.Branches[branchName].Tip);
                        }
                    }

                    Debug.Log("[GitHelper] Executing Pull/Merge...");
                    var signature = new Signature(Author.Name, Author.Email, DateTime.Now);
                    Commands.Pull(repo, signature, pullOptions);
                    Debug.Log("[GitHelper] Pull completed successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to pull: {ex.Message}");
                if (ex.InnerException != null) Debug.LogError($"[GitHelper] Inner Exception: {ex.InnerException.Message}");
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
