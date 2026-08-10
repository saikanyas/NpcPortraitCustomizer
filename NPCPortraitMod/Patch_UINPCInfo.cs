using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace NPCPortraitMod
{
    // RULE COMPLIANCE: 
    // 1. Postfix(UINPCInfo __instance) only - avoids InvalidCastException
    // 2. Checks entire UINPCInfo hierarchy to guarantee EXACTLY ONE button is created above Recruitment
    [HarmonyPatch(typeof(UINPCInfo), "InitData")]
    public class Patch_UINPCInfo
    {
        public static void Postfix(UINPCInfo __instance)
        {
            try
            {
                var unit = __instance.unit;
                if (unit == null || unit.data == null || unit.data.unitData == null) return;

                // Log NPC Face Data for debugging
                try
                {
                    var unitData = unit.data.unitData;
                    var rawModel = (unit.data.dynUnitData != null) ? unit.data.dynUnitData.modelData : null;

                    string rawStr = rawModel != null 
                        ? $"hat={rawModel.hat}, hair={rawModel.hair}, hairFront={rawModel.hairFront}, head={rawModel.head}, eyebrows={rawModel.eyebrows}, eyes={rawModel.eyes}, nose={rawModel.nose}, mouth={rawModel.mouth}, body={rawModel.body}, back={rawModel.back}, forehead={rawModel.forehead}, faceFull={rawModel.faceFull}, faceLeft={rawModel.faceLeft}, faceRight={rawModel.faceRight}"
                        : "NULL";

                    MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-INFO] NPC ID: {unitData.unitID}, Sex: {(rawModel != null ? rawModel.sex : 0)}");
                    MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-INFO] Raw dynUnitData.modelData: {rawStr}");
                }
                catch (Exception logEx)
                {
                    MelonLoader.MelonLogger.Warning("[NPCPortraitMod] Debug log error in UINPCInfo: " + logEx.Message);
                }

                // ตรวจสอบว่าปุ่ม BtnChangePortrait ถูกสร้างไปแล้วหรือยังในทั่วทั้งหน้าต่าง UINPCInfo (ป้องกันปุ่มซ้อน)
                foreach (var tr in __instance.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name == "BtnChangePortrait")
                    {
                        return; // มีปุ่มแล้ว ยกเลิกการสร้างซ้ำทันที
                    }
                }

                // ค้นหาปุ่ม Recruitment เฉพาะเจาะจงเป็นอันดับแรก
                Button targetSrcBtn = null;
                foreach (var b in __instance.GetComponentsInChildren<Button>(true))
                {
                    if (b == __instance.btnClose) continue;
                    Text t = b.GetComponentInChildren<Text>();
                    if (t != null && !string.IsNullOrEmpty(t.text))
                    {
                        string txt = t.text.ToLower();
                        if (txt.Contains("recruitment") || txt.Contains("招募"))
                        {
                            targetSrcBtn = b;
                            break;
                        }
                    }
                }

                // ถ้าไม่เจอ Recruitment ให้ค้นหาปุ่ม Mark / Theft / Attack ฝั่งขวา
                if (targetSrcBtn == null)
                {
                    foreach (var b in __instance.GetComponentsInChildren<Button>(true))
                    {
                        if (b == __instance.btnClose) continue;
                        Text t = b.GetComponentInChildren<Text>();
                        if (t != null && !string.IsNullOrEmpty(t.text))
                        {
                            string txt = t.text.ToLower();
                            if (txt.Contains("mark") || txt.Contains("theft") || txt.Contains("attack"))
                            {
                                targetSrcBtn = b;
                                break;
                            }
                        }
                    }
                }

                if (targetSrcBtn == null) return;

                // โคลนปุ่มแต่ยึดกับ __instance.transform (หน้าต่างหลัก) 
                // สำคัญมาก: ป้องกันไม่ให้ปุ่มไปอยู่ใน Container/LayoutGroup เดียวกับปุ่มอื่นๆ ซึ่งจะทำให้เกมก๊อปปี้ปุ่มไปโผล่ซ้อนทุกปุ่ม
                GameObject newBtnObj = UnityEngine.Object.Instantiate(targetSrcBtn.gameObject, __instance.transform);
                newBtnObj.name = "BtnChangePortrait";
                newBtnObj.SetActive(true);

                // ปรับตำแหน่งตามพิกัด World Space เหนือปุ่ม Recruitment
                var rect = targetSrcBtn.GetComponent<RectTransform>();
                float offsetY = (rect != null && rect.rect.height > 0) ? rect.rect.height * targetSrcBtn.transform.lossyScale.y * 1.15f : 0.08f;
                newBtnObj.transform.position = targetSrcBtn.transform.position + new Vector3(0, offsetY, 0);

                // เปลี่ยนข้อความปุ่มเป็น "Customize" ตามกฎ AGENT.md
                Text btnText = newBtnObj.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = "Customize";
                }

                Button btn = newBtnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    var unitId = unit.data.unitData.unitID;
                    btn.onClick.AddListener(new Action(() =>
                    {
                        ModMain.EditingNpcId = unitId;

                        // ปิดหน้าต่างโปรไฟล์ NPC ผ่าน UIMgr
                        g.ui.CloseUI(UIType.NPCInfo);

                        // เปิดหน้าต่างสร้างตัวละคร (UICreatePlayer) พร้อมส่ง InitData เพื่อตั้งค่าเริ่มต้น
                        MelonLoader.MelonLogger.Msg("[NPCPortraitMod] Opening Create Player for NPC: " + unitId);
                        var ui = g.ui.OpenUI<UICreatePlayer>(UIType.CreatePlayer);
                        if (ui != null)
                        {
                            ui.InitData(0, GameLevelType.Common, 0);
                        }
                    }));
                }
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[NPCPortraitMod] Error in UINPCInfo patch: " + e);
            }
        }
    }
}
