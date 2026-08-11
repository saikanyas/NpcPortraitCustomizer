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

                            if (__instance.playerData != null && __instance.playerData.unitData != null && __instance.playerData.unitData.propertyData != null)
                            {
                                var newName = __instance.playerData.unitData.propertyData.name;
                                npc.data.unitData.propertyData.name = newName;
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
