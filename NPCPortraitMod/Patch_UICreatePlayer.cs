using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace NPCPortraitMod
{
    public class Patch_UICreatePlayer
    {
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
                        MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-MATCH] Category '{categoryName}' (TargetId: {targetDressId}) -> MATCHED at index {i}: v.dressID={v.dressID}, v.id={v.id}, v.name='{v.name}'");
                        return true;
                    }
                }
            }

            // 2. If targetDressId == 0 OR exact ID was not found, we MUST reset to "None" or default!
            // Look for an item with id == 0 or dressID == 0 (usually "None")
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
                // Try fetching from game config if missing
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
                    MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-ADD-CUSTOM] Appended missing item '{categoryName}' (ID: {targetDressId}) at index {newIndex}");
                    return true;
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int limit = Math.Min(item.values.Count, 10);
                for (int k = 0; k < limit; k++)
                {
                    var v = item.values[k];
                    if (v != null) sb.Append($"[{k}:dID={v.dressID},id={v.id}] ");
                }
                MelonLoader.MelonLogger.Warning($"[NPCPortraitMod][DEBUG-MISMATCH] Category '{categoryName}' (TargetId: {targetDressId}) -> NOT FOUND in {item.values.Count} options. Forced to default index {defaultIndex}. First {limit}: {sb}");
                return false;
            }
            else
            {
                MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-SLIDER] Category '{categoryName}' targetId=0. Forced to default index {defaultIndex}");
                return true;
            }
        }

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

                MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-CUSTOM] Opening Customize for NPC ID: {npcId}, Sex: {npcModel.sex}");
                MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-CUSTOM] Target NPC raw IDs: hat={npcModel.hat}, hair={npcModel.hair}, hairFront={npcModel.hairFront}, head={npcModel.head}, eyebrows={npcModel.eyebrows}, eyes={npcModel.eyes}, nose={npcModel.nose}, mouth={npcModel.mouth}, body={npcModel.body}, back={npcModel.back}, forehead={npcModel.forehead}, faceFull={npcModel.faceFull}, faceLeft={npcModel.faceLeft}, faceRight={npcModel.faceRight}");

                // 1. ซ่อนแท็บ Stats และสร้างปุ่ม Exit (กากบาท ✕ Exit) ถัดจากปุ่ม Save ที่ใช้งานกดได้ 100%
                if (ui != null)
                {
                    // ซ่อนแท็บ Stats
                    foreach (var t in ui.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
                    {
                        var text = t.GetComponentInChildren<UnityEngine.UI.Text>();
                        if (text != null && (text.text.ToLower().Contains("stat") || text.text.Contains("属性")))
                        {
                            t.gameObject.SetActive(false);
                        }

                        // ปรับเลือก Toggle เพศให้ตรงกับ NPC
                        if (text != null)
                        {
                            string txtVal = text.text.ToLower();
                            if (npcModel.sex == 1 && (txtVal.Contains("male") || txtVal.Contains("男")) && !txtVal.Contains("female") && !txtVal.Contains("女"))
                            {
                                t.isOn = true;
                            }
                            else if (npcModel.sex == 2 && (txtVal.Contains("female") || txtVal.Contains("女")))
                            {
                                t.isOn = true;
                            }
                        }
                    }

                    // หาปุ่ม Save (หรือปุ่มยืนยันสร้างตัวละครเดิม) แล้วเปลี่ยนป้ายชื่อ
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

                    // สร้างปุ่ม Exit พร้อมไอคอนกากบาท ✕ โดยคัดลอกปุ่ม saveBtn ให้กดโต้ตอบ (Interactable) ได้ 100%
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

                        // วางตำแหน่งถัดมาทางซ้ายของปุ่ม Save
                        Vector3 savePos = saveBtn.transform.localPosition;
                        exitObj.transform.localPosition = new Vector3(savePos.x - 160f, savePos.y, savePos.z);
                        exitObj.transform.localScale = saveBtn.transform.localScale;

                        exitBtn.onClick.RemoveAllListeners();
                        exitBtn.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
                        {
                            MelonLoader.MelonLogger.Msg("[NPCPortraitMod] Exit button clicked.");
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

                if (facade == null && ui != null)
                {
                    facade = ui.uiFacade;
                }

                if (facade == null) return;

                // 2. ปรับตัวเลือกชิ้นส่วนบน FacadeItemData เฉพาะชุดไอเทมที่ตรงกับเพศของ NPC
                // เราต้อง iterate ทั้งสองลิสต์เพื่อให้ sliderIndex ตรงกับของเกม (เกมสร้าง slider ต่อกันทั้งสองเพศ)
                var dressItemsList = new System.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<UICreatePlayerFacade.FacadeItemData>>();
                if (facade.manDressItems != null) dressItemsList.Add(facade.manDressItems);
                if (facade.womanDressItems != null) dressItemsList.Add(facade.womanDressItems);

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
                        
                        // ถ้ารายการนี้ตรงกับเพศของ NPC เราถึงจะแก้ไข
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

                                MatchAndSetItem(item, targetId, type);
                            }
                        }

                        sliderIndex++;
                    }
                }

                // สั่งอัปเดต UI Slider Text (Front Hair 45 ฯลฯ) ให้ตรงกับข้อมูลที่เพิ่งยัดไป
                try { facade.UpdateHandleGroup(); } catch { }

                try { facade.OnDressChanged(); } catch { }
                facade.UpdateModelData(); // อ่านค่า dressID จากสไลเดอร์มาเขียนทับ portraitModel.data ก่อน

                // Step 3 (หลัง UpdateModelData): บังคับเขียน id จริงของ NPC ลงใน portraitModel.data
                // ทำหลัง UpdateModelData เพราะ UpdateModelData จะอ่านค่า v.dressID (ซึ่งต่างจาก v.id) มาทับค่าที่ถูกต้อง
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

                    facade.portraitModel.data = facadeData; // บังคับเซ็ตกลับเข้า portraitModel!
                }

                facade.UpdateFacadeUI(); // render โมเดลจาก portraitModel.data ที่ถูก override ให้ถูกต้องแล้ว

                var finalData = (facade.portraitModel != null) ? facade.portraitModel.data : null;
                string finalStr = finalData != null
                    ? $"hat={finalData.hat}, hair={finalData.hair}, hairFront={finalData.hairFront}, head={finalData.head}, eyebrows={finalData.eyebrows}, eyes={finalData.eyes}, nose={finalData.nose}, mouth={finalData.mouth}, body={finalData.body}, back={finalData.back}, forehead={finalData.forehead}, faceFull={finalData.faceFull}, faceLeft={finalData.faceLeft}, faceRight={finalData.faceRight}"
                    : "NULL";
                MelonLoader.MelonLogger.Msg($"[NPCPortraitMod][DEBUG-CUSTOM] Final facade.portraitModel.data: {finalStr}");
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[NPCPortraitMod] Error applying NPC face: " + e);
            }
        }

        // โหลดข้อมูลหน้าตาเดิมของ NPC มาปรับแต่งใน UICreatePlayer
        [HarmonyPatch(typeof(UICreatePlayer), "InitData", new System.Type[] { typeof(int), typeof(GameLevelType), typeof(int) })]
        public static class Patch_UICreatePlayer_InitData
        {
            public static void Postfix(UICreatePlayer __instance)
            {
                // InitData runs before UICreatePlayerFacade.Init finished.
                // We do nothing here to let the game finish building facade structure first.
            }
        }

        // บังคับยัดค่า NPC ทับทันทีหลังจากเกมสั่งสุ่มหน้าตา (RandomFacade) ในทุกๆ กรณี
        [HarmonyPatch(typeof(UICreatePlayerFacade), "RandomFacade")]
        public static class Patch_UICreatePlayerFacade_RandomFacade
        {
            public static void Postfix(UICreatePlayerFacade __instance)
            {
                if (!string.IsNullOrEmpty(ModMain.EditingNpcId))
                {
                    var ui = g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer);
                    ApplyNpcFaceToUI(ui, __instance);
                }
            }
        }

        // โหลดข้อมูล NPC ยัดทับหลังจาก UICreatePlayerFacade สร้างและสุ่มค่าตั้งต้นเสร็จเรียบร้อย
        [HarmonyPatch(typeof(UICreatePlayerFacade), "Init", new System.Type[] { typeof(Transform) })]
        public static class Patch_UICreatePlayerFacade_Init
        {
            public static void Postfix(UICreatePlayerFacade __instance)
            {
                var ui = g.ui.GetUI<UICreatePlayer>(UIType.CreatePlayer);
                ApplyNpcFaceToUI(ui, __instance);
            }
        }

        // ป้องกัน NullReferenceException ภายใน UICreatePlayer.DestroyUI เพื่อให้ UIMgr.CloseUI คืนค่า Input Lock สำเร็จ
        [HarmonyPatch(typeof(UICreatePlayer), "DestroyUI")]
        public static class Patch_UICreatePlayer_DestroyUI
        {
            public static Exception Finalizer(Exception __exception)
            {
                return null; // ระงับ Exception ภายใน DestroyUI
            }
        }

        // เซฟข้อมูลหน้าตาใหม่ลง NPC เมื่อกดปุ่ม Confirm ใน UICreatePlayer
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
                        MelonLoader.MelonLogger.Msg("[NPCPortraitMod] Saving new face to NPC: " + npcId);

                        var npc = g.world.unit.GetUnit(npcId);
                        
                        // อ่านข้อมูลก่อนปิด UI
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

                            if (npc.data.unitData != null)
                            {
                                WorldUnitBase.CreateConf(npc.data.unitData); // รีเฟรชข้อมูลตัวละครและโมเดล NPC
                            }
                            MelonLoader.MelonLogger.Msg("[NPCPortraitMod] Successfully updated portrait model for NPC: " + npcId);
                        }

                        ModMain.EditingNpcId = null; // Reset state

                        // ปิด UI CreatePlayer ผ่าน UIMgr พร้อมปล่อย Input Lock
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
                            MelonLoader.MelonLogger.Warning("[NPCPortraitMod] Exception closing UI: " + closeEx);
                            try
                            {
                                __instance.gameObject.SetActive(false);
                                UnityEngine.Object.Destroy(__instance.gameObject);
                            }
                            catch { }
                        }

                        // เปิดหน้า UINPCInfo ของ NPC กลับขึ้นมาแสดงผลรูปโปรไฟล์ใหม่ทันที (กรณีแต่ง NPC)
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
                        MelonLoader.MelonLogger.Error("[NPCPortraitMod] Error saving NPC face: " + e);
                        ModMain.EditingNpcId = null;
                    }
                    finally
                    {
                        _isSaving = false;
                    }

                    return false; // ยับยั้ง flow ปกติของเกม เสมอ
                }

                return true; // กรณีสร้างตัวละครใหม่ตามปกติ
            }
        }
    }
}
