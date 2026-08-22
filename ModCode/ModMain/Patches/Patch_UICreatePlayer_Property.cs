using HarmonyLib;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using UnhollowerBaseLib;

namespace NPCCustomizer.Patches
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

                var npc = Helpers.UICreatePlayerHelper.GetUnitById(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.unitData == null) return;

                var ui = g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer);
                if (ui == null || ui.playerData == null || ui.playerData.unitData == null) return;

                var pp = ui.playerData.unitData.propertyData;
                var np = npc.data.unitData.propertyData;
                if (pp == null || np == null) return;

                CopyPropertyFields(np, pp);
            }

            private static HashSet<string> _seededNpcDestinies = new HashSet<string>();

            public static void ResetDestinySeedState()
            {
                _seededNpcDestinies.Clear();
            }

            public static void MarkSeeded(string npcId)
            {
                if (!string.IsNullOrEmpty(npcId))
                {
                    _seededNpcDestinies.Add(npcId);
                }
            }

            public static List<int> GetUnitDestinyIds(WorldUnitBase npc)
            {
                var result = new List<int>();
                if (npc == null) return result;
                try
                {
                    // Helper to validate that a destiny ID is strictly Nature / Innate (先天气运, type == 1)
                    bool IsNatureDestiny(int destId)
                    {
                        if (destId <= 0) return false;
                        if (g.conf != null && g.conf.roleCreateFeature != null)
                        {
                            var conf = g.conf.roleCreateFeature.GetItem(destId);
                            if (conf != null)
                            {
                                return conf.type == 1; // 1 = Nature (先天气运), 2 = Nurture/Post-natal (后天气运)
                            }
                        }
                        return false;
                    }

                    // 1. Direct check of propertyData.bornLuck (Native Il2CppReferenceArray)
                    if (npc.data?.unitData?.propertyData?.bornLuck != null)
                    {
                        foreach (var ld in npc.data.unitData.propertyData.bornLuck)
                        {
                            if (ld != null && ld.id > 0 && IsNatureDestiny(ld.id) && !result.Contains(ld.id))
                            {
                                result.Add(ld.id);
                            }
                        }
                    }

                    // 2. Check npc.allLuck (Active in-game luck instances)
                    if (npc.allLuck != null)
                    {
                        foreach (var luck in npc.allLuck)
                        {
                            if (luck == null) continue;
                            int id = 0;
                            if (luck.luckConf != null && luck.luckConf.id > 0)
                                id = luck.luckConf.id;
                            else if (luck.luckData != null && luck.luckData.id > 0)
                                id = luck.luckData.id;

                            if (id > 0 && IsNatureDestiny(id) && !result.Contains(id))
                            {
                                result.Add(id);
                            }
                        }
                    }

                    // 3. Check propertyData and dynUnitData via reflection fallback
                    if (npc.data != null)
                    {
                        object[] targets = new object[] { npc.data.unitData?.propertyData, npc.data.dynUnitData };
                        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                        foreach (var target in targets)
                        {
                            if (target == null) continue;
                            var tType = target.GetType();

                            foreach (var field in tType.GetFields(flags))
                            {
                                string fName = field.Name.ToLower();
                                if (fName.Contains("bornluck") || fName.Contains("feature"))
                                {
                                    try
                                    {
                                        var val = field.GetValue(target);
                                        if (val is Il2CppReferenceArray<DataUnit.LuckData> luckArr)
                                        {
                                            foreach (var ld in luckArr)
                                            {
                                                if (ld != null && IsNatureDestiny(ld.id) && !result.Contains(ld.id))
                                                    result.Add(ld.id);
                                            }
                                        }
                                        else if (val is Il2CppSystem.Collections.Generic.List<DataUnit.LuckData> luckList)
                                        {
                                            foreach (var ld in luckList)
                                            {
                                                if (ld != null && IsNatureDestiny(ld.id) && !result.Contains(ld.id))
                                                    result.Add(ld.id);
                                            }
                                        }
                                        else if (val is Il2CppSystem.Collections.Generic.List<int> intList)
                                        {
                                            foreach (int id in intList)
                                            {
                                                if (IsNatureDestiny(id) && !result.Contains(id))
                                                    result.Add(id);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    ModLogger.Info("[Destiny]", $"GetUnitDestinyIds found {result.Count} Nature destinies: [{string.Join(", ", result)}]");
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Destiny]", "Error in GetUnitDestinyIds: " + ex.Message);
                }
                return result;
            }

            public static void PreSeedUnitDestinies(UICreatePlayerProperty propUI, WorldUnitBase npc)
            {
                if (propUI == null || npc == null || string.IsNullOrEmpty(ModMain.EditingNpcId)) return;
                if (_seededNpcDestinies.Contains(ModMain.EditingNpcId)) return;
                _seededNpcDestinies.Add(ModMain.EditingNpcId);

                try
                {
                    var destinyIds = GetUnitDestinyIds(npc);
                    if (destinyIds == null || destinyIds.Count == 0) return;

                    var root = propUI.goGroupRoot;
                    var luckItems = root != null ? root.GetComponentsInChildren<UIBornLuckItem>(true) : UnityEngine.Object.FindObjectsOfType<UIBornLuckItem>();
                    if (luckItems == null || luckItems.Length == 0) return;

                    if (propUI.lastClickBorn != null)
                    {
                        propUI.lastClickBorn.Clear();
                    }

                    for (int i = 0; i < luckItems.Length; i++)
                    {
                        var luckItem = luckItems[i];
                        if (luckItem == null) continue;
                        var tgl = luckItem.GetComponent<UnityEngine.UI.Toggle>() ?? luckItem.GetComponentInChildren<UnityEngine.UI.Toggle>(true);

                        if (i < destinyIds.Count)
                        {
                            int destId = destinyIds[i];
                            var featureItem = g.conf.roleCreateFeature?.GetItem(destId);
                            if (featureItem != null)
                            {
                                try
                                {
                                    luckItem.InitData(featureItem);
                                    luckItem.UpdateUI();
                                    luckItem.UpdateFateBtn();
                                    if (luckItem.btnFateEffect != null) luckItem.btnFateEffect.SetActive(true);
                                }
                                catch { }

                                if (tgl != null)
                                {
                                    tgl.isOn = true;
                                    if (tgl.graphic != null) tgl.graphic.gameObject.SetActive(true);
                                    if (propUI.lastClickBorn != null && !propUI.lastClickBorn.Contains(tgl))
                                    {
                                        propUI.lastClickBorn.Add(tgl);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (luckItem.btnFateEffect != null) luckItem.btnFateEffect.SetActive(false);
                            if (tgl != null)
                            {
                                tgl.isOn = false;
                                if (tgl.graphic != null) tgl.graphic.gameObject.SetActive(false);
                            }
                        }
                    }

                    try { propUI.UpdatePlayerBornLuckData(); } catch { }
                    ModLogger.Info("[Destiny]", $"Pre-seeded and highlighted {destinyIds.Count} Nature destinies for NPC {ModMain.EditingNpcId}");
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Destiny]", "Error pre-seeding destinies: " + ex.Message);
                }
            }

            public static void Postfix(UICreatePlayerProperty __instance)
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId) || __instance == null) return;
                var npc = Helpers.UICreatePlayerHelper.GetUnitById(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.dynUnitData == null) return;

                try
                {
                    PreSeedUnitDestinies(__instance, npc);
                    RefreshPropertyUIDisplay(__instance, npc);
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Property]", "Error in RefreshPropertyUIDisplay: " + ex.Message);
                }
            }

            private static int GetDynStat(WorldUnitDynData dyn, params string[] propNames)
            {
                if (dyn == null || propNames == null) return 0;
                var type = dyn.GetType();
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                foreach (var name in propNames)
                {
                    try
                    {
                        var p = type.GetProperty(name, flags);
                        if (p != null)
                        {
                            var val = p.GetValue(dyn);
                            if (val is DynInt di) return di.value;
                            if (val is int i) return i;
                        }
                        var f = type.GetField(name, flags);
                        if (f != null)
                        {
                            var val = f.GetValue(dyn);
                            if (val is DynInt di) return di.value;
                            if (val is int i) return i;
                        }
                    }
                    catch { }
                }
                return 0;
            }

            private static void RefreshPropertyUIDisplay(UICreatePlayerProperty propUI, WorldUnitBase npc)
            {
                if (propUI == null || npc == null || npc.data == null || npc.data.dynUnitData == null) return;
                var dyn = npc.data.dynUnitData;

                int hp = GetDynStat(dyn, "hp");
                int hpMax = GetDynStat(dyn, "hpMax");
                if (hpMax == 0 && hp > 0) hpMax = hp;

                int mp = GetDynStat(dyn, "mp");
                int mpMax = GetDynStat(dyn, "mpMax");
                if (mpMax == 0 && mp > 0) mpMax = mp;

                int sp = GetDynStat(dyn, "sp");
                int spMax = GetDynStat(dyn, "spMax");
                if (spMax == 0 && sp > 0) spMax = sp;

                int atk = GetDynStat(dyn, "attack", "atk");
                int def = GetDynStat(dyn, "defense", "def");
                int travel = GetDynStat(dyn, "footSpeed", "travel");
                int martialRes = GetDynStat(dyn, "phycicalFree", "martialRes");
                int spiritualRes = GetDynStat(dyn, "magicFree", "spiritualRes");
                int crit = GetDynStat(dyn, "crit");
                int critRes = GetDynStat(dyn, "guard", "critRes");
                int agility = GetDynStat(dyn, "moveSpeed", "agility");
                int critDmg = GetDynStat(dyn, "critValue", "critDmg");
                int critDr = GetDynStat(dyn, "guardValue", "critDr");

                int sword = GetDynStat(dyn, "basisSword", "magicSword", "sword");
                int blade = GetDynStat(dyn, "basisBlade", "magicBlade", "blade");
                int spear = GetDynStat(dyn, "basisSpear", "magicSpear", "spear");
                int fist = GetDynStat(dyn, "basisFist", "magicFist", "fist");
                int palm = GetDynStat(dyn, "basisPalm", "magicPalm", "palm");
                int finger = GetDynStat(dyn, "basisFinger", "magicFinger", "finger");

                int fire = GetDynStat(dyn, "basisFire", "magicFire", "fire");
                int water = GetDynStat(dyn, "basisFroze", "magicFroze", "magicWater", "water");
                int thunder = GetDynStat(dyn, "basisThunder", "magicThunder", "lightning", "thunder");
                int wind = GetDynStat(dyn, "basisWind", "magicWind", "wind");
                int earth = GetDynStat(dyn, "basisEarth", "magicEarth", "earth");
                int wood = GetDynStat(dyn, "basisWood", "magicWood", "wood");

                int alchemy = GetDynStat(dyn, "refineElixir", "alchemy");
                int forge = GetDynStat(dyn, "refineWeapon", "forge");
                int fengshui = GetDynStat(dyn, "geomancy", "fengshui");
                int symbol = GetDynStat(dyn, "symbol", "talismans");
                int herbal = GetDynStat(dyn, "herbal", "herbology");
                int mine = GetDynStat(dyn, "mine", "mining");

                int insight = GetDynStat(dyn, "talent", "insight");
                int luck = GetDynStat(dyn, "luck");
                int mood = GetDynStat(dyn, "mood");
                int moodMax = GetDynStat(dyn, "moodMax");
                if (moodMax == 0 && mood > 0) moodMax = mood;

                int health = GetDynStat(dyn, "health");
                int healthMax = GetDynStat(dyn, "healthMax");
                if (healthMax == 0 && health > 0) healthMax = health;

                int energy = GetDynStat(dyn, "energy");
                int energyMax = GetDynStat(dyn, "energyMax");
                if (energyMax == 0 && energy > 0) energyMax = energy;

                int reputation = GetDynStat(dyn, "reputation");

                var stats = new Dictionary<string, (string valueStr, float sliderRatio)>(StringComparer.OrdinalIgnoreCase)
                {
                    // General
                    { "vitality", ($"{hp}/{hpMax}", 1f) },
                    { "气血", ($"{hp}/{hpMax}", 1f) },
                    { "energy", ($"{mp}/{mpMax}", 1f) },
                    { "灵力", ($"{mp}/{mpMax}", 1f) },
                    { "focus", ($"{sp}/{spMax}", 1f) },
                    { "念力", ($"{sp}/{spMax}", 1f) },
                    { "mood", ($"{mood}/{moodMax}", (float)mood / (moodMax > 0 ? moodMax : 100)) },
                    { "心情", ($"{mood}/{moodMax}", (float)mood / (moodMax > 0 ? moodMax : 100)) },
                    { "health", ($"{health}/{healthMax}", (float)health / (healthMax > 0 ? healthMax : 100)) },
                    { "健康", ($"{health}/{healthMax}", (float)health / (healthMax > 0 ? healthMax : 100)) },
                    { "stamina", ($"{energy}/{energyMax}", (float)energy / (energyMax > 0 ? energyMax : 100)) },
                    { "精力", ($"{energy}/{energyMax}", (float)energy / (energyMax > 0 ? energyMax : 100)) },
                    { "luck", ($"{luck}", (float)luck / 300f) },
                    { "幸运", ($"{luck}", (float)luck / 300f) },
                    { "insight", ($"{insight}", (float)insight / 300f) },
                    { "悟性", ($"{insight}", (float)insight / 300f) },
                    { "reputation", ($"{reputation}", (float)reputation / 20000f) },
                    { "声望", ($"{reputation}", (float)reputation / 20000f) },

                    // Combat
                    { "atk", ($"{atk}", (float)atk / 2000f) },
                    { "攻击", ($"{atk}", (float)atk / 2000f) },
                    { "def", ($"{def}", (float)def / 1000f) },
                    { "防御", ($"{def}", (float)def / 1000f) },
                    { "travel", ($"{travel}", (float)travel / 3000f) },
                    { "脚力", ($"{travel}", (float)travel / 3000f) },
                    { "martial res", ($"{martialRes}", (float)martialRes / 1000f) },
                    { "物理抗性", ($"{martialRes}", (float)martialRes / 1000f) },
                    { "spiritual res", ($"{spiritualRes}", (float)spiritualRes / 1000f) },
                    { "魔法抗性", ($"{spiritualRes}", (float)spiritualRes / 1000f) },
                    { "crit", ($"{crit}", (float)crit / 2000f) },
                    { "会心", ($"{crit}", (float)crit / 2000f) },
                    { "crit res", ($"{critRes}", (float)critRes / 1000f) },
                    { "护心", ($"{critRes}", (float)critRes / 1000f) },
                    { "agility", ($"{agility}", (float)agility / 1000f) },
                    { "移速", ($"{agility}", (float)agility / 1000f) },
                    { "crit dmg", ($"{critDmg}%", (float)critDmg / 500f) },
                    { "会心倍率", ($"{critDmg}%", (float)critDmg / 500f) },
                    { "crit dr", ($"{critDr}%", (float)critDr / 500f) },
                    { "护心倍率", ($"{critDr}%", (float)critDr / 500f) },

                    // Martial Arts
                    { "blade", ($"{blade}", (float)blade / 1000f) },
                    { "刀法", ($"{blade}", (float)blade / 1000f) },
                    { "spear", ($"{spear}", (float)spear / 1000f) },
                    { "枪法", ($"{spear}", (float)spear / 1000f) },
                    { "sword", ($"{sword}", (float)sword / 1000f) },
                    { "剑法", ($"{sword}", (float)sword / 1000f) },
                    { "fist", ($"{fist}", (float)fist / 1000f) },
                    { "拳法", ($"{fist}", (float)fist / 1000f) },
                    { "palm", ($"{palm}", (float)palm / 1000f) },
                    { "掌法", ($"{palm}", (float)palm / 1000f) },
                    { "finger", ($"{finger}", (float)finger / 1000f) },
                    { "指法", ($"{finger}", (float)finger / 1000f) },

                    // Spiritual Roots
                    { "fire", ($"{fire}", (float)fire / 1000f) },
                    { "火灵根", ($"{fire}", (float)fire / 1000f) },
                    { "water", ($"{water}", (float)water / 1000f) },
                    { "水灵根", ($"{water}", (float)water / 1000f) },
                    { "lightning", ($"{thunder}", (float)thunder / 1000f) },
                    { "雷灵根", ($"{thunder}", (float)thunder / 1000f) },
                    { "wind", ($"{wind}", (float)wind / 1000f) },
                    { "风灵根", ($"{wind}", (float)wind / 1000f) },
                    { "earth", ($"{earth}", (float)earth / 1000f) },
                    { "土灵根", ($"{earth}", (float)earth / 1000f) },
                    { "wood", ($"{wood}", (float)wood / 1000f) },
                    { "木灵根", ($"{wood}", (float)wood / 1000f) },

                    // Artisanship
                    { "alchemy", ($"{alchemy}", (float)alchemy / 1000f) },
                    { "炼丹", ($"{alchemy}", (float)alchemy / 1000f) },
                    { "forge", ($"{forge}", (float)forge / 1000f) },
                    { "锻造", ($"{forge}", (float)forge / 1000f) },
                    { "炼器", ($"{forge}", (float)forge / 1000f) },
                    { "feng shui", ($"{fengshui}", (float)fengshui / 1000f) },
                    { "风水", ($"{fengshui}", (float)fengshui / 1000f) },
                    { "talismans", ($"{symbol}", (float)symbol / 1000f) },
                    { "画符", ($"{symbol}", (float)symbol / 1000f) },
                    { "herbology", ($"{herbal}", (float)herbal / 1000f) },
                    { "药材", ($"{herbal}", (float)herbal / 1000f) },
                    { "采药", ($"{herbal}", (float)herbal / 1000f) },
                    { "mining", ($"{mine}", (float)mine / 1000f) },
                    { "矿物", ($"{mine}", (float)mine / 1000f) },
                    { "采矿", ($"{mine}", (float)mine / 1000f) }
                };

                // Update Righteous / Demonic (正道 / 魔道) Stand values & circle
                try
                {
                    int standUp = GetDynStat(dyn, "standUp");
                    int standDown = GetDynStat(dyn, "standDown");

                    if (propUI.textStand1 != null) propUI.textStand1.text = standUp.ToString();
                    if (propUI.textStand2 != null) propUI.textStand2.text = standDown.ToString();
                    if (propUI.textStand1_En != null) propUI.textStand1_En.text = standUp.ToString();
                    if (propUI.textStand2_En != null) propUI.textStand2_En.text = standDown.ToString();

                    int total = standUp + standDown;
                    if (total > 0)
                    {
                        float ratio = (float)standUp / total;
                        if (propUI.imgStand != null) propUI.imgStand.fillAmount = ratio;
                        if (propUI.imgStand_En != null) propUI.imgStand_En.fillAmount = ratio;
                    }
                }
                catch (Exception standEx)
                {
                    ModLogger.Warn("[Property]", "Error updating Stand (Righteous/Demonic): " + standEx.Message);
                }

                var root = propUI.goGroupRoot;
                if (root == null) return;

                var allTexts = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                if (allTexts == null) return;

                foreach (var labelText in allTexts)
                {
                    if (labelText == null) continue;
                    string rawLabel = labelText.text?.Trim() ?? "";
                    if (string.IsNullOrEmpty(rawLabel)) continue;

                    foreach (var kvp in stats)
                    {
                        if (string.Equals(rawLabel, kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            Transform rowContainer = labelText.transform.parent;
                            if (rowContainer == null) break;

                            // 1. If row has a Slider (Martial Arts, Spiritual Roots, Artisanship, Vitality, Energy, Focus)
                            var slider = rowContainer.GetComponentInChildren<UnityEngine.UI.Slider>(true);
                            if (slider != null)
                            {
                                var sliderText = slider.GetComponentInChildren<UnityEngine.UI.Text>(true);
                                if (sliderText != null)
                                {
                                    sliderText.text = kvp.Value.valueStr;
                                }
                                if (kvp.Value.sliderRatio >= 0)
                                {
                                    slider.value = Mathf.Clamp01(kvp.Value.sliderRatio);
                                }

                                var allRowTexts = rowContainer.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                                if (allRowTexts != null)
                                {
                                    foreach (var rt in allRowTexts)
                                    {
                                        if (rt != labelText && (sliderText == null || rt != sliderText))
                                        {
                                            rt.text = "";
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // 2. Plain text row without slider (Lifespan, Travel, Agility, CRIT DMG, RES, etc.)
                                var allRowTexts = rowContainer.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                                if (allRowTexts != null)
                                {
                                    foreach (var rt in allRowTexts)
                                    {
                                        if (rt != labelText && rt.gameObject != labelText.gameObject)
                                        {
                                            rt.text = kvp.Value.valueStr;
                                            break;
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }

            private static void CopyPropertyFields(DataUnit.PropertyData src, DataUnit.PropertyData dst)
            {
                if (src == null || dst == null) return;
                try
                {
                    dst.gradeID   = src.gradeID;
                    dst.age       = src.age;
                    dst.life      = src.life;
                    dst.beauty    = src.beauty;
                    dst.inTrait   = src.inTrait;
                    dst.outTrait1 = src.outTrait1;
                    dst.outTrait2 = src.outTrait2;

                    var type = typeof(DataUnit.PropertyData);
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                    foreach (var f in type.GetFields(flags))
                    {
                        if (f.FieldType == typeof(int))
                        {
                            try
                            {
                                int val = (int)f.GetValue(src);
                                f.SetValue(dst, val);
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Property]", "Error copying property fields: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(UICreatePlayerProperty), "RandomProperty")]
        public static class Patch_UICreatePlayerProperty_RandomProperty
        {
            public static void Postfix(UICreatePlayerProperty __instance)
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId) || __instance == null) return;
                var npc = Helpers.UICreatePlayerHelper.GetUnitById(ModMain.EditingNpcId);
                if (npc == null) return;

                Patch_UICreatePlayerProperty_UpdatePropertyUI.PreSeedUnitDestinies(__instance, npc);
            }
        }

        [HarmonyPatch(typeof(UICreatePlayerProperty), "OnRandom")]
        public static class Patch_UICreatePlayerProperty_OnRandom
        {
            public static void Prefix()
            {
                // When user clicks the random dice button, permit genuinely random roll by marking as seeded
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    Patch_UICreatePlayerProperty_UpdatePropertyUI.MarkSeeded(ModMain.EditingNpcId);
                }
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

                var npc = Helpers.UICreatePlayerHelper.GetUnitById(ModMain.EditingNpcId);
                if (npc == null || npc.data == null || npc.data.unitData == null) return;

                // Sync Race/Realm EVERY frame — game overwrites these fields in its own Update
                var facade = __instance.facade?.TryCast<UICreatePlayerFacade>();
                Helpers.UICreatePlayerHelper.SyncRaceAndRealmToUI(facade, npc);

                // Heavy init (name fields, trait toggles) — once per session only
                if (LastInitializedNpcId == ModMain.EditingNpcId) return;

                try
                {
                    var prop = npc.data.unitData.propertyData;
                    if (prop == null) return;

                    string surname = "", givenName = "", fullName = "";
                    try { fullName = prop.GetName(); } catch { }

                    if (prop.name != null && prop.name.Length >= 2)
                    {
                        surname   = prop.name[0] ?? "";
                        givenName = (prop.name[1] ?? "").Trim();
                    }

                    if (string.IsNullOrEmpty(surname) && string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(fullName))
                    {
                        var parts = fullName.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) { surname = parts[0]; givenName = string.Join(" ", parts, 1, parts.Length - 1); }
                        else givenName = fullName;
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
                                input.text = surname;
                            else if (objName.EndsWith("_en") || objName.Contains("given") || objName.Contains("first"))
                                input.text = givenName;
                            else
                                input.text = fullName;
                        }
                    }

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
