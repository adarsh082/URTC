using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using System;
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

    #endregion

    public class URTC_Panel : EditorWindow
    {
        private enum PanelMode { Owner, Collaborator }
        private PanelMode currentMode = PanelMode.Owner;

        // Common fields
        private string serverURL = "http://localhost:8000";
        private string userEmail = "";
        private string sessionID = "";
        private string userID = "";
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
        private string githubToken = "";
        private GitHelper gitHelper;

        [MenuItem("Window/URTC Panel")]
        public static void ShowWindow()
        {
            URTC_Panel window = GetWindow<URTC_Panel>();
            window.titleContent = new GUIContent("URTC Collaboration");
            window.Show();
        }

        private void OnEnable()
        {
            projectName = Application.productName;
            // Use Path.GetDirectoryName for cross-platform robust path handling
            projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);

            // Load persisted values
            userEmail = EditorPrefs.GetString("URTC_Email", "");
            sessionID = EditorPrefs.GetString("URTC_SessionID", "");
            userID = EditorPrefs.GetString("URTC_UserID", "");
            currentProjectID = EditorPrefs.GetString("URTC_ProjectID", "");
            currentRepoURL = EditorPrefs.GetString("URTC_RepoURL", "");
            token = EditorPrefs.GetString("URTC_JoinToken", "");
            githubToken = EditorPrefs.GetString("URTC_GitHubToken", "");

            if (!string.IsNullOrEmpty(userEmail))
            {
                gitHelper = new GitHelper(userEmail.Split('@')[0], userEmail);
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("URTC Collaboration Panel", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Save values as they change
            EditorGUI.BeginChangeCheck();

            currentMode = (PanelMode)GUILayout.Toolbar((int)currentMode, new string[] { "Owner", "Collaborator" });
            GUILayout.Space(10);

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
            EditorPrefs.SetString("URTC_Email", userEmail);
            EditorPrefs.SetString("URTC_SessionID", sessionID);
            EditorPrefs.SetString("URTC_UserID", userID);
            EditorPrefs.SetString("URTC_ProjectID", currentProjectID);
            EditorPrefs.SetString("URTC_RepoURL", currentRepoURL);
            EditorPrefs.SetString("URTC_JoinToken", token);
            EditorPrefs.SetString("URTC_GitHubToken", githubToken);
        }

        #region Owner Panel

        private void DrawOwnerPanel()
        {
            GUILayout.Label("Start New Collaboration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Server URL", serverURL);
            userEmail = EditorGUILayout.TextField("Your Email", userEmail);
            sessionID = EditorGUILayout.TextField("Session ID", sessionID);
            userID = EditorGUILayout.TextField("User ID", userID);

            if (GUILayout.Button("1. Login with GitHub"))
            {
                Application.OpenURL(serverURL + "/github/login");
            }

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
            }
        }

        private void AddCollaborator()
        {
            if (string.IsNullOrEmpty(collaboratorEmail))
            {
                statusMessage = "Error: Collaborator email is required.";
                return;
            }

            string jsonData = "{\"owner_email\":\"" + userEmail + "\",\"collaborator_email\":\"" + collaboratorEmail + "\",\"project_id\":\"" + currentProjectID + "\"}";
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
            userEmail = EditorGUILayout.TextField("Your Email", userEmail);
            joinToken = EditorGUILayout.TextField("Join Token", joinToken);

            GUI.enabled = !isLoading && !string.IsNullOrEmpty(joinToken);
            if (GUILayout.Button(isLoading ? "Joining..." : "Join Collaboration"))
            {
                JoinCollaboration();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(currentRepoURL))
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Connected Repository", currentRepoURL);
                if (GUILayout.Button("Pull Latest Changes"))
                {
                    statusMessage = "Pulling latest changes...";
                    StartSimulatedPull();
                }
            }
        }

        #endregion

        #region API Calls

        private void StartCollaboration()
        {
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
            CollaborationRequest req = new CollaborationRequest
            {
                user_email = userEmail,
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

                        gitHelper = new GitHelper(userEmail.Split('@')[0], userEmail);

                        if (!string.IsNullOrEmpty(userID))
                        {
                            URTC_WebSocketClient.Connect(userID, sessionID);
                        }
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
            if (gitHelper == null) gitHelper = new GitHelper(userEmail.Split('@')[0], userEmail);

            bool success = gitHelper.ExecuteFullGitWorkflow(
                projectPath,
                "Initial commit from Unity URTC Panel",
                currentRepoURL,
                userEmail.Split('@')[0],
                githubToken
            );

            if (success)
                statusMessage = "Changes successfully pushed to GitHub.";
            else
                statusMessage = "Error: Failed to push changes. Check Console.";
        }

        private void StartSimulatedPull()
        {
            if (gitHelper == null) gitHelper = new GitHelper(userEmail.Split('@')[0], userEmail);

            // Essential fix: Ensure repository path is initialized and remote is added
            gitHelper.InitializeRepository(projectPath);
            if (!string.IsNullOrEmpty(currentRepoURL))
            {
                gitHelper.AddRemote("origin", currentRepoURL);
            }

            bool success = gitHelper.PullFromRemote(
                "origin",
                "main",
                userEmail.Split('@')[0],
                githubToken
            );

            if (success)
                statusMessage = "Latest changes pulled from GitHub.";
            else
                statusMessage = "Error: Failed to pull changes. Check Console.";
        }

        #endregion

        private void OnDestroy()
        {
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
