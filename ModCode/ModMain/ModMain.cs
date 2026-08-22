using System;
using System.Reflection;
using UnityEngine;

namespace NPCCustomizer
{
    /// <summary>
    /// Main entry class for NPC Customizer mod matching official Tale of Immortals Mod API.
    /// </summary>
    public class ModMain
    {
        #region State & Private Fields

        // Stores the ID of the NPC currently being customized. Null means standard player creation.
        public static string EditingNpcId = null;

        private TimerCoroutine corUpdate;
        private static HarmonyLib.Harmony harmony;

        #endregion

        #region Game Mod Lifecycle Hooks

        /// <summary>
        /// Called when the game loads the mod.
        /// </summary>
        public void Init()
        {
            try
            {
                try
                {
                    MelonLoader.MelonLogger.Msg("================================================================");
                    MelonLoader.MelonLogger.Msg("[NPCCustomizer] NPC Customizer Mod Loaded! (Press F9 for Player)");
                    MelonLoader.MelonLogger.Msg("================================================================");
                }
                catch { }

                ModLogger.LoadConfig();

                if (harmony != null)
                {
                    harmony.UnpatchSelf();
                    harmony = null;
                }

                harmony = new HarmonyLib.Harmony("NPCCustomizer");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                corUpdate = g.timer.Frame(new Action(OnUpdate), 1, true);

                ModLogger.Info("[Init]", "NPC Customizer Mod Initialized! Press F9 in-game to customize Player portrait.");
            }
            catch (Exception ex)
            {
                ModLogger.Error("[Init]", "Error initializing NPCCustomizer", ex);
            }
        }

        /// <summary>
        /// Called when exiting to main menu or when mod is destroyed.
        /// </summary>
        public void Destroy()
        {
            try
            {
                if (corUpdate != null)
                {
                    g.timer.Stop(corUpdate);
                    corUpdate = null;
                }

                if (harmony != null)
                {
                    harmony.UnpatchSelf();
                    harmony = null;
                }

                ModLogger.Info("[Destroy]", "NPC Customizer Mod Destroyed.");
            }
            catch (Exception ex)
            {
                ModLogger.Error("[Destroy]", "Error destroying NPCPortraitMod", ex);
            }
        }

        /// <summary>
        /// Frame update loop registered via g.timer.Frame.
        /// </summary>
        private void OnUpdate()
        {
            // Auto-clean EditingNpcId if the UI is no longer open in game
            if (!string.IsNullOrEmpty(EditingNpcId))
            {
                var curUI = g.ui != null ? g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer) : null;
                if (curUI == null || !curUI.gameObject.activeInHierarchy)
                {
                    EditingNpcId = null;
                    Patches.Patch_UICreatePlayer_Property.Patch_UICreatePlayerProperty_UpdatePropertyUI.ResetDestinySeedState();
                }
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
            {
                HandleF9Shortcut();
            }
        }

        #endregion

        #region Shortcut Handlers

        private void HandleF9Shortcut()
        {
            try
            {
                if (g.world != null && g.world.playerUnit != null && g.ui != null)
                {
                    var player = g.world.playerUnit;
                    if (player.data != null && player.data.unitData != null)
                    {
                        string playerId = player.data.unitData.unitID;
                        ModLogger.Info("[UI-Open]", $"F9 pressed: Opening Customize for Player (ID: {playerId})");
                        EditingNpcId = playerId;
                        Patches.Patch_UICreatePlayer_Property.Patch_UICreatePlayer_Update.LastInitializedNpcId = null;
                        Patches.Patch_UICreatePlayer_Property.Patch_UICreatePlayerProperty_UpdatePropertyUI.ResetDestinySeedState();

                        var ui = g.ui.OpenUI<UICreatePlayer>(UIType.CreatePlayer);
                        if (ui != null)
                        {
                            try { ui.InitData(0, GameLevelType.Common, 0); } catch { }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ModLogger.Error("[UI-Open]", "Error opening player customize via F9", e);
            }
        }

        #endregion
    }
}
