using HarmonyLib;
using UnityEngine;
using System;
using NPCPortraitCustomizer.Helpers;

namespace NPCPortraitCustomizer.Patches
{
    /// <summary>
    /// Harmony patches for UICreatePlayerFacade randomization suppression and NPC face injection.
    /// </summary>
    public static class Patch_UICreatePlayer_Facade
    {
        // Blocks RandomFacade while editing an existing NPC to prevent changing outfit/face items
        [HarmonyPatch(typeof(UICreatePlayerFacade), "RandomFacade")]
        public static class Patch_UICreatePlayerFacade_RandomFacade
        {
            public static bool Prefix()
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    ModLogger.Info("[Facade]", "RandomFacade BLOCKED because EditingNpcId is active.");
                    return false; // Prevent game from randomizing outfit/face items
                }
                return true;
            }
        }

        // Injects NPC model data after UICreatePlayerFacade finishes building options
        [HarmonyPatch(typeof(UICreatePlayerFacade), "Init", new Type[] { typeof(Transform) })]
        public static class Patch_UICreatePlayerFacade_Init
        {
            public static void Postfix(UICreatePlayerFacade __instance)
            {
                var ui = g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer);
                UICreatePlayerHelper.ApplyNpcFaceToUI(ui, __instance);
            }
        }
    }
}
