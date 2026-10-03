using System.IO;
using UnityEditor;
using UnityEngine;

namespace PocketToyworks.Editor
{
    // Runtime sprites use source-pixel borders. Resampling NPOT art changes those borders
    // and bends curved corners through the stretchable center of a nine-slice.
    public sealed class WorkshopUIImport : AssetPostprocessor
    {
        const string Folder = "Assets/PocketToyworks/Resources/UI/";
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.ClearPlatformTextureSettings("Android");
        }

        [MenuItem("Pocket Toyworks/Repair UI texture imports")]
        public static void RefreshArt()
        {
            foreach (var path in Directory.GetFiles(Folder, "*.png"))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
