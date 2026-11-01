using System.Collections.Generic;
using TMPro;
using UnityEngine;
#nullable enable
namespace MajdataPlay
{
    /// <summary>
    /// 不带描边、阴影和发光的 TMP 字体材质。场景里的字体材质带描边和下阴影，放在浅色界面上很难读，
    /// 列表式选歌和分享图的文字统一换成这份材质；每个字体只建一份，整个程序共用
    /// </summary>
    internal static class CleanTmpMaterial
    {
        static readonly Dictionary<TMP_FontAsset, Material> _cache = new();
        static readonly string[] EFFECT_KEYWORDS = { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "GLOW_ON", "BEVEL_ON" };
        static readonly string[] ZERO_PROPERTIES =
        {
            "_OutlineWidth", "_OutlineSoftness", "_FaceDilate",
            "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlayDilate", "_UnderlaySoftness",
            "_GlowPower", "_GlowOffset", "_GlowInner", "_GlowOuter", "_Bevel",
        };

        public static Material? Get(TMP_FontAsset? font)
        {
            if (font is null || font.material is null)
            {
                return null;
            }
            if (_cache.TryGetValue(font, out var cached) && cached != null)
            {
                return cached;
            }
            var material = new Material(font.material)
            {
                name = $"{font.material.name} (Clean)",
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            foreach (var keyword in EFFECT_KEYWORDS)
            {
                material.DisableKeyword(keyword);
            }
            foreach (var property in ZERO_PROPERTIES)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, 0f);
                }
            }
            if (material.HasProperty("_OutlineColor"))
            {
                material.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0f));
            }
            if (material.HasProperty("_UnderlayColor"))
            {
                material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0f));
            }
            if (material.HasProperty("_FaceColor"))
            {
                material.SetColor("_FaceColor", Color.white);
            }
            _cache[font] = material;
            return material;
        }
        /// <summary>
        /// 在设置字体之后调用：把文字换成干净的材质
        /// </summary>
        public static void Apply(TMP_Text text)
        {
            var material = Get(text.font);
            if (material is not null)
            {
                text.fontSharedMaterial = material;
            }
        }
    }
}
