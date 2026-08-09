#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace TD.Editor
{
    [InitializeOnLoad]
    internal static class TDMcpAutoConnect
    {
        private static bool _attempted;

        static TDMcpAutoConnect()
        {
            EditorApplication.delayCall += TryConnect;
        }

        [MenuItem("TD/Tools/Connect Unity MCP")]
        private static async void ConnectFromMenu()
        {
            _attempted = false;
            await ConnectAsync(forceRestart: true);
        }

        private static async void TryConnect()
        {
            if (_attempted || Application.isBatchMode)
            {
                return;
            }

            _attempted = true;
            // Domain reloads can leave the package reporting an HTTP transport as
            // running while its Unity bridge socket is stale. A full reconnect is
            // required before automation can issue play-mode commands again.
            await ConnectAsync(forceRestart: true);
        }

        private static async Task ConnectAsync(bool forceRestart)
        {
            try
            {
                if (MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http))
                {
                    if (!forceRestart)
                    {
                        var verification = await MCPServiceLocator.Bridge.VerifyAsync();
                        if (verification.Success)
                        {
                            return;
                        }
                    }

                    await MCPServiceLocator.TransportManager.StopAsync(TransportMode.Http);
                }

                if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
                {
                    MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
                    for (var attempt = 0; attempt < 20 && !MCPServiceLocator.Server.IsLocalHttpServerReachable(); attempt++)
                    {
                        await Task.Delay(500);
                    }
                }

                if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
                {
                    Debug.LogWarning("TD MCP auto-connect could not reach the local HTTP server.");
                    return;
                }

                if (!await MCPServiceLocator.Bridge.StartAsync())
                {
                    Debug.LogWarning("TD MCP auto-connect could not start the Unity bridge.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"TD MCP auto-connect failed: {exception.Message}");
            }
        }
    }
}
#endif
