using MelonLoader;
using System;
using UnityEngine;

[assembly: MelonInfo(typeof(NPCPortraitMod.ModMain), "NPC Portrait Customizer", "1.0.0", "saikanyas")]
[assembly: MelonGame("guigugame", "guigubahuang")]

namespace NPCPortraitMod
{
    public class ModMain : MelonMod
    {
        // ใช้เก็บ ID ของ NPC ที่กำลังถูกแก้ไขหน้าตา
        // ถ้าเป็น null แสดงว่าไม่ได้กำลังแก้ NPC (สร้างตัวละครใหม่ตามปกติ)
        public static string EditingNpcId = null;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("NPC Portrait Customizer Mod Loaded! Press F9 in-game to customize Player portrait.");
            // Harmony patches are applied automatically by MelonLoader 0.5.7
        }

        public override void OnUpdate()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9))
            {
                try
                {
                    if (g.world != null && g.world.playerUnit != null && g.ui != null)
                    {
                        var player = g.world.playerUnit;
                        if (player.data != null && player.data.unitData != null)
                        {
                            string playerId = player.data.unitData.unitID;
                            MelonLogger.Msg($"[NPCPortraitMod] F9 pressed: Opening Customize for Player (ID: {playerId})");
                            EditingNpcId = playerId;
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
                    MelonLogger.Error("[NPCPortraitMod] Error opening player customize via F9: " + e);
                }
            }
        }
    }
}
