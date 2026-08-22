using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Reflection;

namespace NPCCustomizer.Patches
{
    /// <summary>
    /// Harmony patch for UINPCInfo to inject the "Customize" button into NPC profile UI
    /// and extract current Race and Realm text values.
    /// </summary>
    [HarmonyPatch(typeof(UINPCInfo), "InitData")]
    public class Patch_UINPCInfo
    {
        public static string CurrentNpcRaceText = null;
        public static string CurrentNpcRealmText = null;

        // Cached reflection field — resolved once, reused every call
        private static FieldInfo _roleRaceField = null;

        /// <summary>Exposes cached FieldInfo for roleRace so helpers can reuse it without re-calling GetField.</summary>
        public static FieldInfo GetRoleRaceField(object propInstance)
        {
            if (_roleRaceField == null && propInstance != null)
                _roleRaceField = propInstance.GetType().GetField("roleRace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return _roleRaceField;
        }


        #region Harmony Patch Entry Point

        public static void Postfix(UINPCInfo __instance)
        {
            try
            {
                var unit = __instance.unit;
                if (unit == null || unit.data == null || unit.data.unitData == null) return;

                ModLogger.Debug("[Face-Match]", $"NPC ID: {unit.data.unitData.unitID}");

                ExtractRaceAndRealm(unit);

                Button targetSrcBtn = FindTargetActionButton(__instance);
                if (targetSrcBtn == null) return;

                CreateOrUpdateCustomizeButton(__instance, targetSrcBtn, unit.data.unitData.unitID);
            }
            catch (Exception e)
            {
                ModLogger.Error("[Init]", "Error in UINPCInfo patch", e);
            }
        }

        #endregion

        #region Helper Methods

        private static void ExtractRaceAndRealm(WorldUnitBase unit)
        {
            CurrentNpcRaceText = null;
            CurrentNpcRealmText = null;

            if (unit.data?.unitData?.propertyData == null) return;

            var prop = unit.data.unitData.propertyData;

            // Race via cached reflection
            try
            {
                if (_roleRaceField == null)
                    _roleRaceField = prop.GetType().GetField("roleRace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (_roleRaceField != null)
                {
                    int raceID = Convert.ToInt32(_roleRaceField.GetValue(prop));
                    if (raceID > 0 && g.conf?.roleRace != null)
                    {
                        var raceItem = g.conf.roleRace.GetItem(raceID);
                        if (raceItem != null && !string.IsNullOrEmpty(raceItem.race))
                            CurrentNpcRaceText = GameTool.LS(raceItem.race);
                    }
                }
            }
            catch { }

            // Realm via grade config
            try
            {
                var gradeItem = g.conf?.roleGrade?.GetItem(prop.gradeID);
                if (gradeItem != null)
                {
                    string gName = (GameTool.LS(gradeItem.gradeName) ?? "").Trim();
                    string pName = (GameTool.LS(gradeItem.phaseName) ?? "").Trim();
                    CurrentNpcRealmText = string.IsNullOrEmpty(pName) ? gName : gName + " " + pName;
                }
            }
            catch { }

            ModLogger.Debug("[RaceRealm]", $"Extracted -> Race: '{CurrentNpcRaceText}', Realm: '{CurrentNpcRealmText}'");
        }

        private static Button FindTargetActionButton(UINPCInfo instance)
        {
            if (instance == null) return null;

            var allBtns = instance.GetComponentsInChildren<Button>(true);
            if (allBtns == null || allBtns.Length == 0) return null;

            // Find the topmost Recruitment or Mark button by text label (most reliable)
            Button targetBtn = null;
            float maxY = -99999f;

            foreach (var b in allBtns)
            {
                if (b == null || b == instance.btnClose) continue;
                if (b.name != null && b.name.StartsWith("BtnChangePortrait")) continue;

                Text t = b.GetComponentInChildren<Text>();
                if (t == null || string.IsNullOrEmpty(t.text)) continue;

                string txt = t.text.ToLower();
                if (txt.Contains("recruitment") || txt.Contains("mark") ||
                    txt.Contains("招募")         || txt.Contains("标记"))
                {
                    float y = b.transform.position.y;
                    if (y > maxY) { maxY = y; targetBtn = b; }
                }
            }

            return targetBtn;
        }

        private static void CreateOrUpdateCustomizeButton(UINPCInfo instance, Button targetSrcBtn, string unitId)
        {
            Transform existingTr = instance.transform.Find("BtnChangePortrait");
            GameObject newBtnObj;

            if (existingTr != null)
            {
                newBtnObj = existingTr.gameObject;
            }
            else
            {
                newBtnObj = UnityEngine.Object.Instantiate(targetSrcBtn.gameObject, instance.transform);
                newBtnObj.name = "BtnChangePortrait";
            }

            newBtnObj.SetActive(true);

            var srcRect = targetSrcBtn.GetComponent<RectTransform>();
            float btnH = (srcRect != null && srcRect.rect.height > 0) ? srcRect.rect.height : 36f;
            Vector3 localPos = instance.transform.InverseTransformPoint(targetSrcBtn.transform.position);

            newBtnObj.transform.localPosition = new Vector3(localPos.x, localPos.y + btnH + 6f, localPos.z);
            newBtnObj.transform.localScale = targetSrcBtn.transform.localScale;

            Text btnText = newBtnObj.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = "Customize";

            Button btn = newBtnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = true;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(new Action(() =>
                {
                    ModMain.EditingNpcId = unitId;
                    Patch_UICreatePlayer_Property.Patch_UICreatePlayer_Update.LastInitializedNpcId = null;
                    g.ui.CloseUI(UIType.NPCInfo);

                    ModLogger.Info("[UI-Open]", "Opening Create Player for NPC: " + unitId);
                    var ui = g.ui.OpenUI<UICreatePlayer>(UIType.CreatePlayer);
                    if (ui != null) ui.InitData(0, GameLevelType.Common, 0);
                }));
            }
        }

        #endregion
    }
}
