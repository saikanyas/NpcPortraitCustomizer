using HarmonyLib;
using UnityEngine;
using System;

namespace NPCCustomizer.Patches
{
    /// <summary>
    /// Harmony patches to suppress game shortcuts and hotkeys while the Customize UI is open.
    /// </summary>
    public static class Patch_InputBlock
    {
        // Block InputSDK.GetKeyDown game shortcut queries while customizing
        [HarmonyPatch(typeof(InputSDK), "GetKeyDown", new Type[] { typeof(InputButton) })]
        public static class Patch_InputSDK_GetKeyDown
        {
            public static bool Prefix(ref bool __result)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Block InputSDK.GetKey queries while customizing
        [HarmonyPatch(typeof(InputSDK), "GetKey", new Type[] { typeof(InputButton) })]
        public static class Patch_InputSDK_GetKey
        {
            public static bool Prefix(ref bool __result)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Block InputSDK.GetKeyUp queries while customizing
        [HarmonyPatch(typeof(InputSDK), "GetKeyUp", new Type[] { typeof(InputButton) })]
        public static class Patch_InputSDK_GetKeyUp
        {
            public static bool Prefix(ref bool __result)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Block UnityEngine.Input.GetKeyDown queries while customizing
        [HarmonyPatch(typeof(UnityEngine.Input), "GetKeyDown", new Type[] { typeof(KeyCode) })]
        public static class Patch_UnityEngine_Input_GetKeyDown
        {
            public static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // Block UnityEngine.Input.GetKey queries while customizing
        [HarmonyPatch(typeof(UnityEngine.Input), "GetKey", new Type[] { typeof(KeyCode) })]
        public static class Patch_UnityEngine_Input_GetKey
        {
            public static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}
