using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NPCPortraitCustomizer
{
    /// <summary>
    /// Centralized logging utility for NPCPortraitCustomizer using UnityEngine.Debug.
    /// Configured via ModAssets/config.json (default EnableDebugLog: false).
    /// </summary>
    public static class ModLogger
    {
        #region Settings & Fields

        // Controls verbose debug logging output (default: false)
        public static bool EnableDebug = false;
        
        private const string GlobalPrefix = "[NPCPortraitCustomizer]";

        #endregion

        #region Config Management

        public static void LoadConfig()
        {
            try
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string configPath = null;

                if (!string.IsNullOrEmpty(dllDir))
                {
                    DirectoryInfo current = new DirectoryInfo(dllDir);
                    for (int i = 0; i < 5 && current != null; i++)
                    {
                        string candidate = Path.Combine(current.FullName, "ModAssets", "config.json");
                        if (File.Exists(candidate))
                        {
                            configPath = candidate;
                            break;
                        }
                        candidate = Path.Combine(current.FullName, "config.json");
                        if (File.Exists(candidate))
                        {
                            configPath = candidate;
                            break;
                        }
                        current = current.Parent;
                    }
                }

                if (string.IsNullOrEmpty(configPath))
                {
                    string targetFolder = string.IsNullOrEmpty(dllDir) ? "." : Path.Combine(dllDir, "..", "..", "ModAssets");
                    if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
                    configPath = Path.Combine(targetFolder, "config.json");

                    string defaultJson = "{\n  \"EnableDebugLog\": false\n}";
                    File.WriteAllText(configPath, defaultJson);
                    EnableDebug = false;
                    return;
                }

                string content = File.ReadAllText(configPath).ToLower();
                if (content.Contains("\"enabledebuglog\""))
                {
                    int idx = content.IndexOf("\"enabledebuglog\"");
                    string sub = content.Substring(idx);
                    EnableDebug = sub.Contains("true");
                }
                else
                {
                    EnableDebug = false;
                }
            }
            catch (Exception ex)
            {
                EnableDebug = false;
                UnityEngine.Debug.LogWarning($"{GlobalPrefix}[Config] Error loading config.json: {ex.Message}");
            }
        }

        #endregion

        #region Logging Methods

        public static void Debug(string subsystem, string message)
        {
            if (EnableDebug)
            {
                UnityEngine.Debug.Log($"{GlobalPrefix}{subsystem} [DEBUG] {message}");
            }
        }

        public static void Info(string subsystem, string message)
        {
            UnityEngine.Debug.Log($"{GlobalPrefix}{subsystem} {message}");
        }

        public static void Warn(string subsystem, string message)
        {
            UnityEngine.Debug.LogWarning($"{GlobalPrefix}{subsystem} {message}");
        }

        public static void Error(string subsystem, string message, Exception ex = null)
        {
            if (ex != null)
            {
                UnityEngine.Debug.LogError($"{GlobalPrefix}{subsystem} {message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            else
            {
                UnityEngine.Debug.LogError($"{GlobalPrefix}{subsystem} {message}");
            }
        }

        #endregion
    }
}
