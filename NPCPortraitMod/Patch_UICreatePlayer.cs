using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace NPCPortraitMod
{
    public class Patch_UICreatePlayer
    {
        // 1. Patch ตอนโหลดข้อมูลหน้าตา เพื่อดึงข้อมูล NPC มาแสดงถ้า EditingNpcId ไม่ใช่ null
        [HarmonyPatch(typeof(UICreatePlayer), "InitData")] // เช็คชื่อเมธอดที่ใช้เตรียมข้อมูล UI อีกที
        public static class Patch_UICreatePlayer_InitData
        {
            public static void Prefix(UICreatePlayer __instance)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    MelonLoader.MelonLogger.Msg("Loading NPC data for customization...");
                    // โค้ดสำหรับดึงข้อมูล ConfRoleFace หรือ ActorHuman ของ NPC ที่ตรงกับ ModMain.EditingNpcId
                    // มายัดใส่ตัวแปรเริ่มต้นของ UICreatePlayer
                }
            }
        }

        // 2. Patch ตอนกดปุ่มบันทึก เพื่อเซฟทับ NPC แทนที่จะสร้างผู้เล่นใหม่
        [HarmonyPatch(typeof(UICreatePlayer), "OnBtnCreate")] // สมมติว่าชื่อเมธอดคือ OnBtnCreate หรือ OnClickConfirm
        public static class Patch_UICreatePlayer_OnBtnCreate
        {
            public static bool Prefix(UICreatePlayer __instance)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    try
                    {
                        MelonLoader.MelonLogger.Msg("Saving new face to NPC " + ModMain.EditingNpcId);
                        
                        // โค้ดดึงข้อมูล Face ปัจจุบันที่ปรับแต่งเสร็จแล้วจาก __instance 
                        // แล้วนำไปเขียนทับ (Overwrite) ข้อมูล NPC ที่ตรงกับ EditingNpcId

                        // ปิดหน้าต่างนี้
                        __instance.gameObject.SetActive(false); // หรือ g.ui.CloseUI(...)
                        
                        // รีเซ็ตค่า
                        ModMain.EditingNpcId = null;

                        // เปิดหน้า UINPCInfo กลับมาใหม่ และสั่ง Refresh
                        // g.ui.OpenUI<UINPCInfo>(...);
                    }
                    catch (Exception e)
                    {
                        MelonLoader.MelonLogger.Error("Error saving NPC face: " + e.ToString());
                    }

                    // Return false เพื่อขัดจังหวะ ไม่ให้รันโค้ด OnBtnCreate ดั้งเดิมของเกม (ไม่ให้เข้าโหมด New Game)
                    return false;
                }

                // ถ้าไม่ได้แก้ NPC (EditingNpcId == null) ก็ปล่อยให้ทำงานปกติ
                return true; 
            }
        }
    }
}
