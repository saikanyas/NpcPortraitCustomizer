using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using UnhollowerBaseLib;
using NPCCustomizer.Helpers;

namespace NPCCustomizer.Patches
{
    /// <summary>
    /// Harmony patches for UICreatePlayer DestroyUI finalizer exception suppression and OnOkClick save logic.
    /// </summary>
    public static class Patch_UICreatePlayer_Save
    {
        // Suppresses exception inside DestroyUI to ensure input lock is properly released
        [HarmonyPatch(typeof(UICreatePlayer), "DestroyUI")]
        public static class Patch_UICreatePlayer_DestroyUI
        {
            public static Exception Finalizer(Exception __exception)
            {
                return null;
            }

            public static void Postfix()
            {
                Patch_UICreatePlayer_Property.Patch_UICreatePlayerProperty_UpdatePropertyUI.ResetDestinySeedState();
            }
        }

        // Saves modified portrait and name back to the target NPC on OK button click
        [HarmonyPatch(typeof(UICreatePlayer), "OnOkClick")]
        public static class Patch_UICreatePlayer_OnOkClick
        {
            private static bool _isSaving = false;

            public static bool Prefix(UICreatePlayer __instance)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    if (_isSaving) return false;
                    _isSaving = true;

                    string npcId = ModMain.EditingNpcId;
                    try
                    {
                        ModLogger.Info("[Save]", "Saving new face to NPC: " + npcId);

                        var npc = UICreatePlayerHelper.GetUnitById(npcId);
                        
                        if (npc != null && npc.data != null && npc.data.dynUnitData != null && npc.data.dynUnitData.modelData != null && __instance.uiFacade != null && __instance.uiFacade.portraitModel != null && __instance.uiFacade.portraitModel.data != null)
                        {
                            var newModel = __instance.uiFacade.portraitModel.data;

                            if (newModel.sex != 0 && npc.data != null && npc.data.unitData != null && npc.data.unitData.propertyData != null)
                            {
                                npc.data.unitData.propertyData.sex = (UnitSexType)newModel.sex;
                                ModLogger.Info("[Save]", $"Updated NPC sex: {newModel.sex}");
                            }

                            // Use official game SetModelData API (updates both 2D portrait model and 3D Spine combat/map model)
                            try
                            {
                                BattleModelHumanData battleModelHumanData = null;
                                if (npc.data.unitData?.propertyData?.battleModelData != null)
                                {
                                    battleModelHumanData = npc.data.unitData.propertyData.battleModelData.Clone();
                                }
                                else
                                {
                                    battleModelHumanData = new BattleModelHumanData();
                                }
                                battleModelHumanData.body = newModel.body;
                                npc.data.SetModelData(newModel, battleModelHumanData);
                                ModLogger.Info("[Save]", "Called official npc.data.SetModelData(newModel, battleModelHumanData) successfully.");
                            }
                            catch (Exception smEx)
                            {
                                ModLogger.Warn("[Save]", "Error in SetModelData: " + smEx.Message);
                            }

                            // Sync modelData to both dynamic runtime (dynUnitData) AND persistent save data (unitData/propertyData)
                            SyncModelDataToUnit(npc, newModel);

                            try
                            {
                                var tmpInputs = __instance.GetComponentsInChildren<TMPro.TMP_InputField>(true);
                                if (tmpInputs != null && tmpInputs.Length > 0 && npc.data != null && npc.data.unitData != null && npc.data.unitData.propertyData != null)
                                {
                                    string saveSurname = null;
                                    string saveGivenName = null;

                                    foreach (var input in tmpInputs)
                                    {
                                        if (input == null) continue;
                                        string objName = input.gameObject.name.ToLower();
                                        if (objName.Contains("family") || objName.Contains("sur"))
                                            saveSurname = input.text;
                                        else if (objName.EndsWith("_en") || objName.Contains("given") || objName.Contains("first"))
                                            saveGivenName = input.text;
                                    }

                                    var oldName = npc.data.unitData.propertyData.name;
                                    string oldSurname = (oldName != null && oldName.Length > 0) ? oldName[0] : "";
                                    string oldGivenName = (oldName != null && oldName.Length > 1) ? oldName[1] : "";

                                    string finalSurname = !string.IsNullOrEmpty(saveSurname) ? saveSurname : oldSurname;
                                    string finalGiven = !string.IsNullOrEmpty(saveGivenName) ? saveGivenName.Trim() : oldGivenName.Trim();

                                    // Tale of Immortals format requirement: Given name (name[1]) MUST have a leading space (" " + givenName)!
                                    npc.data.unitData.propertyData.name = new string[2] { finalSurname, " " + finalGiven };
                                    ModLogger.Info("[Save]", $"Updated NPC name: Surname='{finalSurname}', GivenName=' {finalGiven}'");
                                }
                            }
                            catch (Exception nameEx)
                            {
                                ModLogger.Warn("[Save]", "Error saving modified name: " + nameEx.Message);
                            }

                            try
                            {
                                var targetProp = npc.data.unitData.propertyData;
                                if (targetProp != null)
                                {
                                    int newInTrait = 0;
                                    int newOut1 = 0;
                                    int newOut2 = 0;

                                    if (__instance.playerData != null && __instance.playerData.unitData != null && __instance.playerData.unitData.propertyData != null)
                                    {
                                        var pData = __instance.playerData.unitData.propertyData;
                                        newInTrait = pData.inTrait;
                                        newOut1 = pData.outTrait1;
                                        newOut2 = pData.outTrait2;
                                    }

                                    var toggles = __instance.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
                                    if (toggles != null && toggles.Length > 0)
                                    {
                                        var selectedOutTraits = new System.Collections.Generic.List<int>();
                                        foreach (var tgl in toggles)
                                        {
                                            if (tgl == null || !tgl.isOn) continue;
                                            var textObj = tgl.GetComponentInChildren<UnityEngine.UI.Text>(true);
                                            var tmpTextObj = tgl.GetComponentInChildren<TMPro.TMP_Text>(true);
                                            string label = textObj != null ? textObj.text : (tmpTextObj != null ? tmpTextObj.text : "");
                                            if (string.IsNullOrEmpty(label)) continue;
                                            label = label.Trim();

                                            foreach (var kvp in Patch_UICreatePlayer_Property.TraitNameMap)
                                            {
                                                int traitId = kvp.Key;
                                                bool matched = false;
                                                foreach (var name in kvp.Value)
                                                {
                                                    if (string.Equals(label, name, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        if (traitId >= 1 && traitId <= 7)
                                                        {
                                                            newInTrait = traitId;
                                                        }
                                                        else if (traitId >= 8 && traitId <= 19)
                                                        {
                                                            if (!selectedOutTraits.Contains(traitId))
                                                                selectedOutTraits.Add(traitId);
                                                        }
                                                        matched = true;
                                                        break;
                                                    }
                                                }
                                                if (matched) break;
                                            }
                                        }

                                        if (selectedOutTraits.Count > 0) newOut1 = selectedOutTraits[0];
                                        if (selectedOutTraits.Count > 1) newOut2 = selectedOutTraits[1];
                                    }

                                    if (newInTrait != 0) targetProp.inTrait = newInTrait;
                                    if (newOut1 != 0) targetProp.outTrait1 = newOut1;
                                    if (newOut2 != 0) targetProp.outTrait2 = newOut2;

                                    ModLogger.Info("[Save]", $"Updated NPC traits: inTrait={targetProp.inTrait}, outTrait1={targetProp.outTrait1}, outTrait2={targetProp.outTrait2}");
                                }
                            }
                            catch (Exception traitEx)
                            {
                                ModLogger.Warn("[Save]", "Error saving modified traits: " + traitEx.Message);
                            }

                            try
                            {
                                // Extract and save ALL selected Innate Destinies (先天气运) without 3-count limit
                                var selectedDestinyIds = new List<int>();

                                // Source 1: Check all UIBornLuckItem instances (supports 3, 9, or any number of items)
                                var luckItems = __instance.GetComponentsInChildren<UIBornLuckItem>(true);
                                if (luckItems != null && luckItems.Length > 0)
                                {
                                    foreach (var luckItem in luckItems)
                                    {
                                        if (luckItem == null || luckItem.item == null || luckItem.item.id <= 0) continue;
                                        bool isSelected = false;
                                        var tgl = luckItem.GetComponent<UnityEngine.UI.Toggle>() ?? luckItem.GetComponentInParent<UnityEngine.UI.Toggle>() ?? luckItem.GetComponentInChildren<UnityEngine.UI.Toggle>(true);
                                        if (tgl != null && tgl.isOn) isSelected = true;
                                        else if (luckItem.btnFateEffect != null && luckItem.btnFateEffect.activeSelf) isSelected = true;
                                        else if (luckItem.lastOrder > 0) isSelected = true;

                                        if (isSelected && !selectedDestinyIds.Contains(luckItem.item.id))
                                        {
                                            selectedDestinyIds.Add(luckItem.item.id);
                                        }
                                    }
                                }

                                // Source 2: Check UICreatePlayerProperty lastClickBorn toggles
                                var propUI = __instance.GetComponentInChildren<UICreatePlayerProperty>(true);
                                if (propUI != null && propUI.lastClickBorn != null)
                                {
                                    foreach (var tgl in propUI.lastClickBorn)
                                    {
                                        if (tgl == null) continue;
                                        var luckItem = tgl.GetComponent<UIBornLuckItem>() ?? tgl.GetComponentInParent<UIBornLuckItem>() ?? tgl.GetComponentInChildren<UIBornLuckItem>(true);
                                        if (luckItem != null && luckItem.item != null && luckItem.item.id > 0)
                                        {
                                            if (!selectedDestinyIds.Contains(luckItem.item.id))
                                                selectedDestinyIds.Add(luckItem.item.id);
                                        }
                                    }
                                }

                                // Source 3: Check playerData.unitData.propertyData.bornLuck (modified by 9-destiny mods)
                                if (__instance.playerData?.unitData?.propertyData?.bornLuck != null)
                                {
                                    foreach (var ld in __instance.playerData.unitData.propertyData.bornLuck)
                                    {
                                        if (ld != null && ld.id > 0 && !selectedDestinyIds.Contains(ld.id))
                                            selectedDestinyIds.Add(ld.id);
                                    }
                                }

                                // Source 4: Fallback to existing NPC destinies if none selected in UI
                                if (selectedDestinyIds.Count == 0)
                                {
                                    var existingIds = Patch_UICreatePlayer_Property.Patch_UICreatePlayerProperty_UpdatePropertyUI.GetUnitDestinyIds(npc);
                                    if (existingIds != null && existingIds.Count > 0)
                                    {
                                        selectedDestinyIds.AddRange(existingIds);
                                        ModLogger.Info("[Save]", $"Preserved {selectedDestinyIds.Count} existing NPC destinies.");
                                    }
                                }

                                ModLogger.Info("[Save]", $"Extracted {selectedDestinyIds.Count} selected destinies: [{string.Join(", ", selectedDestinyIds)}]");

                                if (selectedDestinyIds.Count > 0 && npc.data?.unitData?.propertyData != null)
                                {
                                    SyncDestiniesToUnit(npc, selectedDestinyIds);
                                    ModLogger.Info("[Save]", $"Successfully synced NPC Destinies: [{string.Join(", ", selectedDestinyIds)}]");
                                }
                            }
                            catch (Exception destEx)
                            {
                                ModLogger.Warn("[Save]", "Error saving modified destinies: " + destEx.Message);
                            }

                            // Refresh combat and map models so battle avatar updates immediately
                            RefreshUnitCombatAndMapModel(npc);
                            RefreshMapAndTownNpcPortraits(npc);

                            ModLogger.Info("[Save]", "Successfully updated portrait model, combat model, destinies, and name for NPC: " + npcId);
                        }

                        ModMain.EditingNpcId = null;

                        try
                        {
                            if (g.ui != null)
                            {
                                g.ui.CloseUI(UIType.CreatePlayer, true);
                            }
                            else
                            {
                                __instance.gameObject.SetActive(false);
                                UnityEngine.Object.Destroy(__instance.gameObject);
                            }
                        }
                        catch (Exception closeEx)
                        {
                            ModLogger.Warn("[Save]", "Exception closing UI: " + closeEx.Message);
                            try
                            {
                                __instance.gameObject.SetActive(false);
                                UnityEngine.Object.Destroy(__instance.gameObject);
                            }
                            catch { }
                        }

                        if (npc != null && g.world != null && g.world.playerUnit != null)
                        {
                            bool isPlayer = (npc.data != null && npc.data.unitData != null && npc.data.unitData.unitID == g.world.playerUnit.data.unitData.unitID);
                            if (!isPlayer)
                            {
                                var ui = g.ui.OpenUI<UINPCInfo>(UIType.NPCInfo);
                                if (ui != null)
                                {
                                    ui.InitData(npc, false);
                                }
                            }
                            else
                            {
                                try
                                {
                                    var mapMain = g.ui.GetUI<UIMapMain>(UIType.MapMain);
                                    if (mapMain != null && mapMain.uiPlayerInfo != null)
                                    {
                                        try { mapMain.uiPlayerInfo.ResetUnitModel(); } catch { }
                                        try { mapMain.uiPlayerInfo.OnPlayerEquipCloth(); } catch { }
                                        try { mapMain.uiPlayerInfo.UpdateUI(); } catch { }
                                        try { mapMain.uiPlayerInfo.UpdatePlayerInfo(); } catch { }
                                        try { mapMain.uiPlayerInfo.CorUpdateInfo(); } catch { }
                                        ModLogger.Info("[Save]", "Successfully updated mapMain.uiPlayerInfo for player.");
                                    }
                                }
                                catch (Exception pEx)
                                {
                                    ModLogger.Warn("[Save]", "Error refreshing player bottom-left info: " + pEx.Message);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ModLogger.Error("[Save]", "Error saving NPC face", e);
                        ModMain.EditingNpcId = null;
                    }
                    finally
                    {
                        _isSaving = false;
                    }

                    return false;
                }

                return true;
            }

            private static void SetReflectedFieldOrProp(object target, string name, object value)
            {
                if (target == null || string.IsNullOrEmpty(name) || value == null) return;
                try
                {
                    var type = target.GetType();
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var f = type.GetField(name, flags);
                    if (f != null && f.FieldType == value.GetType())
                    {
                        f.SetValue(target, value);
                        return;
                    }
                    var p = type.GetProperty(name, flags);
                    if (p != null && p.CanWrite && p.PropertyType == value.GetType())
                    {
                        p.SetValue(target, value);
                        return;
                    }
                }
                catch { }
            }

            private static void RefreshMapAndTownNpcPortraits(WorldUnitBase npc)
            {
                if (npc == null || npc.data == null) return;
                try
                {
                    var mapMains = UnityEngine.Object.FindObjectsOfType<UIMapMain>();
                    if (mapMains != null)
                    {
                        foreach (var mm in mapMains)
                        {
                            if (mm == null) continue;
                            try { mm.UpdateOpgroupUnitList(); } catch { }
                        }
                    }
                    ModLogger.Info("[Save]", "Refreshed map & town NPC avatar icons.");
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Save]", $"Error refreshing NPC town icons: {ex.Message}");
                }
            }

            private static void RefreshUnitCombatAndMapModel(WorldUnitBase npc)
            {
                if (npc == null || npc.data == null) return;
                try
                {
                    if (npc.data.unitData != null)
                    {
                        try { WorldUnitBase.CreateConf(npc.data.unitData); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Save]", $"Error in RefreshUnitCombatAndMapModel: {ex.Message}");
                }
            }

            private static void CopyModelData(PortraitModelData src, PortraitModelData dst)
            {
                if (src == null || dst == null) return;
                if (src.sex != 0) dst.sex = src.sex;
                dst.hat       = src.hat;
                dst.hair      = src.hair;
                dst.hairFront = src.hairFront;
                dst.head      = src.head;
                dst.eyebrows  = src.eyebrows;
                dst.eyes      = src.eyes;
                dst.nose      = src.nose;
                dst.mouth     = src.mouth;
                dst.body      = src.body;
                dst.back      = src.back;
                dst.forehead  = src.forehead;
                dst.faceFull  = src.faceFull;
                dst.faceLeft  = src.faceLeft;
                dst.faceRight = src.faceRight;
            }

            private static void SyncModelDataToUnit(WorldUnitBase npc, PortraitModelData newModel)
            {
                if (npc == null || npc.data == null || newModel == null) return;

                // 1. Dynamic runtime model
                if (npc.data.dynUnitData != null)
                {
                    if (npc.data.dynUnitData.modelData == null)
                        npc.data.dynUnitData.modelData = new PortraitModelData();
                    CopyModelData(newModel, npc.data.dynUnitData.modelData);
                }

                // 2. Persistent unitData / propertyData (saved to game save file)
                if (npc.data.unitData != null)
                {
                    var unitDataObj = npc.data.unitData;
                    TryCopyModelDataToObject(unitDataObj, newModel);

                    if (unitDataObj.propertyData != null)
                    {
                        TryCopyModelDataToObject(unitDataObj.propertyData, newModel);
                    }
                }

                // 3. Resolve official dressID for Spine combat and map models
                try
                {
                    if (newModel.body > 0 && g.conf != null && g.conf.roleDress != null)
                    {
                        var dressItem = g.conf.roleDress.GetItem(newModel.body);
                        if (dressItem != null && dressItem.dressID > 0)
                        {
                            int dressId = dressItem.dressID;
                            if (npc.data.unitData?.propertyData != null)
                            {
                                SetReflectedFieldOrProp(npc.data.unitData.propertyData, "dressID", dressId);
                                SetReflectedFieldOrProp(npc.data.unitData.propertyData, "dress", dressId);
                            }
                            if (npc.data.dynUnitData != null)
                            {
                                SetReflectedFieldOrProp(npc.data.dynUnitData, "dressID", dressId);
                                SetReflectedFieldOrProp(npc.data.dynUnitData, "dress", dressId);
                            }
                            ModLogger.Info("[Save]", $"Resolved and synced official dressID={dressId} (ConfRoleDress id={newModel.body})");
                        }
                    }
                }
                catch (Exception dEx)
                {
                    ModLogger.Warn("[Save]", "Error resolving dressID from roleDress: " + dEx.Message);
                }
            }

            private static void TryCopyModelDataToObject(object targetObj, PortraitModelData newModel)
            {
                if (targetObj == null) return;
                try
                {
                    var type = targetObj.GetType();
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                    foreach (var field in type.GetFields(flags))
                    {
                        if (field.FieldType == typeof(PortraitModelData))
                        {
                            var currentVal = field.GetValue(targetObj) as PortraitModelData;
                            if (currentVal == null)
                            {
                                currentVal = new PortraitModelData();
                                field.SetValue(targetObj, currentVal);
                            }
                            CopyModelData(newModel, currentVal);
                            ModLogger.Info("[Save]", $"Synced modelData to field '{field.Name}' on '{type.Name}'");
                        }
                    }

                    foreach (var prop in type.GetProperties(flags))
                    {
                        if (prop.PropertyType == typeof(PortraitModelData) && prop.CanRead)
                        {
                            var currentVal = prop.GetValue(targetObj) as PortraitModelData;
                            if (currentVal == null && prop.CanWrite)
                            {
                                currentVal = new PortraitModelData();
                                try { prop.SetValue(targetObj, currentVal); } catch { }
                            }
                            if (currentVal != null)
                            {
                                CopyModelData(newModel, currentVal);
                                ModLogger.Info("[Save]", $"Synced modelData to property '{prop.Name}' on '{type.Name}'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Save]", $"Error reflection syncing modelData: {ex.Message}");
                }
            }

            private static void SyncDestiniesToUnit(WorldUnitBase npc, List<int> destinyIds)
            {
                if (npc == null || npc.data == null || destinyIds == null || destinyIds.Count == 0) return;
                try
                {
                    var unitData = npc.data.unitData;
                    if (unitData == null || unitData.propertyData == null) return;
                    var prop = unitData.propertyData;

                    // 1. Direct Assignment to PropertyData.bornLuck (Native Il2CppReferenceArray)
                    var luckArray = new Il2CppReferenceArray<DataUnit.LuckData>(destinyIds.Count);
                    for (int i = 0; i < destinyIds.Count; i++)
                    {
                        var ld = new DataUnit.LuckData();
                        ld.id = destinyIds[i];
                        luckArray[i] = ld;
                    }
                    prop.bornLuck = luckArray;
                    ModLogger.Info("[Save]", $"Directly assigned prop.bornLuck array with {destinyIds.Count} items.");

                    // 2. Fallback sync to any other fields via reflection
                    var propType = prop.GetType();
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                    if (npc.data.dynUnitData != null)
                    {
                        try
                        {
                            var df = npc.data.dynUnitData.GetType().GetField("bornLuck", flags);
                            df?.SetValue(npc.data.dynUnitData, luckArray);
                        }
                        catch { }
                    }
                    foreach (var field in propType.GetFields(flags))
                    {
                        string fName = field.Name.ToLower();
                        if ((fName.Contains("bornluck") || fName.Contains("feature")) && field.Name != "bornLuck")
                        {
                            try
                            {
                                var val = field.GetValue(prop);
                                if (val is Il2CppSystem.Collections.Generic.List<DataUnit.LuckData> luckDataList)
                                {
                                    luckDataList.Clear();
                                    foreach (int id in destinyIds)
                                    {
                                        var ld = new DataUnit.LuckData();
                                        ld.id = id;
                                        luckDataList.Add(ld);
                                    }
                                }
                                else if (val is Il2CppSystem.Collections.Generic.List<int> intList)
                                {
                                    intList.Clear();
                                    foreach (int id in destinyIds) intList.Add(id);
                                }
                            }
                            catch { }
                        }
                    }

                    // 4. Sync to Runtime npc.allLuck (So NPC profile UI displays new destinies immediately)
                    if (npc.allLuck != null)
                    {
                        for (int i = npc.allLuck.Count - 1; i >= 0; i--)
                        {
                            var luck = npc.allLuck[i];
                            if (luck != null && luck.luckConf != null && luck.luckConf.type == 1)
                            {
                                try { luck.Destroy(); } catch { }
                                npc.allLuck.RemoveAt(i);
                            }
                        }

                        foreach (int id in destinyIds)
                        {
                            var conf = g.conf.roleCreateFeature?.GetItem(id);
                            if (conf != null)
                            {
                                var luckObj = new WorldUnitLuckBase();
                                var ld = new DataUnit.LuckData();
                                ld.id = id;
                                try
                                {
                                    luckObj.Init(npc, ld, null);
                                    luckObj.Create();
                                }
                                catch
                                {
                                    luckObj.unit = npc;
                                    luckObj.luckConf = conf;
                                    luckObj.luckData = ld;
                                }
                                npc.allLuck.Add(luckObj);
                            }
                        }
                        ModLogger.Info("[Save]", $"Rebuilt npc.allLuck with {destinyIds.Count} Nature destinies.");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[Save]", "Error in SyncDestiniesToUnit: " + ex.Message);
                }
            }
        }
    }
}
