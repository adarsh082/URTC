using LibGit2Sharp;
using System;
using UnityEngine;
using System.Linq;

namespace URTC.Editor
{
    /// <summary>
    /// GitHelper - Professional Git operations manager using LibGit2Sharp
    /// Provides comprehensive version control functionality for Unity projects
    /// </summary>
    public class GitHelper
    {
        #region Properties
        
        /// <summary>
        /// Path to the repository
        /// </summary>
        public string RepositoryPath { get; private set; }
        
        /// <summary>
        /// Author information for commits
        /// </summary>
        public Signature Author { get; private set; }
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initialize GitHelper with author information
        /// </summary>
        /// <param name="authorName">Name of the commit author</param>
        /// <param name="authorEmail">Email of the commit author</param>
        public GitHelper(string authorName, string authorEmail)
        {
            Author = new Signature(authorName, authorEmail, DateTime.Now);
        }
        
        #endregion
        
        #region Core Git Operations
        
        /// <summary>
        /// 1. Initialize a new Git repository
        /// Equivalent to: git init
        /// </summary>
        /// <param name="path">Path where repository should be initialized</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool InitializeRepository(string path)
        {
            try
            {
                Debug.Log($"[GitHelper] Initializing/Opening repository at: {path}");
                RepositoryPath = Repository.Init(path);
                Debug.Log($"[GitHelper] Final Repository Path: {RepositoryPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to initialize repository: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 2. Stage all files for commit
        /// Equivalent to: git add .
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool StageAllFiles()
        {
            try
            {
                using var repo = new Repository(RepositoryPath);
                Debug.Log($"[GitHelper] Scanning for changes in: {repo.Info.WorkingDirectory}");
                
                // Retrieve the status with explicit options to include everything untracked
                var statusOptions = new StatusOptions 
                { 
                    IncludeUntracked = true,
                    RecurseUntrackedDirs = true,
                    IncludeIgnored = false,
                    Show = StatusShowOption.IndexAndWorkDir
                };

                var status = repo.RetrieveStatus(statusOptions);
                int count = 0;
                
                Debug.Log($"[GitHelper] Total status entries found: {status.Count()}");

                foreach (var entry in status)
                {
                    if (entry.State != FileStatus.Ignored)
                    {
                        Debug.Log($"[GitHelper] Staging: {entry.FilePath} (State: {entry.State})");
                        repo.Index.Add(entry.FilePath);
                        count++;
                    }
                    else
                    {
                        Debug.Log($"[GitHelper] Ignoring (per .gitignore): {entry.FilePath}");
                    }
                }

                if (count > 0)
                {
                    repo.Index.Write();
                    Debug.Log($"[GitHelper] Staging successful. {count} files added to the index.");
                }
                else
                {
                    Debug.Log("[GitHelper] No new or modified files found to stage. Check if your files are in .gitignore.");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to stage files: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 3. Commit staged changes
        /// Equivalent to: git commit -m "message"
        /// </summary>
        /// <param name="message">Commit message</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool CommitChanges(string message)
        {
            try
            {
                using var repo = new Repository(RepositoryPath);
                
                // Directly check the index count as a more reliable indicator
                int stagedCount = repo.Index.Count;
                Debug.Log($"[GitHelper] Attempting commit. Index contains {stagedCount} entries.");

                // Even if stagedCount is high, we only commit if there's actually a change from the last commit
                // LibGit2Sharp's repo.Commit handles the "nothing to commit" case with an exception
                try 
                {
                    var commit = repo.Commit(message, Author, Author);
                    Debug.Log($"[GitHelper] SUCCESS: Commit created {commit.Sha.Substring(0, 7)}");
                    return true;
                }
                catch (Exception commitEx)
                {
                    if (commitEx.Message.Contains("nothing to commit") || commitEx.Message.Contains("no changes"))
                    {
                        Debug.Log("[GitHelper] No changes detected to commit. Everything is already up to date.");
                        return true;
                    }
                    throw; // Re-throw other errors to be caught by the outer catch
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to commit: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 4. Create or switch to 'main' branch
        /// Equivalent to: git branch -M main
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool CreateOrSwitchToMainBranch()
        {
            try
            {
                using var repo = new Repository(RepositoryPath);
                
                // Ensure we have at least one commit before creating a branch
                if (repo.Head.Tip == null)
                {
                    Debug.LogWarning("[GitHelper] Cannot create branch: No commits in repository yet.");
                    return false;
                }

                // Get or create 'main' branch
                var mainBranch = repo.Branches["main"] ?? repo.CreateBranch("main", repo.Head.Tip);
                
                // Checkout main branch
                Commands.Checkout(repo, "main", new CheckoutOptions { CheckoutStrategy = CheckoutStrategy.Force });
                
                Debug.Log("[GitHelper] Switched to 'main' branch successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to create/switch to main branch: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 5. Add a remote repository
        /// Equivalent to: git remote add origin <url>
        /// </summary>
        /// <param name="remoteName">Name of the remote (typically 'origin')</param>
        /// <param name="remoteUrl">URL of the remote repository</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool AddRemote(string remoteName, string remoteUrl)
        {
            try
            {
                using var repo = new Repository(RepositoryPath);
                
                // Check if remote already exists
                var existingRemote = repo.Network.Remotes[remoteName];
                if (existingRemote != null)
                {
                    if (existingRemote.Url == remoteUrl)
                    {
                        Debug.Log($"[GitHelper] Remote '{remoteName}' already exists with correct URL.");
                    }
                    else
                    {
                        Debug.LogWarning($"[GitHelper] Remote '{remoteName}' already exists but with DIFFERENT URL: {existingRemote.Url}. Updating to: {remoteUrl}");
                        repo.Network.Remotes.Update(remoteName, r => r.Url = remoteUrl);
                    }
                    return true;
                }
                
                // Add new remote
                repo.Network.Remotes.Add(remoteName, remoteUrl);
                Debug.Log($"[GitHelper] Remote '{remoteName}' added: {remoteUrl}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to add remote: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 6. Push branch to remote with tracking
        /// Equivalent to: git push -u origin main
        /// </summary>
        /// <param name="remoteName">Name of the remote (typically 'origin')</param>
        /// <param name="branchName">Name of the branch to push (typically 'main')</param>
        /// <param name="username">Git username or token</param>
        /// <param name="password">Git password or personal access token</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool PushToRemote(string remoteName, string branchName, string username, string password)
        {
            try
            {
                using var repo = new Repository(RepositoryPath);
                
                // Get the branch
                var branch = repo.Branches[branchName];
                if (branch == null)
                {
                    Debug.LogError($"[GitHelper] Branch '{branchName}' not found locally.");
                    return false;
                }
                
                // Get the remote
                var remote = repo.Network.Remotes[remoteName];
                if (remote == null)
                {
                    Debug.LogError($"[GitHelper] Remote '{remoteName}' not found.");
                    return false;
                }
                
                // Setup push options with credentials
                var pushOptions = new PushOptions
                {
                    CredentialsProvider = (url, user, cred) =>
                        new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = password
                        }
                };
                
                // Push the branch using a RefSpec to ensure it works even without upstream tracking
                // Equivalent to: git push origin main
                string refSpec = $"{branch.CanonicalName}:{branch.CanonicalName}";
                repo.Network.Push(remote, refSpec, pushOptions);
                
                // Now that it's pushed, set the upstream tracking
                // Equivalent to: git push -u origin main
                repo.Branches.Update(branch, b =>
                {
                    b.Remote = remote.Name;
                    b.UpstreamBranch = branch.CanonicalName;
                });
                
                Debug.Log($"[GitHelper] Successfully pushed '{branchName}' to '{remoteName}' and established upstream tracking.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to push: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 7. Pull changes from remote
        /// Equivalent to: git pull origin main
        /// </summary>
        /// <param name="remoteName">Name of the remote (typically 'origin')</param>
        /// <param name="branchName">Name of the branch (typically 'main')</param>
        /// <param name="username">Git username or token</param>
        /// <param name="password">Git password or personal access token</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool PullFromRemote(string remoteName, string branchName, string username, string password)
        {
            try
            {
                if (string.IsNullOrEmpty(RepositoryPath))
                {
                    Debug.LogError("[GitHelper] Repository path is not set. Cannot pull. Make sure to initialize the repository first.");
                    return false;
                }

                using var repo = new Repository(RepositoryPath);
                
                // Setup pull options with credentials and force checkout strategy
                var pullOptions = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (url, user, cred) =>
                            new UsernamePasswordCredentials
                            {
                                Username = username,
                                Password = password
                            }
                    },
                    MergeOptions = new MergeOptions
                    {
                        CheckoutOptions = new CheckoutOptions
                        {
                            CheckoutStrategy = CheckoutStrategy.Force
                        }
                    }
                };

                // Check if the branch exists locally
                var localBranch = repo.Branches[branchName];
                if (localBranch == null)
                {
                    Debug.Log($"[GitHelper] Local branch '{branchName}' not found. Fetching from remote...");
                    // Fetch to get remote branch info
                    repo.Network.Fetch(remoteName, new string[] { branchName }, pullOptions.FetchOptions, null);
                    
                    var remoteBranch = repo.Branches[$"{remoteName}/{branchName}"];
                    if (remoteBranch != null)
                    {
                        Debug.Log($"[GitHelper] Creating local branch '{branchName}' from '{remoteName}/{branchName}'");
                        localBranch = repo.CreateBranch(branchName, remoteBranch.Tip);
                        
                        // Establish upstream tracking
                        repo.Branches.Update(localBranch, b =>
                        {
                            b.Remote = remoteName;
                            b.UpstreamBranch = $"refs/heads/{branchName}";
                        });
                    }
                    else
                    {
                        Debug.LogError($"[GitHelper] Remote branch '{remoteName}/{branchName}' not found.");
                        return false;
                    }
                }

                // Ensure tracking is established even if branch exists
                if (localBranch.RemoteName != remoteName)
                {
                     repo.Branches.Update(localBranch, b =>
                     {
                         b.Remote = remoteName;
                         b.UpstreamBranch = $"refs/heads/{branchName}";
                     });
                }

                // Ensure we are on the branch we want to pull
                if (repo.Head.FriendlyName != branchName)
                {
                    Debug.Log($"[GitHelper] Switching to branch '{branchName}' before pulling.");
                    Commands.Checkout(repo, branchName, new CheckoutOptions { CheckoutStrategy = CheckoutStrategy.Force });
                }

                // Pull the changes
                var signature = new Signature(Author.Name, Author.Email, DateTime.Now);
                try
                {
                    Commands.Pull(repo, signature, pullOptions);
                }
                catch (Exception pullEx) when (pullEx.Message.Contains("conflicts prevent checkout"))
                {
                    Debug.LogWarning($"[GitHelper] Standard pull failed due to conflicts. Attempting Hard Reset to sync with remote...");
                    
                    // Get the remote branch again to ensure we have the latest Tip after fetch (which Pull does)
                    var remoteBranch = repo.Branches[$"{remoteName}/{branchName}"];
                    if (remoteBranch != null)
                    {
                        repo.Reset(ResetMode.Hard, remoteBranch.Tip);
                        Debug.Log($"[GitHelper] Successfully synchronized via Hard Reset to {remoteName}/{branchName}");
                    }
                    else
                    {
                        throw new Exception($"Could not find remote branch {remoteName}/{branchName} for hard reset fallback.");
                    }
                }
                
                Debug.Log($"[GitHelper] Successfully pulled from '{remoteName}'");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHelper] Failed to pull: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region High-Level Workflow Methods
        
        /// <summary>
        /// Complete Git workflow: Init → Add → Commit → Branch → Remote → Push
        /// This executes all Git operations in sequence
        /// </summary>
        /// <param name="localPath">Local repository path</param>
        /// <param name="commitMessage">First commit message</param>
        /// <param name="remoteUrl">Remote repository URL</param>
        /// <param name="username">Git username or token</param>
        /// <param name="password">Git password or personal access token</param>
        /// <returns>True if all operations successful, false otherwise</returns>
        public bool ExecuteFullGitWorkflow(
            string localPath,
            string commitMessage,
            string remoteUrl,
            string username,
            string password)
        {
            Debug.Log("[GitHelper] ===== Starting Full Git Workflow =====");
            
            // 1. Initialize repository
            if (!InitializeRepository(localPath))
                return false;
            
            // 2. Stage all files
            if (!StageAllFiles())
                return false;
            
            // 3. Commit changes
            if (!CommitChanges(commitMessage))
                return false;
            
            // 4. Create and switch to main branch
            if (!CreateOrSwitchToMainBranch())
                return false;
            
            // 5. Add remote origin
            if (!AddRemote("origin", remoteUrl))
                return false;
            
            // 6. Push to remote with tracking
            if (!PushToRemote("origin", "main", username, password))
                return false;
            
            Debug.Log("[GitHelper] ===== Full Git Workflow Completed Successfully =====");
            return true;
        }
        
        /// <summary>
        /// Quick commit and push workflow for existing repositories
        /// </summary>
        /// <param name="commitMessage">Commit message</param>
        /// <param name="username">Git username or token</param>
        /// <param name="password">Git password or personal access token</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool QuickCommitAndPush(string commitMessage, string username, string password)
        {
            Debug.Log("[GitHelper] ===== Quick Commit & Push =====");
            
            if (!StageAllFiles())
                return false;
            
            if (!CommitChanges(commitMessage))
                return false;
            
            if (!PushToRemote("origin", "main", username, password))
                return false;
            
            Debug.Log("[GitHelper] ===== Commit & Push Completed =====");
            return true;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Get current repository status
        /// </summary>
        /// <returns>Repository status information</returns>
        public string GetRepositoryStatus()
        {
            try
            {
                using var repo = new Repository(RepositoryPath);
                var status = repo.RetrieveStatus();
                
                return $"Modified: {status.Modified.Count()}, " +
                       $"Added: {status.Added.Count()}, " +
                       $"Removed: {status.Removed.Count()}, " +
                       $"Untracked: {status.Untracked.Count()}";
            }
            catch (Exception ex)
            {
                return $"Error retrieving status: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Check if path is a valid Git repository
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if valid repository, false otherwise</returns>
        public static bool IsValidRepository(string path)
        {
            return Repository.IsValid(path);
        }
        
        #endregion
    }
}

