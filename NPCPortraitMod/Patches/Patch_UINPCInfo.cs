using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace NPCPortraitMod.Patches
{
    /// <summary>
    /// Harmony patch for UINPCInfo to inject the "Customize" button into NPC profile UI.
    /// </summary>
    [HarmonyPatch(typeof(UINPCInfo), "InitData")]
    public class Patch_UINPCInfo
    {
        #region Harmony Patch Entry Point

        public static void Postfix(UINPCInfo __instance)
        {
            try
            {
                var unit = __instance.unit;
                if (unit == null || unit.data == null || unit.data.unitData == null) return;

                LogNpcFaceData(unit);

                // Prevent duplicate button creation
                if (HasExistingButton(__instance)) return;

                // Find anchor button to duplicate style & position
                Button targetSrcBtn = FindTargetActionButton(__instance);
                if (targetSrcBtn == null) return;

                // Instantiate and configure Customize button
                CreateCustomizeButton(__instance, targetSrcBtn, unit.data.unitData.unitID);
            }
            catch (Exception e)
            {
                ModLogger.Error("[Init]", "Error in UINPCInfo patch", e);
            }
        }

        #endregion

        #region Helper Methods

        private static void LogNpcFaceData(WorldUnitBase unit)
        {
            try
            {
                var unitData = unit.data.unitData;
                var rawModel = (unit.data.dynUnitData != null) ? unit.data.dynUnitData.modelData : null;

                string rawStr = rawModel != null 
                    ? $"hat={rawModel.hat}, hair={rawModel.hair}, hairFront={rawModel.hairFront}, head={rawModel.head}, eyebrows={rawModel.eyebrows}, eyes={rawModel.eyes}, nose={rawModel.nose}, mouth={rawModel.mouth}, body={rawModel.body}, back={rawModel.back}, forehead={rawModel.forehead}, faceFull={rawModel.faceFull}, faceLeft={rawModel.faceLeft}, faceRight={rawModel.faceRight}"
                    : "NULL";

                ModLogger.Debug("[Face-Match]", $"NPC ID: {unitData.unitID}, Sex: {(rawModel != null ? rawModel.sex : 0)}");
                ModLogger.Debug("[Face-Match]", $"Raw dynUnitData.modelData: {rawStr}");
            }
            catch (Exception logEx)
            {
                ModLogger.Warn("[Init]", "Debug log error in UINPCInfo: " + logEx.Message);
            }
        }

        private static bool HasExistingButton(UINPCInfo instance)
        {
            foreach (var tr in instance.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == "BtnChangePortrait")
                {
                    return true;
                }
            }
            return false;
        }

        private static Button FindTargetActionButton(UINPCInfo instance)
        {
            // Priority 1: Recruitment button
            foreach (var b in instance.GetComponentsInChildren<Button>(true))
            {
                if (b == instance.btnClose) continue;
                Text t = b.GetComponentInChildren<Text>();
                if (t != null && !string.IsNullOrEmpty(t.text))
                {
                    string txt = t.text.ToLower();
                    if (txt.Contains("recruitment") || txt.Contains("招募"))
                    {
                        return b;
                    }
                }
            }

            // Priority 2: Action buttons on right panel (Mark / Theft / Attack)
            foreach (var b in instance.GetComponentsInChildren<Button>(true))
            {
                if (b == instance.btnClose) continue;
                Text t = b.GetComponentInChildren<Text>();
                if (t != null && !string.IsNullOrEmpty(t.text))
                {
                    string txt = t.text.ToLower();
                    if (txt.Contains("mark") || txt.Contains("theft") || txt.Contains("attack"))
                    {
                        return b;
                    }
                }
            }

            return null;
        }

        private static void CreateCustomizeButton(UINPCInfo instance, Button targetSrcBtn, string unitId)
        {
            // Clone anchor button and attach directly to window root to prevent layout group auto-cloning
            GameObject newBtnObj = UnityEngine.Object.Instantiate(targetSrcBtn.gameObject, instance.transform);
            newBtnObj.name = "BtnChangePortrait";
            newBtnObj.SetActive(true);

            // Position button directly above targetSrcBtn in World Space
            var rect = targetSrcBtn.GetComponent<RectTransform>();
            float offsetY = (rect != null && rect.rect.height > 0) 
                ? rect.rect.height * targetSrcBtn.transform.lossyScale.y * 1.15f 
                : 0.08f;
            newBtnObj.transform.position = targetSrcBtn.transform.position + new Vector3(0, offsetY, 0);

            // Set label text to "Customize" per AGENT.md UI language rules
            Text btnText = newBtnObj.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = "Customize";
            }

            // Attach onClick click handler
            Button btn = newBtnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(new Action(() =>
                {
                    ModMain.EditingNpcId = unitId;

                    // Close NPC info window
                    g.ui.CloseUI(UIType.NPCInfo);

                    // Open character customization window
                    ModLogger.Info("[UI-Open]", "Opening Create Player for NPC: " + unitId);
                    var ui = g.ui.OpenUI<UICreatePlayer>(UIType.CreatePlayer);
                    if (ui != null)
                    {
                        ui.InitData(0, GameLevelType.Common, 0);
                    }
                }));
            }
        }

        #endregion
    }
}
