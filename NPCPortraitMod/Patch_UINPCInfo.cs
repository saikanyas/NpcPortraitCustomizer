using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace NPCPortraitMod
{
    // สมมติว่า UINPCInfo มีเมธอดชื่อ InitData ที่รับ Data ของ NPC
    // ถ้าชื่อเมธอดต่างออกไป (เช่น Init) ให้เปลี่ยนชื่อตรง "InitData"
    [HarmonyPatch(typeof(UINPCInfo), "InitData")]
    public class Patch_UINPCInfo
    {
        public static void Postfix(UINPCInfo __instance, object npcData) 
        {
            try
            {
                // ตรวจสอบว่าปุ่มถูกสร้างไปแล้วหรือยัง เพื่อไม่ให้สร้างซ้ำ
                Transform existingBtn = __instance.transform.Find("BtnChangePortrait");
                if (existingBtn != null) return;

                // ตัวอย่างการสร้างปุ่มโดยโคลนจากปุ่มที่มีอยู่แล้วในหน้าต่าง
                // (ถ้ามี GuiLib สามารถใช้ GuiLib.UIUtil.AddButton แทนได้เลย)
                Transform btnClose = __instance.transform.Find("Root/BtnClose"); // หาปุ่มปิดเป็นต้นแบบ
                if (btnClose != null)
                {
                    GameObject newBtnObj = UnityEngine.Object.Instantiate(btnClose.gameObject, btnClose.parent);
                    newBtnObj.name = "BtnChangePortrait";
                    
                    // ปรับตำแหน่งให้เหมาะสม (ขยับไปทางซ้ายของปุ่มปิด)
                    newBtnObj.transform.localPosition = btnClose.localPosition + new Vector3(-100, 0, 0);
                    
                    // เปลี่ยน Text ของปุ่ม
                    Text btnText = newBtnObj.GetComponentInChildren<Text>();
                    if (btnText != null) btnText.text = "เปลี่ยนหน้าตา";

                    // ลบ Event เก่าทิ้ง และเพิ่ม Event ใหม่
                    Button btn = newBtnObj.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(new Action(() => {
                        // ดึง ID ของ NPC (สมมติว่า npcData เป็นชนิด DataUnit หรือ WorldUnitBase)
                        // ให้เปลี่ยนตรงนี้ตามคลาสจริงของ Tale of Immortal
                        string npcId = npcData.GetType().GetField("id").GetValue(npcData).ToString();
                        
                        // เซ็ต ID ที่ต้องการแก้
                        ModMain.EditingNpcId = npcId;
                        
                        // ซ่อนหน้าต่าง NPC ชั่วคราว
                        __instance.gameObject.SetActive(false);
                        
                        // เปิดหน้าต่างสร้างตัวละคร
                        // UIManager หรือ g.ui.OpenUI(UIType.CreatePlayer);
                        // ถ้าเกมใช้ g.ui.OpenUI สามารถใช้ Reflection หรือโค้ดตรงได้เลย
                        MelonLoader.MelonLogger.Msg("Opening Create Player UI for NPC: " + npcId);
                    }));
                }
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("Error patching UINPCInfo: " + e.ToString());
            }
        }
    }
}
