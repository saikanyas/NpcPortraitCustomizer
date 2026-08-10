using MelonLoader;
using System;
using UnityEngine;

[assembly: MelonInfo(typeof(NPCPortraitMod.ModMain), "NPC Portrait Customizer", "1.0.0", "Author")]
[assembly: MelonGame("guigugame", "guigubahuang")]

namespace NPCPortraitMod
{
    public class ModMain : MelonMod
    {
        // ใช้เก็บ ID ของ NPC ที่กำลังถูกแก้ไขหน้าตา
        // ถ้าเป็น null แสดงว่าไม่ได้กำลังแก้ NPC (สร้างตัวละครใหม่ตามปกติ)
        public static string EditingNpcId = null;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("NPC Portrait Customizer Mod Loaded!");
            // Harmony patches are applied automatically by MelonLoader 0.5.7
        }
    }
}
