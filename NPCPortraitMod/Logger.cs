using MelonLoader;
using System;

namespace NPCPortraitMod
{
    /// <summary>
    /// Centralized logging utility for NPCPortraitMod wrapper around MelonLogger.
    /// </summary>
    public static class ModLogger
    {
        #region Settings & Fields

        // Controls verbose debug logging output
        public static bool EnableDebug = true;
        
        private const string GlobalPrefix = "[NPCPortraitMod]";

        #endregion

        #region Logging Methods

        public static void Debug(string subsystem, string message)
        {
            if (EnableDebug)
            {
                MelonLogger.Msg(ConsoleColor.DarkGray, $"{GlobalPrefix}{subsystem} [DEBUG] {message}");
            }
        }

        public static void Info(string subsystem, string message)
        {
            MelonLogger.Msg($"{GlobalPrefix}{subsystem} {message}");
        }

        public static void Warn(string subsystem, string message)
        {
            MelonLogger.Warning($"{GlobalPrefix}{subsystem} {message}");
        }

        public static void Error(string subsystem, string message, Exception ex = null)
        {
            if (ex != null)
            {
                MelonLogger.Error($"{GlobalPrefix}{subsystem} {message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            else
            {
                MelonLogger.Error($"{GlobalPrefix}{subsystem} {message}");
            }
        }

        #endregion
    }
}
