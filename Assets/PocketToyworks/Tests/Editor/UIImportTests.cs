using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace PocketToyworks.Tests
{
    public class UIImportTests
    {
        [Test] public void UIFontsUseDistanceFieldsAndContainInterfaceCharacters()
        {
            FontEngine.InitializeFontEngine();
            var outline=Resources.Load<Material>("Fonts/DisplayOutline");Assert.IsNotNull(outline);Assert.IsTrue(outline.IsKeywordEnabled("OUTLINE_ON"),"Keep the outlined shader variant in player builds");
            foreach(var name in new[]{"DisplaySDF","BodySDF"})
            {
                var font=Resources.Load<TMP_FontAsset>("Fonts/"+name);
                Assert.IsNotNull(font,name+" must be baked before building");
                Assert.AreEqual(GlyphRenderMode.SDFAA,font.atlasRenderMode,name);
                Assert.AreEqual(AtlasPopulationMode.Dynamic,font.atlasPopulationMode,name+" must support player names");
                foreach(var character in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789·•–—…")
                    Assert.IsTrue(font.HasCharacter(character),name+" lacks "+character);
                Assert.IsNotNull(font.sourceFontFile,name+" needs its source font for dynamic glyphs");
                Assert.AreEqual(FontEngineError.Success,FontEngine.LoadFontFace(font.sourceFontFile,72),name+" needs usable embedded font data");
                // TryAddCharacters returns false when every requested glyph already exists.
                // HasCharacter with tryAddCharacter verifies availability on repeated runs too.
                Assert.IsTrue(font.HasCharacter('é',false,true),name+" cannot render player-name glyphs");
                Assert.AreEqual(1,font.atlasTexture.mipmapCount,name+" must not blur UI text with mipmaps");
                Assert.That(font.material.shader.name,Does.Contain("Distance Field"),name);
            }
        }
        [Test] public void UITexturesRetainAuthoredDimensionsAndDoNotGenerateMipmaps()
        {
            foreach (var path in Directory.GetFiles("Assets/PocketToyworks/Resources/UI", "*.png"))
            {
                var bytes = File.ReadAllBytes(path);
                int Dimension(int offset) => (bytes[offset]<<24)|(bytes[offset+1]<<16)|(bytes[offset+2]<<8)|bytes[offset+3];
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreEqual(Dimension(16), imported.width, path+" width was resampled");
                Assert.AreEqual(Dimension(20), imported.height, path+" height was resampled");
                var settings = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterNPOTScale.None, settings.npotScale, path);
                Assert.IsFalse(settings.mipmapEnabled, path);
                Assert.AreEqual(TextureWrapMode.Clamp, settings.wrapMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, settings.textureCompression, path);
            }
        }
    }
}
