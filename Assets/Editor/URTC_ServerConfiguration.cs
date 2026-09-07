using System;
using UnityEditor;

namespace URTC.Editor
{
    /// <summary>Centralizes and validates the deployment address used by the editor plug-in.</summary>
    internal static class URTC_ServerConfiguration
    {
        internal const string DefaultApiBaseUrl = "http://localhost:8000";
        private const string ApiUrlPreference = "URTC_ServerUrl";

        internal static string LoadApiBaseUrl()
        {
            return NormalizeApiBaseUrl(EditorPrefs.GetString(ApiUrlPreference, DefaultApiBaseUrl));
        }

        internal static void SaveApiBaseUrl(string url)
        {
            EditorPrefs.SetString(ApiUrlPreference, NormalizeApiBaseUrl(url));
        }

        internal static string NormalizeApiBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return DefaultApiBaseUrl;

            url = url.Trim().TrimEnd('/');
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return DefaultApiBaseUrl;
            }

            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/');
        }

        internal static string GetWebSocketUrl(string apiBaseUrl)
        {
            var apiUri = new Uri(NormalizeApiBaseUrl(apiBaseUrl));
            var builder = new UriBuilder(apiUri)
            {
                Scheme = apiUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
                Port = apiUri.Port,
                Path = apiUri.AbsolutePath.TrimEnd('/') + "/ws"
            };
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }
    }
}
