using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections.Generic;

namespace URTC.Editor
{
    #region Data Classes

    [System.Serializable]
    public class CollaborationRequest
    {
        public string project_name;
        public string user_email;
        public string project_description;
        public string token; // used only for collaborators
    }

    [System.Serializable]
    public class CollaborationResponse
    {
        public bool success;
        public string message;
        public string project_id;
        public string collab_id;
        public string repo_url;
        public string token;
        public string github_token;
        public string user_id;
        public string username;
    }

    [System.Serializable]
    public class AddCollaboratorRequest
    {
        public string owner_email;
        public string collaborator_email;
        public string project_id;
    }

    #endregion

    public class URTC_Panel : EditorWindow
    {
        private enum PanelMode { Owner, Collaborator }
        private PanelMode currentMode = PanelMode.Owner;

        // Common fields
        private string serverURL = URTC_ServerConfiguration.DefaultApiBaseUrl;
        private string userEmail = "";
        private string sessionID = "";
        private string userID = "";
        private string githubUsername = "";
        private bool isLoading = false;
        private string statusMessage = "";

        // Owner fields
        private string projectName = "";
        private string projectDescription = "";
        private string projectPath = "";
        private string token = "";
        private string currentProjectID = "";
        private string currentRepoURL = "";
        private string collaboratorEmail = "";

        // Collaborator fields
        private string joinToken = "";
        private string collabUserEmail = ""; // collaborator's own email — used in Collaborator tab
        private string githubToken = "";
        private GitHelper gitHelper;

        [MenuItem("Window/URTC Panel")]
        public static void ShowWindow()
        {
            URTC_Panel window = GetWindow<URTC_Panel>();
            window.titleContent = new GUIContent("URTC Collaboration");
            window.Show();
        }

        private string GetPrefKey(string key)
        {
            // Use a stable custom hash of the path instead of GetHashCode() which can change between sessions
            string path = Application.dataPath.Replace("\\", "/").ToLower();
            long hash = 0;
            for (int i = 0; i < path.Length; i++) {
                hash = 31 * hash + path[i];
            }
            return $"URTC_{key}_{hash}";
        }

        private void OnLostFocus()
        {
            // Force save whenever the window loses focus (e.g. clicking a file in Project window)
            SavePrefs();
        }

        private void OnEnable()
        {
            projectName = Application.productName;
            projectPath = Path.GetDirectoryName(Application.dataPath);

            // Load persisted values safely
            userEmail = EditorPrefs.GetString(GetPrefKey("URTC_Email"), "");
            sessionID = EditorPrefs.GetString(GetPrefKey("URTC_SessionID"), "");
            userID = EditorPrefs.GetString(GetPrefKey("URTC_UserID"), "");
            githubUsername = EditorPrefs.GetString(GetPrefKey("URTC_GitHubUsername"), "");
            currentProjectID = EditorPrefs.GetString(GetPrefKey("URTC_ProjectID"), "");
            currentRepoURL = EditorPrefs.GetString(GetPrefKey("URTC_RepoURL"), "");
            token = EditorPrefs.GetString(GetPrefKey("URTC_JoinToken"), "");
            collabUserEmail = EditorPrefs.GetString(GetPrefKey("URTC_CollabEmail"), "");
            // GitHub credentials are intentionally memory-only. Re-authenticate after restarting Unity.
            EditorPrefs.DeleteKey(GetPrefKey("URTC_GitHubToken"));
            serverURL = URTC_ServerConfiguration.LoadApiBaseUrl();

            if (!string.IsNullOrEmpty(userEmail))
            {
                string authorName = !string.IsNullOrEmpty(githubUsername) ? githubUsername : (userEmail.Contains("@") ? userEmail.Split('@')[0] : "User");
                gitHelper = new GitHelper(authorName, userEmail);
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("URTC Collaboration Panel", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Start tracking changes to save to EditorPrefs
            EditorGUI.BeginChangeCheck();

            currentMode = (PanelMode)GUILayout.Toolbar((int)currentMode, new string[] { "Owner", "Collaborator" });
            GUILayout.Space(10);

            DrawConnectionSettings();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUIStyle style = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { textColor = statusMessage.StartsWith("Error") ? Color.red : Color.green }
                };
                GUILayout.Label(statusMessage, style);
                GUILayout.Space(10);
            }

            switch (currentMode)
            {
                case PanelMode.Owner:
                    DrawOwnerPanel();
                    break;
                case PanelMode.Collaborator:
                    DrawCollaboratorPanel();
                    break;
            }

            // Save values if any UI field changed
            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }

            GUILayout.Space(20);
            if (!string.IsNullOrEmpty(currentRepoURL) && GUILayout.Button("Open Repository"))
            {
                Application.OpenURL(currentRepoURL);
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(GetPrefKey("URTC_Email"), userEmail);
            EditorPrefs.SetString(GetPrefKey("URTC_SessionID"), sessionID);
            EditorPrefs.SetString(GetPrefKey("URTC_UserID"), userID);
            EditorPrefs.SetString(GetPrefKey("URTC_GitHubUsername"), githubUsername);
            EditorPrefs.SetString(GetPrefKey("URTC_ProjectID"), currentProjectID);
            EditorPrefs.SetString(GetPrefKey("URTC_RepoURL"), currentRepoURL);
            EditorPrefs.SetString(GetPrefKey("URTC_JoinToken"), token);
            EditorPrefs.SetString(GetPrefKey("URTC_CollabEmail"), collabUserEmail);
            URTC_ServerConfiguration.SaveApiBaseUrl(serverURL);
        }

        #region Owner Panel

        private void DrawConnectionSettings()
        {
            GUILayout.Label("Connection", EditorStyles.boldLabel);
            serverURL = EditorGUILayout.TextField("Server URL", serverURL);
            userEmail = EditorGUILayout.TextField("Your Email", userEmail);
            sessionID = EditorGUILayout.TextField("Session ID", sessionID);
            userID = EditorGUILayout.TextField("User ID", userID);

            if (serverURL.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !serverURL.Contains("localhost"))
            {
                EditorGUILayout.HelpBox("Use HTTPS for a shared or production server.", MessageType.Warning);
            }

            if (GUILayout.Button("Login with GitHub"))
            {
                Application.OpenURL(URTC_ServerConfiguration.NormalizeApiBaseUrl(serverURL) + "/github/login");
            }

            GUILayout.Space(10);
        }

        private void DrawOwnerPanel()
        {
            GUILayout.Label("Start New Collaboration", EditorStyles.boldLabel);

            projectName = EditorGUILayout.TextField("Project Name", projectName);
            projectDescription = EditorGUILayout.TextField("Description (optional)", projectDescription);

            GUI.enabled = !isLoading && !string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(sessionID);
            if (GUILayout.Button(isLoading ? "Creating..." : "2. Start Collaboration"))
            {
                StartCollaboration();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(currentProjectID))
            {
                GUILayout.Space(15);
                GUILayout.Label("Project Details", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Project ID", currentProjectID);
                EditorGUILayout.LabelField("Repository URL", currentRepoURL);
                EditorGUILayout.LabelField("Join Token", token);

                GUILayout.Space(10);
                collaboratorEmail = EditorGUILayout.TextField("Add Collaborator Email", collaboratorEmail);
                if (GUILayout.Button("Add Collaborator"))
                {
                    AddCollaborator();
                }

                if (!string.IsNullOrEmpty(token))
                {
                    EditorGUILayout.HelpBox($"Invitation Sent! Share this Join Token:\n{token}", MessageType.Info);
                    if (GUILayout.Button("Copy Join Token"))
                    {
                        GUIUtility.systemCopyBuffer = token;
                    }
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Push Changes to GitHub"))
                {
                    statusMessage = "Pushing changes...";
                    StartSimulatedPush();
                }

                if (GUILayout.Button("Pull Changes from GitHub"))
                {
                    statusMessage = "Pulling latest changes...";
                    StartSimulatedPull();
                }

                if (GUILayout.Button("Refresh Project Assets"))
                {
                    AssetDatabase.Refresh();
                    statusMessage = "Project assets refreshed.";
                }
            }
        }

        private void AddCollaborator()
        {
            if (string.IsNullOrEmpty(collaboratorEmail))
            {
                statusMessage = "Error: Collaborator email is required.";
                return;
            }

            string jsonData = JsonUtility.ToJson(new AddCollaboratorRequest
            {
                owner_email = userEmail,
                collaborator_email = collaboratorEmail,
                project_id = currentProjectID
            });
            
            EditorCoroutineUtility.StartCoroutine(SendAPIRequest(serverURL + "/api/collab/request", jsonData, "POST", (response) => {
                statusMessage = "Collaboration request sent successfully!";
                try
                {
                    var resObj = JsonUtility.FromJson<CollaborationResponse>(response);
                    token = resObj.collab_id;
                }
                catch { }
                collaboratorEmail = "";
                Repaint();
            }));
        }

        #endregion

        #region Collaborator Panel

        private void DrawCollaboratorPanel()
        {
            GUILayout.Label("Join Existing Collaboration", EditorStyles.boldLabel);

            // FIX: Collaborators need their own email field — they may not have gone through
            // the GitHub login flow in this project yet, so userEmail (Connection Settings) could be empty.
            collabUserEmail = EditorGUILayout.TextField("Your Email", collabUserEmail);
            joinToken = EditorGUILayout.TextField("Join Token", joinToken);

            EditorGUILayout.HelpBox(
                "Enter your email and the join token provided by the project owner, then click Join.\n" +
                "Note: You must Join each Unity session before pulling (GitHub token is session-only).",
                MessageType.Info);

            GUI.enabled = !isLoading && !string.IsNullOrEmpty(joinToken) && !string.IsNullOrEmpty(collabUserEmail);
            if (GUILayout.Button(isLoading ? "Joining..." : "Join Collaboration"))
            {
                JoinCollaboration();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(currentRepoURL))
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Connected Repository", currentRepoURL);

                if (string.IsNullOrEmpty(githubToken))
                {
                    EditorGUILayout.HelpBox(
                        "GitHub token not available. Join the collaboration first to enable pull.",
                        MessageType.Warning);
                }

                GUI.enabled = !string.IsNullOrEmpty(githubToken);
                if (GUILayout.Button("Pull Latest Changes"))
                {
                    statusMessage = "Pulling latest changes...";
                    StartSimulatedPull();
                }
                GUI.enabled = true;

                if (GUILayout.Button("Refresh Project Assets"))
                {
                    AssetDatabase.Refresh();
                    statusMessage = "Project assets refreshed.";
                }
            }
        }

        #endregion

        #region API Calls

        private void StartCollaboration()
        {
            serverURL = URTC_ServerConfiguration.NormalizeApiBaseUrl(serverURL);
            CollaborationRequest req = new CollaborationRequest
            {
                project_name = projectName,
                user_email = userEmail,
                project_description = projectDescription
            };

            string jsonData = JsonUtility.ToJson(req);
            StartRequestCoroutine(serverURL + "/api/start-collaboration", jsonData, isJoin: false);
        }

        private void JoinCollaboration()
        {
            serverURL = URTC_ServerConfiguration.NormalizeApiBaseUrl(serverURL);

            // FIX: Use collabUserEmail (the Collaborator tab's own email field).
            // Fall back to the top-level userEmail if collabUserEmail is somehow blank.
            string emailToUse = !string.IsNullOrEmpty(collabUserEmail) ? collabUserEmail : userEmail;

            CollaborationRequest req = new CollaborationRequest
            {
                user_email = emailToUse,
                token = joinToken
            };

            string jsonData = JsonUtility.ToJson(req);
            StartRequestCoroutine(serverURL + "/api/join-collaboration", jsonData, isJoin: true);
        }

        private void StartRequestCoroutine(string url, string jsonData, bool isJoin)
        {
            EditorCoroutineUtility.StartCoroutine(SendCollaborationRequest(url, jsonData, isJoin));
        }

        private IEnumerator SendCollaborationRequest(string url, string jsonData, bool isJoin)
        {
            isLoading = true;
            Repaint();

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(sessionID))
                {
                    www.SetRequestHeader("X-Session-ID", sessionID);
                }
                www.timeout = 30;

                yield return www.SendWebRequest();
                isLoading = false;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    statusMessage = $"Error: {www.error}";
                }
                else
                {
                    string responseText = www.downloadHandler.text;
                    CollaborationResponse response = JsonUtility.FromJson<CollaborationResponse>(responseText);

                    if (response.success)
                    {
                        statusMessage = response.message;
                        currentRepoURL = response.repo_url;
                        currentProjectID = response.project_id;
                        token = response.token;
                        githubToken = response.github_token;
                        userID = response.user_id;
                        githubUsername = response.username;

                        // If this was a Join, keep collabUserEmail populated and
                        // set userEmail from the joined email for git author info.
                        if (isJoin && !string.IsNullOrEmpty(collabUserEmail))
                        {
                            userEmail = collabUserEmail;
                        }

                        string authorName = !string.IsNullOrEmpty(githubUsername) ? githubUsername : (userEmail.Contains("@") ? userEmail.Split('@')[0] : "User");
                        gitHelper = new GitHelper(authorName, userEmail);

                        if (!string.IsNullOrEmpty(userID))
                        {
                            URTC_WebSocketClient.Connect(URTC_ServerConfiguration.GetWebSocketUrl(serverURL), userID, sessionID);
                        }

                        SavePrefs();
                    }
                    else
                    {
                        statusMessage = "Error: " + response.message;
                    }
                }

                Repaint();
            }
        }

        #endregion

        #region Git Actions

        private void StartSimulatedPush()
        {
            if (string.IsNullOrEmpty(githubToken))
            {
                statusMessage = "Error: GitHub credentials are not available. Log in and start or join the collaboration again.";
                return;
            }

            string authorName = !string.IsNullOrEmpty(githubUsername) ? githubUsername : (userEmail.Contains("@") ? userEmail.Split('@')[0] : "User");
            if (gitHelper == null) gitHelper = new GitHelper(authorName, userEmail);

            bool success = gitHelper.ExecuteFullGitWorkflow(
                projectPath,
                "Initial commit from Unity URTC Panel",
                currentRepoURL,
                authorName,
                githubToken
            );

            if (success)
                statusMessage = "Changes successfully pushed to GitHub.";
            else
                statusMessage = "Error: Failed to push changes. Check Console.";
        }

        private void StartSimulatedPull()
        {
            if (string.IsNullOrEmpty(githubToken))
            {
                statusMessage = "Error: GitHub credentials are not available. Please Join the collaboration first (required each Unity session).";
                return;
            }

            string authorName = !string.IsNullOrEmpty(githubUsername) ? githubUsername : (userEmail.Contains("@") ? userEmail.Split('@')[0] : "User");
            if (gitHelper == null) gitHelper = new GitHelper(authorName, userEmail);

            // Essential fix: Ensure repository path is initialized and remote is added
            gitHelper.InitializeRepository(projectPath);
            if (!string.IsNullOrEmpty(currentRepoURL))
            {
                gitHelper.AddRemote("origin", currentRepoURL);
            }

            bool success = gitHelper.PullFromRemote(
                "origin",
                "main",
                authorName,
                githubToken
            );

            if (success)
            {
                statusMessage = "Latest changes pulled from GitHub.";
                AssetDatabase.Refresh();
            }
            else
                statusMessage = "Error: Failed to pull changes. Check Console.";
        }

        #endregion

        private void OnDestroy()
        {
            URTC_WebSocketClient.Disconnect();
            EditorCoroutineUtility.StopAllCoroutines();
            EditorUtility.ClearProgressBar();
        }

        private delegate void APIResponseCallback(string response);

        private IEnumerator SendAPIRequest(string url, string jsonData, string method, APIResponseCallback callback)
        {
            isLoading = true;
            Repaint();

            using (UnityWebRequest www = new UnityWebRequest(url, method))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(sessionID))
                {
                    www.SetRequestHeader("X-Session-ID", sessionID);
                }
                www.timeout = 30;

                yield return www.SendWebRequest();
                isLoading = false;

                if (www.result != UnityWebRequest.Result.Success)
                {
                    statusMessage = "Error: " + www.downloadHandler.text;
                    if (string.IsNullOrEmpty(statusMessage)) statusMessage = "Error: " + www.error;
                }
                else
                {
                    callback?.Invoke(www.downloadHandler.text);
                }
                Repaint();
            }
        }
    }
}
