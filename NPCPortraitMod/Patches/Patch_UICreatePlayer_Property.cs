using HarmonyLib;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

namespace NPCPortraitMod.Patches
{
    /// <summary>
    /// Harmony patches for UICreatePlayer property UI, traits locking, and name input field sync.
    /// </summary>
    public static class Patch_UICreatePlayer_Property
    {
        private static bool _isSyncingTraits = false;

        // Static trait ID to name mapping fallback for English and Chinese locales
        public static readonly Dictionary<int, string[]> TraitNameMap = new Dictionary<int, string[]>()
        {
            { 1,  new[] { "Selfless", "无私" } },
            { 2,  new[] { "Upstanding", "正直" } },
            { 3,  new[] { "Kind", "仁慈" } },
            { 4,  new[] { "Middle Way", "中庸" } },
            { 5,  new[] { "Wicked", "狂妄" } },
            { 6,  new[] { "Selfish", "利己" } },
            { 7,  new[] { "Evil", "邪恶" } },
            { 8,  new[] { "Caring", "重情" } },
            { 9,  new[] { "Loyal to friends", "义气" } },
            { 10, new[] { "Protective", "护短" } },
            { 11, new[] { "Self-centered", "孤僻" } },
            { 12, new[] { "Family-Oriented", "爱护后辈" } },
            { 13, new[] { "Glory Hound", "名气" } },
            { 14, new[] { "Power-hungry", "权利" } },
            { 15, new[] { "Vengeful", "报复" } },
            { 16, new[] { "Carefree", "随性" } },
            { 17, new[] { "Romantic", "情种" } },
            { 18, new[] { "Traditional", "传承" } },
            { 19, new[] { "Faithful", "忠贞" } }
        };

        // Helper to sync UI trait toggles directly with NPC traits
        public static void SyncTraitToggles(Component uiComponent, DataUnit.PropertyData np)
        {
            if (_isSyncingTraits) return; // Prevent recursive re-entry loop
            if (uiComponent == null || np == null) return;

            try
            {
                _isSyncingTraits = true;
                List<string> activeNames = new List<string>();

                int[] traitIds = new int[] { np.inTrait, np.outTrait1, np.outTrait2 };
                foreach (int tid in traitIds)
                {
                    if (tid == 0) continue;

                    // Try lookup via game configuration
                    if (g.conf != null && g.conf.roleCreateFeature != null)
                    {
                        var item = g.conf.roleCreateFeature.GetItem(tid);
                        if (item != null && !string.IsNullOrEmpty(item.name))
                        {
                            string locName = GameTool.LS(item.name);
                            if (!string.IsNullOrEmpty(locName)) activeNames.Add(locName.Trim());
                        }
                    }

                    // Fallback to static mapping if localized name lookup was empty
                    if (TraitNameMap.TryGetValue(tid, out var mapNames))
                    {
                        foreach (var name in mapNames)
                        {
                            if (!activeNames.Contains(name)) activeNames.Add(name);
                        }
                    }
                }

                ModLogger.Info("[Traits]", $"SyncTraitToggles: Active NPC traits = [{string.Join(", ", activeNames)}] (in={np.inTrait}, out1={np.outTrait1}, out2={np.outTrait2})");

                var toggles = uiComponent.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
                if (toggles != null && toggles.Length > 0)
                {
                    foreach (var tgl in toggles)
                    {
                        if (tgl == null) continue;

                        var textObj = tgl.GetComponentInChildren<UnityEngine.UI.Text>(true);
                        var tmpTextObj = tgl.GetComponentInChildren<TMP_Text>(true);
                        string label = textObj != null ? textObj.text : (tmpTextObj != null ? tmpTextObj.text : "");

                        if (string.IsNullOrEmpty(label)) continue;
                        label = label.Trim();

                        bool shouldBeOn = false;
                        foreach (var activeName in activeNames)
                        {
                            if (string.Equals(label, activeName, StringComparison.OrdinalIgnoreCase))
                            {
                                shouldBeOn = true;
                                break;
                            }
                        }

                        // Only toggle traits (skip sex / appearance toggles)
                        bool isTraitToggle = false;
                        foreach (var kvp in TraitNameMap)
                        {
                            foreach (var n in kvp.Value)
                            {
                                if (string.Equals(label, n, StringComparison.OrdinalIgnoreCase))
                                {
                                    isTraitToggle = true;
                                    break;
                                }
                            }
                            if (isTraitToggle) break;
                        }

                        if (isTraitToggle && tgl.isOn != shouldBeOn)
                        {
                            tgl.isOn = shouldBeOn;
                            ModLogger.Info("[Traits]", $"  Trait toggle '{label}' -> isOn={shouldBeOn}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn("[Traits]", "Error in SyncTraitToggles: " + ex.Message);
            }
            finally
            {
                _isSyncingTraits = false;
            }
        }

        // Lock Age, Life, and Traits to NPC values before UpdatePropertyUI renders
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

                pp.age       = np.age;
                pp.life      = np.life;
                pp.beauty    = np.beauty;
                pp.inTrait   = np.inTrait;
                pp.outTrait1 = np.outTrait1;
                pp.outTrait2 = np.outTrait2;
            }
        }

        // Keeps UI Name InputFields and Trait toggles synchronized with NPC
        [HarmonyPatch(typeof(UICreatePlayer), "Update")]
        public static class Patch_UICreatePlayer_Update
        {
            public static string LastInitializedNpcId = null;

            public static void Postfix(UICreatePlayer __instance)
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    LastInitializedNpcId = null;
                    return;
                }

                // Only initialize input fields and trait toggles once per NPC customization session
                if (LastInitializedNpcId == ModMain.EditingNpcId) return;

                var npc = g.world.unit.GetUnit(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.unitData == null) return;

                try
                {
                    var prop = npc.data.unitData.propertyData;
                    if (prop == null) return;

                    string surname = "";
                    string givenName = "";
                    string fullName = "";

                    try { fullName = prop.GetName(); } catch { }

                    if (prop.name != null && prop.name.Length >= 2)
                    {
                        surname = prop.name[0] ?? "";
                        givenName = (prop.name[1] ?? "").Trim();
                    }

                    if (string.IsNullOrEmpty(surname) && string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(fullName))
                    {
                        var parts = fullName.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            surname = parts[0];
                            givenName = string.Join(" ", parts, 1, parts.Length - 1);
                        }
                        else
                        {
                            givenName = fullName;
                        }
                    }

                    var tmpInputs = __instance.GetComponentsInChildren<TMP_InputField>(true);
                    ModLogger.Info("[Name]", $"Initializing InputFields for NPC '{ModMain.EditingNpcId}': Surname='{surname}', GivenName='{givenName}', Full='{fullName}' (inputs count={tmpInputs?.Length ?? 0})");

                    if (tmpInputs != null && tmpInputs.Length > 0)
                    {
                        foreach (var input in tmpInputs)
                        {
                            if (input == null) continue;
                            string objName = input.gameObject.name.ToLower();
                            ModLogger.Info("[Name]", $"  Setting TMP GO='{input.gameObject.name}'");

                            try { input.interactable = true; } catch { }

                            if (objName.Contains("family") || objName.Contains("sur"))
                            {
                                input.text = surname;
                            }
                            else if (objName.EndsWith("_en") || objName.Contains("given") || objName.Contains("first"))
                            {
                                input.text = givenName;
                            }
                            else
                            {
                                input.text = fullName;
                            }
                        }
                    }

                    // Sync trait toggles ONCE on UI initialization
                    SyncTraitToggles(__instance, prop);

                    LastInitializedNpcId = ModMain.EditingNpcId;
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Name]", "Error initializing name/trait inputs: " + ex.Message);
                }
            }
        }
    }
}
