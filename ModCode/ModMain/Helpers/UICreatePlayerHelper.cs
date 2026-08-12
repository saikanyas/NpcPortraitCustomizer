using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NPCPortraitCustomizer.Helpers
{
    /// <summary>
    /// Helper functions for character customization UI matching and manipulation.
    /// </summary>
    public static class UICreatePlayerHelper
    {
        #region Facade Item Matching

        public static bool MatchAndSetItem(UICreatePlayerFacade.FacadeItemData item, int targetDressId, string categoryName)
        {
            if (item == null || item.values == null || item.values.Count == 0) return false;

            // 1. Try to find the exact targetDressId
            if (targetDressId > 0)
            {
                for (int i = 0; i < item.values.Count; i++)
                {
                    var v = item.values[i];
                    if (v != null && (v.dressID == targetDressId || v.id == targetDressId))
                    {
                        item.index = i;
                        try { item.SetValue(v.dressID); } catch { }
                        try { item.SetValueInID(v.id); } catch { }
                        ModLogger.Info("[Face-Match]", $"Category '{categoryName}' (TargetId: {targetDressId}) -> MATCHED at index {i}: v.dressID={v.dressID}, v.id={v.id}, v.name='{v.name}'");
                        return true;
                    }
                }
            }

            // 2. Fallback to default/None item if target ID was not found or is 0
            int defaultIndex = 0;
            for (int i = 0; i < item.values.Count; i++)
            {
                var v = item.values[i];
                if (v != null && (v.id == 0 || v.dressID == 0))
                {
                    defaultIndex = i;
                    break;
                }
            }

            item.index = defaultIndex;
            var defV = item.values[defaultIndex];
            if (defV != null)
            {
                try { item.SetValue(defV.dressID); } catch { }
                try { item.SetValueInID(defV.id); } catch { }
            }
            
            if (targetDressId > 0)
            {
                // Attempt to fetch missing item definition from game configuration
                ConfRoleDressItem addVal = null;
                try { addVal = g.conf.roleDress.GetItem(targetDressId); } catch { }

                if (addVal == null)
                {
                    try
                    {
                        addVal = new ConfRoleDressItem();
                        addVal.id = targetDressId;
                        addVal.dressID = targetDressId;
                    }
                    catch { }
                }

                if (addVal != null)
                {
                    item.values.Add(addVal);
                    int newIndex = item.values.Count - 1;
                    item.index = newIndex;
                    try { item.SetValue(addVal.dressID); } catch { }
                    try { item.SetValueInID(addVal.id); } catch { }
                    ModLogger.Info("[Face-Match]", $"Appended missing item '{categoryName}' (ID: {targetDressId}) at index {newIndex}");
                    return true;
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int limit = Math.Min(item.values.Count, 10);
                for (int k = 0; k < limit; k++)
                {
                    var v = item.values[k];
                    sb.Append($"[{k}]: id={v.id}, dressID={v.dressID}, name='{v.name}' | ");
                }
                ModLogger.Info("[Face-Match]", $"Category '{categoryName}' (TargetId: {targetDressId}) -> NOT FOUND in {item.values.Count} options. Forced to default index {defaultIndex}. First {limit}: {sb}");
                return false;
            }
            else
            {
                ModLogger.Debug("[Face-Match]", $"Category '{categoryName}' targetId=0. Forced to default index {defaultIndex}");
                return true;
            }
        }

        #endregion

        #region Portrait UI Application Logic

        public static void ApplyNpcFaceToUI(UICreatePlayer ui, UICreatePlayerFacade facade)
        {
            if (string.IsNullOrEmpty(ModMain.EditingNpcId)) return;

            try
            {
                string npcId = ModMain.EditingNpcId;
                var npc = g.world.unit.GetUnit(npcId);
                if (npc == null || npc.data == null || npc.data.dynUnitData == null) return;

                var npcModel = npc.data.dynUnitData.modelData;
                if (npcModel == null) return;

                NpcFaceStateTracker.Reset(npcModel);

                ModLogger.Info("[UI-Open]", $"Opening Customize for NPC ID: {npcId}, Sex: {npcModel.sex}");
                ModLogger.Debug("[Face-Match]", $"Target NPC raw IDs: hat={npcModel.hat}, hair={npcModel.hair}, hairFront={npcModel.hairFront}, head={npcModel.head}, eyebrows={npcModel.eyebrows}, eyes={npcModel.eyes}, nose={npcModel.nose}, mouth={npcModel.mouth}, body={npcModel.body}, back={npcModel.back}, forehead={npcModel.forehead}, faceFull={npcModel.faceFull}, faceLeft={npcModel.faceLeft}, faceRight={npcModel.faceRight}");

                // Step 1: Hide Stats tab, match sex toggle, rename Save button & create Exit button
                if (ui != null)
                {
                    SetupCustomizeUIButtonsAndToggles(ui, npcModel.sex);
                    SyncRaceAndRealmToUI(facade, npc);

                    // Seed NPC property data into playerData immediately so name/traits start correct
                    try
                    {
                        if (ui.playerData != null && ui.playerData.unitData != null
                            && npc.data.unitData != null)
                        {
                            var pp = ui.playerData.unitData.propertyData;
                            var np = npc.data.unitData.propertyData;
                            if (pp != null && np != null)
                            {
                                pp.age       = np.age;
                                pp.life      = np.life;
                                pp.beauty    = np.beauty;
                                pp.inTrait   = np.inTrait;
                                pp.outTrait1 = np.outTrait1;
                                pp.outTrait2 = np.outTrait2;
                            }
                        }
                    }
                    catch (Exception ex) { ModLogger.Warn("[Face-Match]", "Seed playerData error: " + ex.Message); }
                }

                if (facade == null && ui != null)
                {
                    facade = ui.uiFacade;
                }

                if (facade == null) return;

                // Step 2: Log dressItems status and attempt matching
                var dressItemsList = new List<Il2CppSystem.Collections.Generic.List<UICreatePlayerFacade.FacadeItemData>>();
                if (facade.manDressItems != null) dressItemsList.Add(facade.manDressItems);
                if (facade.womanDressItems != null) dressItemsList.Add(facade.womanDressItems);

                ModLogger.Info("[Face-Match]", $"dressItemsList count={dressItemsList.Count}, manDressItems={(facade.manDressItems == null ? "NULL" : facade.manDressItems.Count.ToString())}, womanDressItems={(facade.womanDressItems == null ? "NULL" : facade.womanDressItems.Count.ToString())}");

                var targetDressItems = (npcModel.sex == 1) ? facade.manDressItems : facade.womanDressItems;
                if (targetDressItems != null)
                {
                    foreach (var item in targetDressItems)
                    {
                        if (item == null || item.values == null || item.values.Count == 0) continue;

                        var firstVal = item.values[0];
                        if (firstVal != null && !string.IsNullOrEmpty(firstVal.type))
                        {
                            string type = firstVal.type.ToLower().Replace("_", "");

                            int targetId = 0;
                            if (type == "maozi" || type == "hat") targetId = npcModel.hat;
                            else if (type == "toufa" || type == "hair") targetId = npcModel.hair;
                            else if (type == "toufaqian" || type == "hairfront") targetId = npcModel.hairFront;
                            else if (type == "lian" || type == "head") targetId = npcModel.head;
                            else if (type == "meimao" || type == "eyebrows") targetId = npcModel.eyebrows;
                            else if (type == "yanjing" || type == "eyes") targetId = npcModel.eyes;
                            else if (type == "bizi" || type == "nose") targetId = npcModel.nose;
                            else if (type == "zuiba" || type == "mouth") targetId = npcModel.mouth;
                            else if (type == "yifu" || type == "body" || type == "dress") targetId = npcModel.body;
                            else if (type == "houbei" || type == "back") targetId = npcModel.back;
                            else if (type == "meixin" || type == "forehead") targetId = npcModel.forehead;
                            else if (type == "facefull") targetId = npcModel.faceFull;
                            else if (type == "faceleft") targetId = npcModel.faceLeft;
                            else if (type == "faceright") targetId = npcModel.faceRight;

                            MatchAndSetItem(item, targetId, type);
                        }
                    }
                }

                try { facade.UpdateModelData(); } catch { }

                if (facade.portraitModel != null && facade.portraitModel.data != null)
                {
                    var facadeData = facade.portraitModel.data;
                    facadeData.hat = npcModel.hat;
                    facadeData.hair = npcModel.hair;
                    facadeData.hairFront = npcModel.hairFront;
                    facadeData.head = npcModel.head;
                    facadeData.eyebrows = npcModel.eyebrows;
                    facadeData.eyes = npcModel.eyes;
                    facadeData.nose = npcModel.nose;
                    facadeData.mouth = npcModel.mouth;
                    facadeData.body = npcModel.body;
                    facadeData.back = npcModel.back;
                    facadeData.forehead = npcModel.forehead;
                    facadeData.faceFull = npcModel.faceFull;
                    facadeData.faceLeft = npcModel.faceLeft;
                    facadeData.faceRight = npcModel.faceRight;

                    facade.portraitModel.data = facadeData;
                }

                try { facade.UpdateFacadeUI(); } catch { }

                NpcFaceStateTracker.FinishInit();

                var finalData = (facade.portraitModel != null) ? facade.portraitModel.data : null;
                string finalStr = finalData != null
                    ? $"hat={finalData.hat}, hair={finalData.hair}, hairFront={finalData.hairFront}, head={finalData.head}, eyebrows={finalData.eyebrows}, eyes={finalData.eyes}, nose={finalData.nose}, mouth={finalData.mouth}, body={finalData.body}, back={finalData.back}, forehead={finalData.forehead}, faceFull={finalData.faceFull}, faceLeft={finalData.faceLeft}, faceRight={finalData.faceRight}"
                    : "NULL";
                ModLogger.Debug("[Face-Match]", $"Final facade.portraitModel.data: {finalStr}");
            }
            catch (Exception e)
            {
                ModLogger.Error("[Face-Match]", "Error applying NPC face", e);
            }
        }

        public static void SetupCustomizeUIButtonsAndToggles(UICreatePlayer ui, int npcSex)
        {
            // Hide Stats tab toggles & match sex toggle to NPC
            foreach (var t in ui.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
            {
                var text = t.GetComponentInChildren<UnityEngine.UI.Text>();
                if (text != null && (text.text.ToLower().Contains("stat") || text.text.Contains("属性")))
                {
                    t.gameObject.SetActive(false);
                }

                if (text != null)
                {
                    string txtVal = text.text.ToLower();
                    if (t != null && t.group != null)
                    {
                        if (npcSex == 1 && (txtVal.Contains("male") || txtVal.Contains("男")) && !txtVal.Contains("female") && !txtVal.Contains("女"))
                        {
                            try { t.isOn = true; } catch { }
                        }
                        else if (npcSex == 2 && (txtVal.Contains("female") || txtVal.Contains("女")))
                        {
                            try { t.isOn = true; } catch { }
                        }
                    }
                }
            }

            // Find Save button label and rename to "Save"
            UnityEngine.UI.Button saveBtn = null;
            foreach (var btn in ui.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var txt = btn.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null && (txt.text.Contains("开始") || txt.text.ToLower().Contains("start") || txt.text.Contains("確定") || txt.text.Contains("Confirm") || txt.text == "Save"))
                {
                    txt.text = "Save";
                    saveBtn = btn;
                }
            }

            // Instantiate "✕ Exit" button next to Save button
            if (saveBtn != null)
            {
                Transform parent = saveBtn.transform.parent;
                Transform existingExit = parent.Find("BtnExitMod");
                GameObject exitObj = null;

                if (existingExit == null)
                {
                    exitObj = UnityEngine.Object.Instantiate(saveBtn.gameObject, parent);
                    exitObj.name = "BtnExitMod";
                }
                else
                {
                    exitObj = existingExit.gameObject;
                }

                exitObj.SetActive(true);

                var exitBtn = exitObj.GetComponent<UnityEngine.UI.Button>();
                if (exitBtn == null) exitBtn = exitObj.AddComponent<UnityEngine.UI.Button>();
                exitBtn.interactable = true;

                var exitTxt = exitObj.GetComponentInChildren<UnityEngine.UI.Text>();
                if (exitTxt != null)
                {
                    exitTxt.text = "✕ Exit";
                    exitTxt.color = Color.red;
                }

                // Position exit button to the left of Save button with clean spacing
                Vector3 savePos = saveBtn.transform.localPosition;
                exitObj.transform.localPosition = new Vector3(savePos.x - 220f, savePos.y, savePos.z);
                exitObj.transform.localScale = saveBtn.transform.localScale;

                exitBtn.onClick.RemoveAllListeners();
                exitBtn.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
                {
                    ModLogger.Debug("[UI-Open]", "Exit button clicked.");
                    ModMain.EditingNpcId = null;
                    try
                    {
                        if (g.ui != null) g.ui.CloseUI(UIType.CreatePlayer, true);
                        else UnityEngine.Object.Destroy(ui.gameObject);
                    }
                    catch
                    {
                        UnityEngine.Object.Destroy(ui.gameObject);
                    }
                }));
            }
        }

        public static void SyncRaceAndRealmToUI(UICreatePlayerFacade facade, WorldUnitBase unit)
        {
            try
            {
                string raceStr  = Patches.Patch_UINPCInfo.CurrentNpcRaceText;
                string realmStr = Patches.Patch_UINPCInfo.CurrentNpcRealmText;

                // Both already resolved — just apply
                if (string.IsNullOrEmpty(raceStr) || string.IsNullOrEmpty(realmStr))
                {
                    var prop = unit?.data?.unitData?.propertyData;
                    if (prop != null)
                    {
                        if (string.IsNullOrEmpty(raceStr))
                        {
                            try
                            {
                                // Uses cached FieldInfo from Patch_UINPCInfo
                                var field = Patches.Patch_UINPCInfo.GetRoleRaceField(prop);
                                if (field != null)
                                {
                                    int raceID = Convert.ToInt32(field.GetValue(prop));
                                    if (raceID > 0 && g.conf?.roleRace != null)
                                    {
                                        var raceItem = g.conf.roleRace.GetItem(raceID);
                                        if (raceItem != null && !string.IsNullOrEmpty(raceItem.race))
                                            raceStr = GameTool.LS(raceItem.race);
                                    }
                                }
                            }
                            catch { }
                        }

                        if (string.IsNullOrEmpty(realmStr))
                        {
                            try
                            {
                                var gradeItem = g.conf?.roleGrade?.GetItem(prop.gradeID);
                                if (gradeItem != null)
                                {
                                    string gn = (GameTool.LS(gradeItem.gradeName) ?? "").Trim();
                                    string pn = (GameTool.LS(gradeItem.phaseName) ?? "").Trim();
                                    realmStr = string.IsNullOrEmpty(pn) ? gn : gn + " " + pn;
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (facade == null) return;

                if (!string.IsNullOrEmpty(raceStr))
                {
                    try { if (facade.textRaceValue  != null && facade.textRaceValue.text  != raceStr) facade.textRaceValue.text  = raceStr; } catch { }
                    try { if (facade.textRaceValue_En != null && facade.textRaceValue_En.text != raceStr) facade.textRaceValue_En.text = raceStr; } catch { }
                }
                if (!string.IsNullOrEmpty(realmStr))
                {
                    try { if (facade.textLevelValue   != null && facade.textLevelValue.text   != realmStr) facade.textLevelValue.text   = realmStr; } catch { }
                    try { if (facade.textLevelValue_En != null && facade.textLevelValue_En.text != realmStr) facade.textLevelValue_En.text = realmStr; } catch { }
                }
            }
            catch { }
        }

        #endregion
    }

    /// <summary>
    /// Tracks which portrait features the user has explicitly modified during a customization session
    /// and preserves original NPC features for unmodified parts.
    /// </summary>
    public static class NpcFaceStateTracker
    {
        public static PortraitModelData OriginalNpcModel = null;
        public static HashSet<string> ModifiedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static bool IsInitializing = false;

        public static void Reset(PortraitModelData npcModel)
        {
            IsInitializing = true;
            OriginalNpcModel = npcModel;
            ModifiedTypes.Clear();
            ModLogger.Info("[FaceTracker]", "Reset tracker with NPC original model.");
        }

        public static void FinishInit()
        {
            IsInitializing = false;
            ModLogger.Info("[FaceTracker]", "Initialization finished. User edits will now be tracked.");
        }

        public static void MarkModified(string itemType)
        {
            if (IsInitializing || string.IsNullOrEmpty(itemType)) return;
            string t = itemType.ToLower().Replace("_", "");
            if (!ModifiedTypes.Contains(t))
            {
                ModifiedTypes.Add(t);
                ModLogger.Info("[FaceTracker]", $"User modified feature '{t}'");
            }
        }

        public static void ApplyStateToModelData(PortraitModelData currentData)
        {
            if (OriginalNpcModel == null || currentData == null) return;

            if (!ModifiedTypes.Contains("maozi") && !ModifiedTypes.Contains("hat")) currentData.hat = OriginalNpcModel.hat;
            if (!ModifiedTypes.Contains("toufa") && !ModifiedTypes.Contains("hair")) currentData.hair = OriginalNpcModel.hair;
            if (!ModifiedTypes.Contains("toufaqian") && !ModifiedTypes.Contains("hairfront")) currentData.hairFront = OriginalNpcModel.hairFront;
            if (!ModifiedTypes.Contains("lian") && !ModifiedTypes.Contains("head")) currentData.head = OriginalNpcModel.head;
            if (!ModifiedTypes.Contains("meimao") && !ModifiedTypes.Contains("eyebrows")) currentData.eyebrows = OriginalNpcModel.eyebrows;
            if (!ModifiedTypes.Contains("yanjing") && !ModifiedTypes.Contains("eyes")) currentData.eyes = OriginalNpcModel.eyes;
            if (!ModifiedTypes.Contains("bizi") && !ModifiedTypes.Contains("nose")) currentData.nose = OriginalNpcModel.nose;
            if (!ModifiedTypes.Contains("zuiba") && !ModifiedTypes.Contains("mouth")) currentData.mouth = OriginalNpcModel.mouth;
            if (!ModifiedTypes.Contains("yifu") && !ModifiedTypes.Contains("body") && !ModifiedTypes.Contains("dress")) currentData.body = OriginalNpcModel.body;
            if (!ModifiedTypes.Contains("houbei") && !ModifiedTypes.Contains("back")) currentData.back = OriginalNpcModel.back;
            if (!ModifiedTypes.Contains("meixin") && !ModifiedTypes.Contains("forehead")) currentData.forehead = OriginalNpcModel.forehead;
            if (!ModifiedTypes.Contains("facefull")) currentData.faceFull = OriginalNpcModel.faceFull;
            if (!ModifiedTypes.Contains("faceleft")) currentData.faceLeft = OriginalNpcModel.faceLeft;
            if (!ModifiedTypes.Contains("faceright")) currentData.faceRight = OriginalNpcModel.faceRight;
        }
    }
}
