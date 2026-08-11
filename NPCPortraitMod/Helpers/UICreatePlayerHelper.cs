using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCPortraitMod.Helpers
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
                        ModLogger.Debug("[Face-Match]", $"Category '{categoryName}' (TargetId: {targetDressId}) -> MATCHED at index {i}: v.dressID={v.dressID}, v.id={v.id}, v.name='{v.name}'");
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
                    ModLogger.Debug("[Face-Match]", $"Appended missing item '{categoryName}' (ID: {targetDressId}) at index {newIndex}");
                    return true;
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int limit = Math.Min(item.values.Count, 10);
                for (int k = 0; k < limit; k++)
                {
                    var v = item.values[k];
                    if (v != null) sb.Append($"[{k}:dID={v.dressID},id={v.id}] ");
                }
                ModLogger.Warn("[Face-Match]", $"Category '{categoryName}' (TargetId: {targetDressId}) -> NOT FOUND in {item.values.Count} options. Forced to default index {defaultIndex}. First {limit}: {sb}");
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

                ModLogger.Info("[UI-Open]", $"Opening Customize for NPC ID: {npcId}, Sex: {npcModel.sex}");
                ModLogger.Debug("[Face-Match]", $"Target NPC raw IDs: hat={npcModel.hat}, hair={npcModel.hair}, hairFront={npcModel.hairFront}, head={npcModel.head}, eyebrows={npcModel.eyebrows}, eyes={npcModel.eyes}, nose={npcModel.nose}, mouth={npcModel.mouth}, body={npcModel.body}, back={npcModel.back}, forehead={npcModel.forehead}, faceFull={npcModel.faceFull}, faceLeft={npcModel.faceLeft}, faceRight={npcModel.faceRight}");

                // Step 1: Hide Stats tab, match sex toggle, rename Save button & create Exit button
                if (ui != null)
                {
                    SetupCustomizeUIButtonsAndToggles(ui, npcModel.sex);

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
                                pp.age    = np.age;
                                pp.life   = np.life;
                                pp.beauty = np.beauty;
                                // Note: do NOT copy pp.name = np.name (name is ID-based; Update loop handles display)
                                if (pp.bornLuck != null && pp.bornLuck.Length >= 3)
                                {
                                    if (np.inTrait  != 0) { pp.inTrait  = np.inTrait;  pp.bornLuck[0].id = np.inTrait;  }
                                    if (np.outTrait1 != 0) { pp.outTrait1 = np.outTrait1; pp.bornLuck[1].id = np.outTrait1; }
                                    if (np.outTrait2 != 0) { pp.outTrait2 = np.outTrait2; pp.bornLuck[2].id = np.outTrait2; }
                                }
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

                int sliderIndex = 0;
                foreach (var dressItems in dressItemsList)
                {
                    bool isMaleList = (facade.manDressItems != null && dressItems == facade.manDressItems);
                    bool isFemaleList = (facade.womanDressItems != null && dressItems == facade.womanDressItems);

                    foreach (var item in dressItems)
                    {
                        if (item == null || item.values == null || item.values.Count == 0) 
                        {
                            sliderIndex++;
                            continue;
                        }
                        
                        // Only update items matching NPC sex
                        if ((npcModel.sex == 1 && isMaleList) || (npcModel.sex == 2 && isFemaleList) || (npcModel.sex != 1 && npcModel.sex != 2))
                        {
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
                                else ModLogger.Info("[Face-Match]", $"Unknown type '{type}' at sliderIndex={sliderIndex}");

                                MatchAndSetItem(item, targetId, type);
                            }
                        }

                        sliderIndex++;
                    }
                }

                try { facade.UpdateHandleGroup(); } catch { }
                try { facade.OnDressChanged(); } catch { }
                try { facade.UpdateModelData(); } catch (Exception ex) { ModLogger.Warn("[Face-Match]", "UpdateModelData error: " + ex.Message); }
                try { facade.UpdateFacadeUI(); } catch (Exception ex) { ModLogger.Warn("[Face-Match]", "UpdateFacadeUI error: " + ex.Message); }

                // Step 3: Force write NPC IDs into portraitModel.data LAST so UpdateFacadeUI cannot overwrite
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
                    if (npcSex == 1 && (txtVal.Contains("male") || txtVal.Contains("男")) && !txtVal.Contains("female") && !txtVal.Contains("女"))
                    {
                        try { t.isOn = true; } catch (Exception ex) { ModLogger.Warn("[Face-Match]", "Toggle male error: " + ex.Message); }
                    }
                    else if (npcSex == 2 && (txtVal.Contains("female") || txtVal.Contains("女")))
                    {
                        try { t.isOn = true; } catch (Exception ex) { ModLogger.Warn("[Face-Match]", "Toggle female error: " + ex.Message); }
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

                // Position exit button to the left of Save button
                Vector3 savePos = saveBtn.transform.localPosition;
                exitObj.transform.localPosition = new Vector3(savePos.x - 160f, savePos.y, savePos.z);
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

        #endregion
    }
}
