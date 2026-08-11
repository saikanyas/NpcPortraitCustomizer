using HarmonyLib;
using UnityEngine;
using TMPro;
using System;

namespace NPCPortraitMod.Patches
{
    /// <summary>
    /// Harmony patches for UICreatePlayer property UI, traits locking, and name input field sync.
    /// </summary>
    public static class Patch_UICreatePlayer_Property
    {
        // After RandomProperty picks random traits, overwrite selectLuck with NPC traits
        [HarmonyPatch(typeof(UICreatePlayerProperty), "RandomProperty")]
        public static class Patch_UICreatePlayerProperty_RandomProperty
        {
            public static void Postfix(UICreatePlayerProperty __instance)
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId)) return;

                var npc = g.world.unit.GetUnit(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.unitData == null) return;

                var np = npc.data.unitData.propertyData;
                if (np == null) return;

                var selectLuck = __instance.selectLuck;
                ModLogger.Info("[Traits]", $"RandomProperty: NPC inTrait={np.inTrait}, out1={np.outTrait1}, out2={np.outTrait2}, selectLuck={(selectLuck == null ? "null" : selectLuck.Count.ToString())}");

                if (selectLuck != null && selectLuck.Count >= 3)
                {
                    try
                    {
                        if (np.inTrait  != 0 && selectLuck[0]?.luckData != null) selectLuck[0].luckData.id = np.inTrait;
                        if (np.outTrait1 != 0 && selectLuck[1]?.luckData != null) selectLuck[1].luckData.id = np.outTrait1;
                        if (np.outTrait2 != 0 && selectLuck[2]?.luckData != null) selectLuck[2].luckData.id = np.outTrait2;

                        // Sync UI toggle highlights from updated selectLuck
                        try { __instance.UpdatePlayerBornLuckData(); }
                        catch (Exception ex) { ModLogger.Warn("[Traits]", "UpdatePlayerBornLuckData error: " + ex.Message); }
                    }
                    catch (Exception ex) { ModLogger.Warn("[Traits]", "selectLuck set error: " + ex.Message); }
                }
            }
        }

        // Lock Age, Life to NPC values before UpdatePropertyUI renders
        [HarmonyPatch(typeof(UICreatePlayerProperty), "UpdatePropertyUI")]
        public static class Patch_UICreatePlayerProperty_UpdatePropertyUI
        {
            // Suppress exceptions from the original method during early initialization
            public static Exception Finalizer(Exception __exception) => null;

            public static void Prefix()
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId)) return;

                var npc = g.world.unit.GetUnit(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.unitData == null) return;

                var ui = g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer);
                if (ui == null || ui.playerData == null || ui.playerData.unitData == null) return;

                var pp = ui.playerData.unitData.propertyData;
                var np = npc.data.unitData.propertyData;
                if (pp == null || np == null) return;

                pp.age    = np.age;
                pp.life   = np.life;
                pp.beauty = np.beauty;
            }
        }

        // Keeps UI Name InputField synchronized with NPC full name every frame
        [HarmonyPatch(typeof(UICreatePlayer), "Update")]
        public static class Patch_UICreatePlayer_Update
        {
            private static int _logThrottle = 0;

            public static void Postfix(UICreatePlayer __instance)
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId)) return;

                var npc = g.world.unit.GetUnit(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.unitData == null) return;

                try
                {
                    var prop = npc.data.unitData.propertyData;
                    string fullName = "";
                    try { fullName = prop.GetName(); } catch { }

                    _logThrottle++;
                    bool shouldLog = (_logThrottle % 300 == 1); // log every ~5 seconds

                    // Handle TMP_InputField (which the game uses)
                    var tmpInputs = __instance.GetComponentsInChildren<TMP_InputField>(true);
                    if (shouldLog)
                        ModLogger.Info("[Name]", $"TMP InputField count={tmpInputs?.Length ?? 0}, NPC='{fullName}'");

                    if (tmpInputs != null && tmpInputs.Length > 0)
                    {
                        foreach (var input in tmpInputs)
                        {
                            if (input == null) continue;
                            string objName = input.gameObject.name.ToLower();
                            if (shouldLog)
                                ModLogger.Info("[Name]", $"  TMP GO='{input.gameObject.name}'");

                            if (objName.Contains("family") || objName.Contains("sur")) input.text = "";
                            else if (objName.Contains("name")) input.text = fullName;
                            else input.text = fullName; // fallback: set all remaining fields to full name
                        }
                    }
                }
                catch { }
            }
        }
    }
}
