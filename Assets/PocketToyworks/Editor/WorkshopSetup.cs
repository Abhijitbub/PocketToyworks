using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PocketToyworks.Core;

namespace PocketToyworks.Editor
{
    [InitializeOnLoad] public static class WorkshopSetup
    {
        const string Root="Assets/PocketToyworks/Generated";
        static WorkshopSetup(){EditorApplication.delayCall+=Auto;EditorApplication.update+=Poll;}
        static void Auto()
        {
            string pending=SessionState.GetString("ToyworksPendingBuild","");
            if(pending!=""){SessionState.EraseString("ToyworksPendingBuild");if(pending=="android")BuildAndroid();else BuildWindows();return;}
            if(!File.Exists(Root+"/Workshop.unity")&&!EditorApplication.isPlayingOrWillChangePlaymode)Generate();
        }
        static double nextPoll;
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;nextPoll=EditorApplication.timeSinceStartup+1;
            string path="Verification/editor-command.txt";if(!File.Exists(path))return;
            string command=File.ReadAllText(path).Trim();File.Delete(path);
            try {
                if(command=="generate")Generate();
                if(command=="verify")Verify();
                if(command=="play") {EditorSceneManager.OpenScene(Root+"/Workshop.unity");EditorApplication.isPlaying=true;}
                if(command=="stop")EditorApplication.isPlaying=false;
                if(command=="android")BuildAndroid();
                if(command=="windows")BuildWindows();
                if(command=="capture") {Directory.CreateDirectory("Verification/Screenshots");ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/Screenshots/gameplay.png"));}
                if(command.StartsWith("level:"))UnityEngine.Object.FindAnyObjectByType<WorkshopView>()?.LoadLevel(command.Substring(6));
                if(command=="solve") {var view=UnityEngine.Object.FindAnyObjectByType<WorkshopView>();if(view!=null){view.LoadLevel(view.Level.id);foreach(var c in view.Level.solution)view.Commit(c);}}
                File.WriteAllText("Verification/editor-status.txt",command+" OK "+DateTime.UtcNow);
            }catch(Exception e){File.WriteAllText("Verification/editor-status.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Pocket Toyworks/Regenerate prototype assets")]
        public static void Generate()
        {
            WorkshopUIImport.RefreshArt();WorkshopFonts.Generate();
            Directory.CreateDirectory(Root);ContentAuthoring.Export();AssetDatabase.Refresh();
            var mesh=RoundedBox();var old=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/RoundedPlastic.asset");if(old==null)AssetDatabase.CreateAsset(mesh,Root+"/RoundedPlastic.asset");else{EditorUtility.CopySerialized(mesh,old);mesh=old;}
            var tuning=AssetDatabase.LoadAssetAtPath<WorkshopTuning>(Root+"/WorkshopTuning.asset");if(tuning==null){tuning=ScriptableObject.CreateInstance<WorkshopTuning>();AssetDatabase.CreateAsset(tuning,Root+"/WorkshopTuning.asset");}
            Material Mat(string name,Color color)
            {
                string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.32f);return m;
            }
            tuning.cream=new Color32(227,239,253,255);tuning.teal=new Color32(51,135,225,255);tuning.ink=new Color32(32,53,88,255);
            tuning.accents=new Color[]{new Color32(247,83,102,255),new Color32(255,202,49,255),new Color32(61,154,247,255),new Color32(166,94,236,255)};EditorUtility.SetDirty(tuning);
            var coral=Mat("Coral plastic",tuning.accents[0]);var yellow=Mat("Yellow plastic",tuning.accents[1]);var teal=Mat("Teal plastic",tuning.teal);var cream=Mat("Cream plastic",new Color32(255,246,222,255));var ink=Mat("Dark inlay",tuning.ink);
            var robot=new GameObject("Pip — original workshop robot");
            void Part(string name,Vector3 position,Vector3 scale,Material material)
            {
                var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(robot.transform);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=material;
            }
            Part("Body",new Vector3(0,0,0),new Vector3(1.48f,1.25f,.8f),teal);
            Part("Head",new Vector3(0,1.04f,-.02f),new Vector3(1.72f,.98f,.88f),coral);
            Part("Face inset",new Vector3(0,1.04f,-.48f),new Vector3(1.34f,.57f,.09f),cream);
            Part("Left eye",new Vector3(-.36f,1.10f,-.55f),new Vector3(.15f,.23f,.1f),ink);
            Part("Right eye",new Vector3(.36f,1.10f,-.55f),new Vector3(.15f,.23f,.1f),ink);
            Part("Smile",new Vector3(0,.88f,-.55f),new Vector3(.27f,.065f,.09f),ink);
            Part("Chest badge",new Vector3(0,.13f,-.46f),new Vector3(.49f,.48f,.14f),yellow);
            Part("Badge centre",new Vector3(0,.13f,-.55f),new Vector3(.13f,.13f,.05f),cream);
            for(int side=-1;side<=1;side+=2){Part("Arm",new Vector3(side*.99f,-.04f,0),new Vector3(.38f,.94f,.54f),yellow);Part("Foot",new Vector3(side*.45f,-.9f,-.10f),new Vector3(.58f,.54f,.95f),coral);}
            Part("Antenna",new Vector3(.38f,1.69f,0),new Vector3(.12f,.34f,.12f),teal);Part("Antenna cap",new Vector3(.38f,1.9f,0),new Vector3(.3f,.25f,.3f),yellow);
            var prefab=PrefabUtility.SaveAsPrefabAsset(robot,Root+"/PipRobot.prefab");UnityEngine.Object.DestroyImmediate(robot);
            // Only create/replace our owned scene; preserve unrelated open scenes and their unsaved state.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
            var root=new GameObject("Pocket Toyworks");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);var view=root.AddComponent<WorkshopView>();view.tuning=tuning;view.robotPrefab=prefab;view.roundedMesh=mesh;
            var camera=new GameObject("Workshop Camera",typeof(Camera),typeof(AudioListener));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera,scene);camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);var cam=camera.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=5;cam.backgroundColor=tuning.cream;cam.clearFlags=CameraClearFlags.SolidColor;cam.cullingMask=~(1<<30);
            var light=new GameObject("Softbox",typeof(Light));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light,scene);light.transform.rotation=Quaternion.Euler(35,-35,0);light.GetComponent<Light>().type=LightType.Directional;light.GetComponent<Light>().intensity=1.3f;light.GetComponent<Light>().shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.73f,.73f,.69f);
            EditorSceneManager.SaveScene(scene,Root+"/Workshop.unity");if(!Application.isBatchMode)EditorSceneManager.CloseScene(scene,true);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Workshop.unity",true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=Root+"/Workshop.unity")).ToArray();
            PlayerSettings.bundleVersion="0.5.0";PlayerSettings.Android.bundleVersionCode=8;
            PlayerSettings.companyName="Pocket Toyworks";PlayerSettings.productName="Pocket Toyworks Prototype";PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;PlayerSettings.defaultScreenWidth=720;PlayerSettings.defaultScreenHeight=1280;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.pockettoyworks.prototype");
            AssetDatabase.SaveAssets();Verify();
        }
        [MenuItem("Pocket Toyworks/Validate all content")]
        public static void Verify()
        {
            Directory.CreateDirectory("Verification");var lines=new List<string>();var fingerprints=new HashSet<string>();
            foreach(var file in Directory.GetFiles("Assets/PocketToyworks/Resources/Levels","*.json")) {var l=JsonUtility.FromJson<Level>(File.ReadAllText(file));Validator.Validate(l);if(!l.id.StartsWith("compare")&&!fingerprints.Add(Validator.Fingerprint(l)))throw new InvalidOperationException("Duplicate puzzle: "+l.id);lines.Add(l.id+": PASS — "+l.pieces.Length+" pieces, "+l.solution.Length+" actions, "+l.activeLimit+" active mould(s)");}
            File.WriteAllLines("Verification/content-validation.txt",lines);Debug.Log("Pocket Toyworks: all "+lines.Count+" solution traces passed.");
        }
        [MenuItem("Pocket Toyworks/Build Android development APK")]
        public static void BuildAndroid()
        {
            if(!EnsureTarget(BuildTarget.Android,"android"))return;
            Generate();Directory.CreateDirectory("Builds/Android");PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Workshop.unity"},locationPathName="Builds/Android/PocketToyworks-development.apk",target=BuildTarget.Android,options=BuildOptions.Development});
            File.WriteAllText("Verification/android-build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; bytes="+report.summary.totalSize);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new BuildFailedException("Android build failed; inspect the Unity log.");
        }
        public static void BuildWindows()
        {
            if(!EnsureTarget(BuildTarget.StandaloneWindows64,"windows"))return;
            Directory.CreateDirectory("Builds/Windows");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Workshop.unity"},locationPathName="Builds/Windows/PocketToyworks.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});File.WriteAllText("Verification/windows-build.txt",report.summary.result.ToString());
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new BuildFailedException("Windows build failed; inspect the Unity log.");
        }
        static bool EnsureTarget(BuildTarget target,string key)
        {
            if(EditorUserBuildSettings.activeBuildTarget==target)return true;
            if(Application.isBatchMode)throw new InvalidOperationException("Pass -buildTarget "+(target==BuildTarget.Android?"Android":"Win64")+" before -executeMethod so URP resources are prepared for the correct platform.");
            SessionState.SetString("ToyworksPendingBuild",key);
            if(!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target),target)){SessionState.EraseString("ToyworksPendingBuild");throw new InvalidOperationException("Could not switch build platform");}
            return false;
        }
        static Mesh RoundedBox()
        {
            const int subdivisions=8;const float radius=.12f;var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
            var axes=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var axis in axes)
            {
                Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.5f?Vector3.forward:Vector3.up),v=Vector3.Cross(axis,u);int offset=vertices.Count;
                for(int y=0;y<=subdivisions;y++)for(int x=0;x<=subdivisions;x++)
                {
                    var p=axis*.5f+u*(x/(float)subdivisions-.5f)+v*(y/(float)subdivisions-.5f);var inner=new Vector3(Mathf.Clamp(p.x,-.5f+radius,.5f-radius),Mathf.Clamp(p.y,-.5f+radius,.5f-radius),Mathf.Clamp(p.z,-.5f+radius,.5f-radius));var normal=(p-inner).normalized;vertices.Add(inner+normal*radius);normals.Add(normal);
                }
                for(int y=0;y<subdivisions;y++)for(int x=0;x<subdivisions;x++){int a=offset+y*(subdivisions+1)+x,b=a+1,c=a+subdivisions+1,d=c+1;indices.AddRange(new[]{a,b,d,a,d,c});}
            }
            var mesh=new Mesh{name="Original rounded plastic unit"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
    }
}

