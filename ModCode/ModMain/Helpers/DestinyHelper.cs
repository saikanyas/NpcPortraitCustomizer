using System;
using System.Collections.Generic;
using UnityEngine;

namespace NPCCustomizer.Helpers
{
    /// <summary>
    /// Helper utilities for evaluating, filtering, and displaying Innate Destinies (先天气运 / BornLuck).
    /// </summary>
    public static class DestinyHelper
    {
        // Known Start-Only Destiny Keywords (Chinese, English, and Pinyin)
        private static readonly HashSet<string> StartOnlyKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "一见钟情", "true love", "fall in love",
            "开局神器", "伴生法宝", "starter artifact",
            "富甲天下", "初始灵石", "starting wealth",
            "身外之物", "初始功法", "starting manual",
            "千里眼", "开局探图", "map reveal",
            "神仙开局", "青梅竹马", "childhood friend",
            "双修之恋", "初始伴侣", "starting partner"
        };

        // Known Start-Only Effect Action Prefix patterns
        private static readonly string[] StartOnlyEffectPrefixes = new string[]
        {
            "addunit", "createunit", "addpartner", "openmap", "drama"
        };

        /// <summary>
        /// Checks whether a given Innate Destiny is designed strictly for new game creation / world generation.
        /// </summary>
        public static bool IsStartOnlyDestiny(ConfRoleCreateFeatureItem item)
        {
            if (item == null) return false;

            // 1. Check by Name
            if (!string.IsNullOrEmpty(item.name))
            {
                foreach (var kw in StartOnlyKeywords)
                {
                    if (item.name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            // 2. Check by Effect String
            if (!string.IsNullOrEmpty(item.effect))
            {
                string effLower = item.effect.ToLower();
                foreach (var prefix in StartOnlyEffectPrefixes)
                {
                    if (effLower.Contains(prefix))
                        return true;
                }
            }

            // 3. Check by Tips/Description keywords
            if (!string.IsNullOrEmpty(item.tips))
            {
                string tipLower = item.tips.ToLower();
                if (tipLower.Contains("开局") && (tipLower.Contains("获得") || tipLower.Contains("伴侣") || tipLower.Contains("探索")))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a given Innate Destiny ID is start-only.
        /// </summary>
        public static bool IsStartOnlyDestiny(int destinyId)
        {
            if (destinyId <= 0) return false;
            try
            {
                var item = g.conf.roleCreateFeature.GetItem(destinyId);
                return IsStartOnlyDestiny(item);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formats a destiny's tooltip description with a clear warning notice if it is start-only.
        /// </summary>
        public static string FormatWarningTips(string originalTips)
        {
            string warningTag = "<color=#FF4444>【仅开局生效 - 已禁用 / Game Start Only - Disabled】\n此气运仅在开局创角时生效，中途选择可能导致游戏或NPC逻辑异常。\nThis destiny only takes effect on new game start and is disabled mid-game to prevent corruption.</color>\n\n";
            return warningTag + (originalTips ?? "");
        }
    }
}
