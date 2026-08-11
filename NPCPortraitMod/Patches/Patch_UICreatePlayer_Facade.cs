using HarmonyLib;
using UnityEngine;
using System;
using NPCPortraitMod.Helpers;

namespace NPCPortraitMod.Patches
{
    /// <summary>
    /// Harmony patches for UICreatePlayerFacade initialization and initial randomization suppression.
    /// </summary>
    public static class Patch_UICreatePlayer_Facade
    {
        // Prepares UICreatePlayer before initialization
        [HarmonyPatch(typeof(UICreatePlayer), "InitData", new Type[] { typeof(int), typeof(GameLevelType), typeof(int) })]
        public static class Patch_UICreatePlayer_InitData
        {
            public static void Postfix(UICreatePlayer __instance)
            {
                // InitData runs before UICreatePlayerFacade.Init finishes.
            }
        }

        // Suppresses initial random facade generation when editing an existing NPC
        [HarmonyPatch(typeof(UICreatePlayerFacade), "RandomFacade")]
        public static class Patch_UICreatePlayerFacade_RandomFacade
        {
            public static bool Prefix()
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    return false; // Block all randomization while editing an NPC
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
