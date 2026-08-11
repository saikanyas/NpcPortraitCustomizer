using MelonLoader;
using System;
using UnityEngine;

#region Assembly Info
[assembly: MelonInfo(typeof(NPCPortraitMod.ModMain), "NPC Portrait Customizer", "1.0.0", "saikanyas")]
[assembly: MelonGame("guigugame", "guigubahuang")]
#endregion

namespace NPCPortraitMod
{
    public class ModMain : MelonMod
    {
        #region State Properties

        // Stores the ID of the NPC currently being customized. Null means standard player creation.
        public static string EditingNpcId = null;

        #endregion

        #region Melon Lifecycle Hooks

        public override void OnInitializeMelon()
        {
            ModLogger.Info("[Init]", "NPC Portrait Customizer Mod Loaded! Press F9 in-game to customize Player portrait.");
        }

        public override void OnUpdate()
        {
            if (!string.IsNullOrEmpty(EditingNpcId)) return;

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
