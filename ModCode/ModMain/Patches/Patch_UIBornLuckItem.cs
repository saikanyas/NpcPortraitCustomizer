using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using NPCCustomizer.Helpers;

namespace NPCCustomizer.Patches
{
    /// <summary>
    /// Harmony patches for UIBornLuckItem to disable start-only destinies and display warning tooltips.
    /// </summary>
    public static class Patch_UIBornLuckItem
    {
        [HarmonyPatch(typeof(UIBornLuckItem), "UpdateUI")]
        public static class Patch_UIBornLuckItem_UpdateUI
        {
            public static void Postfix(UIBornLuckItem __instance)
            {
                if (string.IsNullOrEmpty(ModMain.EditingNpcId) || __instance == null) return;

                try
                {
                    if (__instance.item != null && DestinyHelper.IsStartOnlyDestiny(__instance.item))
                    {
                        // 1. Disable toggle selection
                        var toggle = __instance.GetComponent<Toggle>() ?? __instance.GetComponentInChildren<Toggle>(true);
                        if (toggle != null)
                        {
                            toggle.interactable = false;
                            toggle.isOn = false;
                        }

                        // 2. Tint visuals to greyed out / disabled look
                        var images = __instance.GetComponentsInChildren<Image>(true);
                        if (images != null)
                        {
                            foreach (var img in images)
                            {
                                if (img != null && (img.gameObject.name.ToLower().Contains("icon") || img.gameObject.name.ToLower().Contains("back") || img.gameObject.name.ToLower().Contains("bg")))
                                {
                                    img.color = new Color(0.5f, 0.5f, 0.5f, 0.65f);
                                }
                            }
                        }

                        var texts = __instance.GetComponentsInChildren<Text>(true);
                        if (texts != null)
                        {
                            foreach (var txt in texts)
                            {
                                if (txt != null && !txt.text.Contains("【"))
                                {
                                    txt.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn("[BornLuckItem]", "Error updating UIBornLuckItem UI state: " + ex.Message);
                }
            }
        }
    }
}
