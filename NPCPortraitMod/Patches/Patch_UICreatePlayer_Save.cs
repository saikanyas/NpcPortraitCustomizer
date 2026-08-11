using HarmonyLib;
using UnityEngine;
using System;

namespace NPCPortraitMod.Patches
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

                        var npc = g.world.unit.GetUnit(npcId);
                        
                        if (npc != null && npc.data != null && npc.data.dynUnitData != null && npc.data.dynUnitData.modelData != null && __instance.uiFacade != null && __instance.uiFacade.portraitModel != null && __instance.uiFacade.portraitModel.data != null)
                        {
                            var newModel = __instance.uiFacade.portraitModel.data;
                            var targetModel = npc.data.dynUnitData.modelData;

                            targetModel.hat = newModel.hat;
                            targetModel.hair = newModel.hair;
                            targetModel.hairFront = newModel.hairFront;
                            targetModel.head = newModel.head;
                            targetModel.eyebrows = newModel.eyebrows;
                            targetModel.eyes = newModel.eyes;
                            targetModel.nose = newModel.nose;
                            targetModel.mouth = newModel.mouth;
                            targetModel.body = newModel.body;
                            targetModel.back = newModel.back;
                            targetModel.forehead = newModel.forehead;
                            targetModel.faceFull = newModel.faceFull;
                            targetModel.faceLeft = newModel.faceLeft;
                            targetModel.faceRight = newModel.faceRight;

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

                            if (npc.data.unitData != null)
                            {
                                WorldUnitBase.CreateConf(npc.data.unitData);
                            }

                            ModLogger.Info("[Save]", "Successfully updated portrait model and name for NPC: " + npcId);
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
        }
    }
}
