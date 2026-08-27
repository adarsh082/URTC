using UnityEngine;
using UnityEditor;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace URTC.Editor
{
    [InitializeOnLoad]
    public class URTC_WebSocketClient
    {
        private static ClientWebSocket webSocket = null;
        private static CancellationTokenSource cancellationTokenSource;
        private static bool isConnecting = false;

        static URTC_WebSocketClient()
        {
        }

        public static async void Connect(string serverURL, string userID, string sessionID)
        {
            if (isConnecting || (webSocket != null && webSocket.State == WebSocketState.Open))
            {
                Debug.Log("[URTC] WebSocket already connected or connecting.");
                return;
            }

            isConnecting = true;
            cancellationTokenSource = new CancellationTokenSource();
            webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader("X-Session-ID", sessionID);

            try
            {
                string finalUrl = $"{serverURL}?user_id={Uri.EscapeDataString(userID)}";
                Debug.Log($"[URTC] Attempting WebSocket connection to: {finalUrl}");
                
                Uri serverUri = new Uri(finalUrl);
                await webSocket.ConnectAsync(serverUri, cancellationTokenSource.Token);
                Debug.Log("[URTC] WebSocket Connected Successfully");
                
                _ = ReceiveMessages();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[URTC] WebSocket Connection Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.LogError($"[URTC] Inner Error: {ex.InnerException.Message}");
                }
            }
            finally
            {
                isConnecting = false;
            }
        }

        public static async void Disconnect()
        {
            if (webSocket != null)
            {
                try
                {
                    cancellationTokenSource?.Cancel();
                    if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    webSocket.Dispose();
                    webSocket = null;
                    Debug.Log("[URTC] WebSocket Disconnected");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[URTC] Error during WebSocket disconnect: {ex.Message}");
                }
            }
        }

        private static async Task ReceiveMessages()
        {
            byte[] buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        Debug.Log("[URTC] WebSocket Closed by Server");
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (cancellationTokenSource == null || !cancellationTokenSource.IsCancellationRequested)
                {
                    Debug.LogError($"[URTC] WebSocket Receive Error: {ex.Message}");
                }
            }
        }

        private static void ProcessMessage(string json)
        {
            Debug.Log($"[URTC] WebSocket Message Received: {json}");
            
            // In a real editor plugin, we would use EditorApplication.delayCall 
            // to interact with the UI from a background thread
            EditorApplication.delayCall += () => {
                // Inform the URTC_Panel if it's open
                // URTC_Panel.OnMessageReceived(json);
                
                // For now, just show a notification if it's a collab request
                if (json.Contains("collaboration_request"))
                {
                    EditorUtility.DisplayDialog("URTC Notification", "New collaboration request received! Check the URTC Panel.", "OK");
                }
            };
        }
    }
}
