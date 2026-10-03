using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PocketToyworks.Editor
{
    public static class WorkshopFonts
    {
        public static void Generate()
        {
            FontEngine.InitializeFontEngine();
            if (TMP_Settings.instance == null)
                throw new System.InvalidOperationException("Missing bundled TMP essentials. Import TMP Essential Resources in Unity, or run Tools/ImportTMPResources.py against this project before batch generation.");
            Bake("ToyworksRounded", "DisplaySDF");
            Bake("ToyworksText", "BodySDF");
            const string outlinePath="Assets/PocketToyworks/Resources/Fonts/DisplayOutline.mat";
            var display=Resources.Load<TMP_FontAsset>("Fonts/DisplaySDF");
            var outline=AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
            if(outline==null){outline=new Material(display.material);AssetDatabase.CreateAsset(outline,outlinePath);}
            outline.CopyPropertiesFromMaterial(display.material);outline.name="Toyworks outlined display";
            outline.SetColor("_OutlineColor",new Color32(32,24,77,255));outline.SetFloat("_OutlineWidth",.18f);outline.EnableKeyword("OUTLINE_ON");
            EditorUtility.SetDirty(outline);AssetDatabase.SaveAssets();
        }
        static void Bake(string source, string name)
        {
            string path="Assets/PocketToyworks/Resources/Fonts/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(existing!=null){Preload(existing);return;}
            var font=Resources.Load<Font>("Fonts/"+source);
            if(font==null)throw new System.InvalidOperationException("Missing font: "+source);
            var asset=TMP_FontAsset.CreateFontAsset(font,72,12,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            asset.name=name;asset.atlasTextures[0].name=name+" Atlas";asset.material.name=name+" Material";
            asset.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{TMP_Settings.defaultFontAsset};
            AssetDatabase.CreateAsset(asset,path);
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0],asset);
            AssetDatabase.AddObjectToAsset(asset.material,asset);
            Preload(asset);
        }
        static void Preload(TMP_FontAsset asset)
        {
            asset.ReadFontAssetDefinition();
            var settings=new SerializedObject(asset);settings.FindProperty("m_ClearDynamicDataOnBuild").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();
            string needed=new string((new string(Enumerable.Range(32,95).Select(n=>(char)n).ToArray())+"·•–—…").Where(c=>!asset.HasCharacter(c)).ToArray());
            if(needed.Length>0&&!asset.TryAddCharacters(needed,out string missing))
                throw new System.InvalidOperationException(asset.name+" cannot render required characters: "+missing);
            EditorUtility.SetDirty(asset);EditorUtility.SetDirty(asset.atlasTextures[0]);EditorUtility.SetDirty(asset.material);AssetDatabase.SaveAssets();
        }
    }
}
