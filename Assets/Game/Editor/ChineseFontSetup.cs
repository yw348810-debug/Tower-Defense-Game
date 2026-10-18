using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using TowerDefense.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TowerDefense.EditorTools
{
    /// <summary>Builds a portable CJK SDF font using the user's OFL Noto Sans download.</summary>
    public static class ChineseFontSetup
    {
        public const string FontPath = "Assets/Resources/UI/ChineseFont.asset";
        public const string SourcePath = "Assets/ThirdParty/Fonts/NotoSansSC/NotoSansSC-Regular.otf";

        public static TMP_FontAsset Build()
        {
            Directory.CreateDirectory("Assets/Resources/UI");
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (!source) throw new InvalidOperationException("Import the Noto Sans SC font before creating the menu.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (!font)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 48, 8, GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
                if (!font) throw new InvalidOperationException("Could not create Chinese SDF font.");
                font.name = "Noto Sans SC UI";
                AssetDatabase.CreateAsset(font, FontPath);
                foreach (var texture in font.atlasTextures)
                    if (texture && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                if (font.material && !AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            var characters = new HashSet<char>();
            for (int c = 32; c <= 126; c++) characters.Add((char)c);
            foreach (char c in "中文简体，。！？：；、（）【】《》“”‘’—–…·×％＋−→ⅠⅡⅢ∞") characters.Add(c);
            foreach (var value in Localization.ChineseTexts)
                foreach (char c in value) if (!char.IsControl(c) && !char.IsSurrogate(c)) characters.Add(c);
            // Include characters in formatted translation templates, not only exact dictionary entries.
            foreach (var path in Directory.GetFiles("Assets/Game/Scripts/UI", "*.cs", SearchOption.AllDirectories))
                foreach (char c in File.ReadAllText(path)) if (c >= '\u3000' && c <= '\uffef' && !char.IsSurrogate(c)) characters.Add(c);
            string required = new string(characters.OrderBy(c => c).ToArray());
            font.TryAddCharacters(required, out string missing);
            foreach (var texture in font.atlasTextures)
            {
                if (!texture) continue;
                if (!AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
                EditorUtility.SetDirty(texture);
            }
            EditorUtility.SetDirty(font); EditorUtility.SetDirty(font.material);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning("Some optional UI font characters were unavailable: " + missing);
            Debug.Log("Chinese UI font ready: " + font.characterTable.Count + " characters, " + font.atlasTextures.Length + " atlases.");
            return font;
        }
    }
}
