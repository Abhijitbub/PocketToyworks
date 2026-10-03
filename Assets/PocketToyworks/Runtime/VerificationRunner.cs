#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PocketToyworks.Core;
namespace PocketToyworks
{
    public class VerificationRunner : MonoBehaviour
    {
        const string Shots="Verification/Screenshots/v0.5.0";int errors,captureScale=1;bool layoutFailed;
        void OnEnable(){Application.logMessageReceived+=Log;}void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Environment.GetCommandLineArgs().Contains("--verify-workshop"))new GameObject("Automated scene verification").AddComponent<VerificationRunner>();}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Shots);Screen.SetResolution(720,1280,FullScreenMode.Windowed);yield return new WaitForSeconds(2);while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            Screen.SetResolution(360,640,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);Screen.SetResolution(720,1280,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            var view=FindAnyObjectByType<WorkshopView>();if(view==null){Fail("No workshop component");yield break;}
            // The hidden desktop player must not inherit focus loss from unrelated
            // artifact rendering. Explicit pause callbacks remain exercised below.
            view.SetVerificationFocus(true);
            view.Data.completed.Clear();view.Data.pausedAttempts.Clear();view.Data.player=new PlayerProgress();Progression.Ensure(view.Data,DateTime.UtcNow);
            view.LoadLevel("level-01");yield return new WaitForSeconds(.2f);yield return SourceClick(view,0);yield return BoardClick(view,0,0);
            if(view.Data.session.state.actions!=1){Fail("Tap placement did not commit");yield break;}yield return Capture("gameplay-single-720x1280");
            view.Undo();yield return BoardDrag(view,0,0,0);if(view.Data.session.state.actions!=1){Fail("Drag placement failed");yield break;}
            yield return SourceClick(view,1);yield return BoardClick(view,0,0);if(view.Data.session.state.actions!=1){Fail("Invalid overlap changed state");yield break;}
            view.LoadLevel("level-06");yield return new WaitForSeconds(.2f);yield return SourceClick(view,0);yield return SourceClick(view,3);if(string.IsNullOrEmpty(view.Data.session.state.reserves[0])){Fail("Reserve parking failed");yield break;}yield return Capture("gameplay-dual-720x1280");
            Screen.SetResolution(360,800,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);yield return Capture("gameplay-tall-360x800");
            Screen.SetResolution(360,720,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);yield return Capture("gameplay-18x9-360x720");
            Screen.SetResolution(360,785,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            view.LoadLevel("level-03");view.Commit(Command.Park(0,0));view.Commit(view.Level.solution[1]);view.Commit(view.Level.solution[2]);yield return new WaitForSeconds(.2f);
            yield return SourceClick(view,3);yield return Rotate(view);yield return Rotate(view);yield return SourceDrag(view,3,new Vector2(25,400));yield return BoardDrag(view,3,0,0);
            if(!Rules.Won(view.Level,view.Data.session.state)){Fail("Recorded reserve L-piece rotation regression failed on tall screen");yield break;}
            view.LoadLevel("level-03");yield return SourceClick(view,0);yield return Rotate(view);yield return Rotate(view);yield return BoardDrag(view,0,0,0);if(view.Data.session.state.actions!=1){Fail("Tall rotated lane drag failed");yield break;}yield return Capture("recording-ratio-360x785");
            Screen.SetResolution(360,640,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);yield return Capture("gameplay-short-360x640");Screen.SetResolution(720,1280,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            view.LoadLevel("level-01");foreach(var c in view.Level.solution){view.Commit(c);yield return null;}yield return new WaitForSeconds(1);yield return Capture("toy-result-720x1280");
            view.LoadLevel("level-03");yield return SourceClick(view,0);yield return Rotate(view);yield return Rotate(view);yield return SourceClick(view,1);yield return SourceClick(view,0);yield return SourceDrag(view,0,new Vector2(25,400));yield return BoardDrag(view,0,0,0);
            if(view.Data.session.state.actions!=1){Fail("Orientation reset after selection switching or rejected drop");yield break;}
            yield return Click(360,1190);yield return new WaitForSeconds(.5f);yield return Capture("rotation-hint-720x1280");
            view.LoadLevel("level-03");view.Commit(Command.Park(0,0));yield return Click(555,174);if(view.Level.id!="tutorial-01"||!view.Data.hasSuspendedSession){Fail("Tutorial did not suspend the current puzzle");yield break;}yield return Capture("tutorial-720x1280");
            yield return BoardDrag(view,0,2,0);if(view.Data.session.state.actions!=1){Fail("Tutorial first fit failed");yield break;}
            yield return Click(360,1190);yield return SourceDrag(view,0,view.VerificationSource(3).center);if(view.Data.session.state.actions!=2){Fail("Tutorial hint/parking failed");yield break;}
            yield return BoardDrag(view,1,2,2);yield return SourceClick(view,3);yield return Rotate(view);yield return BoardDrag(view,3,0,0);
            if(!Rules.Won(view.Level,view.Data.session.state)){Fail("Tutorial final rotated fit failed");yield break;}
            yield return new WaitForSeconds(1);yield return ClickNamed("Let's build!");if(view.Level.id!="level-03"||view.Data.session.state.actions!=1||view.Data.hasSuspendedSession){Fail("Tutorial did not restore the suspended puzzle");yield break;}
            view.LoadLevel("level-10");yield return Click(view.VerificationQueue.center.x,view.VerificationQueue.center.y);yield return Capture("queue-720x1280");
            // A plausible returning-player fixture: six first clears, their earned coins, and level 7 ready.
            view.Data.completed=Enumerable.Range(1,6).Select(n=>"level-"+n.ToString("00")).ToList();view.Data.player=new PlayerProgress{initialized=true,coins=450,totalEarned=300,rewardedLevels=new List<string>(view.Data.completed)};Progression.Ensure(view.Data,DateTime.UtcNow);view.LoadLevel("level-07");view.EnterHome();yield return new WaitForSeconds(.3f);yield return Capture("home-map-720x1280");
            yield return Click(66,65);yield return Capture("profile-characters-720x1280");yield return Click(580,815);if(view.Data.player.avatar!=7){Fail("Character selection did not persist");yield break;}
            yield return Click(400,340);yield return new WaitForSeconds(.2f);var name=FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).First(f=>f.gameObject.activeInHierarchy);name.text="Pocket Maker";name.onEndEdit.Invoke(name.text);yield return ClickNamed("Frames");yield return Click(550,627);
            if(view.Data.player.playerName!="Pocket Maker"||view.Data.player.frame!=2||view.Data.player.coins!=350){Fail("Profile editing or frame purchase failed");yield break;}yield return Capture("profile-frames-720x1280");
            name=FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).First(f=>f.gameObject.activeInHierarchy);name.text="WWWWWWWWWWWWWWWW";name.onEndEdit.Invoke(name.text);yield return Capture("profile-long-name-720x1280");if(view.Data.player.playerName!=name.text){Fail("Maximum-length name did not persist");yield break;}name.text="Pocket Maker";name.onEndEdit.Invoke(name.text);yield return ClickNamed("Done");
            yield return Click(583,96);yield return ClickNamed("Claim 75");if(view.Data.player.coins!=425){Fail("Daily gift did not credit coins");yield break;}yield return ClickNamed("Claimed");if(view.Data.player.coins!=425){Fail("Gift was paid twice");yield break;}
            yield return ClickNamed("50 coins");yield return ClickNamed("80 coins");if(view.Data.player.coins!=295||view.Data.player.freezeCharges!=4||view.Data.player.fitCharges!=2){Fail("Earned-coin tool purchases failed");yield break;}yield return Capture("workshop-720x1280");
            Progression.LoseLife(view.Data.player,DateTime.UtcNow);yield return ClickNamed("150 coins");yield return Capture("lives-720x1280");yield return ClickNamed("Refill - 150 coins");if(view.Data.player.lives!=5||view.Data.player.coins!=145){Fail("Coin life refill failed");yield break;}yield return ClickNamed("Refill - 150 coins");if(view.Data.player.coins!=145){Fail("Full-life refill charged again");yield break;}yield return ClickArt("icon-close");
            view.EnterHome();yield return ClickNamed("7");yield return Capture("level-card-720x1280");yield return ClickNamed("Play level");if(!view.Data.player.challenge){Fail("Timed mode did not start");yield break;}
            double before=view.Data.player.remainingSeconds;yield return Click(500,1190);if(view.Data.player.freezeCharges!=3||view.Data.player.remainingSeconds<before+25){Fail("Time booster did not apply");yield break;}
            yield return Click(638,1190);yield return new WaitForSeconds(.6f);if(view.Data.session.state.actions!=1||view.Data.player.fitCharges!=1){Fail("Verified auto-fit did not commit or consumed wrong quantity");yield break;}
            yield return Capture("timed-gameplay-720x1280");yield return Click(665,55);double paused=view.Data.player.remainingSeconds;yield return new WaitForSeconds(.4f);if(Math.Abs(paused-view.Data.player.remainingSeconds)>.001){Fail("Timer advanced during pause");yield break;}yield return Capture("settings-720x1280");yield return ClickNamed("Resume");
            yield return Click(665,141);yield return Capture("leave-confirmation-720x1280");yield return Click(649,319);if(view.Data.player.lives!=5||view.Data.session.state.actions!=1){Fail("Cancelling restart consumed a life or puzzle state");yield break;}
            yield return Click(665,141);yield return ClickNamed("Restart");if(view.Data.player.lives!=4||view.Data.session.state.actions!=0){Fail("Confirmed restart did not cost exactly one life");yield break;}
            view.Data.player.remainingSeconds=.04;yield return new WaitForSeconds(.4f);if(!view.Data.player.failed||view.Data.player.lives!=3){Fail("Timeout did not consume one life");yield break;}yield return new WaitForSeconds(.3f);if(view.Data.player.lives!=3){Fail("Timeout charged repeatedly");yield break;}yield return Capture("time-up-720x1280");yield return ClickNamed("Home");
            view.SendMessage("OnApplicationPause",true);var resumed=new SaveStore(Path.GetFullPath("Verification/PlayerSave")).Read(Content.Get);view.SendMessage("OnApplicationPause",false);if(resumed==null||resumed.player.avatar!=7||resumed.player.frame!=2||resumed.player.playerName!="Pocket Maker"||resumed.player.coins!=145||resumed.player.lives!=3){Fail("Actual player profile/economy save did not restore");yield break;}
            yield return Click(463,1203);yield return Capture("collection-720x1280");
            // Timed play is the only main-level flow. Undo cannot restore time;
            // Home/tutorial/level switching preserve attempts and zero lives gate new ones.
            view.Data.player.lives=2;view.LoadLevel("level-07");yield return new WaitForSeconds(.2f);
            if(!view.Data.player.challenge||!view.Data.player.challengeStarted||view.Data.player.remainingSeconds<=0){Fail("Default level did not start its timer");yield break;}
            view.Commit(Command.Park(0,0));double undoClock=view.Data.player.remainingSeconds;view.Undo();yield return null;
            if(view.Data.session.state.actions!=0||!view.Data.player.challengeStarted||view.Data.player.remainingSeconds>undoClock||view.Data.player.lives!=2){Fail("Undo rewound time or lost attempt identity");yield break;}
            yield return Capture("default-timed-720x1280");
            view.SendMessage("OnApplicationPause",true);double backgroundClock=view.Data.player.remainingSeconds;yield return new WaitForSeconds(.2f);if(view.Data.player.remainingSeconds!=backgroundClock){Fail("Background pause advanced clock");yield break;}view.SendMessage("OnApplicationPause",false);
            yield return Click(665,55);yield return ClickNamed("Home");double homeClock=view.Data.player.remainingSeconds;yield return new WaitForSeconds(.3f);yield return Capture("paused-home-720x1280");
            if(view.Data.player.remainingSeconds!=homeClock||view.Data.player.lives!=2){Fail("Home did not pause without charge");yield break;}yield return ClickNamed("Continue Level 7");
            view.EnterHome();double savedClock=view.Data.player.remainingSeconds;yield return ClickNamed("6");yield return ClickNamed("Play level");view.Commit(Command.Park(0,0));view.EnterHome();
            yield return ClickNamed("7");yield return ClickNamed("Continue level");
            if(!view.Data.player.challengeStarted||view.Data.session.state.actions!=0||view.Data.player.remainingSeconds>savedClock||view.Data.player.remainingSeconds<savedClock-1||view.Data.player.lives!=2){Fail("Level switching reset the saved clock");yield break;}
            yield return ClickNamed("Learn");if(!view.Data.suspendedChallengeStarted||view.Data.player.challenge||view.Data.player.remainingSeconds!=0){Fail("Tutorial did not suspend the timed attempt");yield break;}yield return Click(665,55);yield return ClickNamed("Home");yield return ClickNamed("Continue Level 7");
            if(!view.Data.player.challengeStarted){Fail("Tutorial return made restart free");yield break;}
            yield return Click(665,141);yield return Capture("restart-after-undo-720x1280");yield return ClickNamed("Restart");
            if(view.Data.player.lives!=1||view.Data.session.state.actions!=0||view.Data.player.remainingSeconds<238){Fail("Restart after undo did not charge once/reset timer");yield break;}
            yield return Click(665,141);yield return ClickNamed("Restart");yield return Capture("zero-lives-720x1280");
            if(view.Data.player.lives!=0||!view.Data.player.failed||!view.Data.player.challenge){Fail("Last-life restart left an accessible untimed or fresh attempt");yield break;}
            yield return ClickArt("icon-close");yield return ClickNamed("Play Level 7");
            if(!view.Data.player.failed){Fail("Home Play bypassed zero-life gate");yield break;}
            yield return ClickArt("icon-close");yield return ClickNamed("7");yield return ClickNamed("Play level");
            if(!view.Data.player.failed){Fail("Level card bypassed zero-life gate");yield break;}
            yield return ClickArt("icon-close");yield return ClickNamed("Learn");view.Commit(view.Level.solution[0]);view.Undo();
            if(view.Data.player.lives!=0||view.Data.player.challenge||view.Data.player.remainingSeconds!=0){Fail("Tutorial used lives or timer");yield break;}
            yield return Click(665,55);yield return ClickNamed("Home");
            view.Data.player.coins=75;view.Data.player.lastGiftUtc=0;yield return ClickArt("icon-plus");yield return ClickNamed("Visit Workshop");yield return ClickNamed("Claim 75");yield return ClickNamed("150 coins");yield return ClickNamed("Refill - 150 coins");
            if(view.Data.player.lives!=5||view.Data.player.coins!=0){Fail("Zero-life gift/refill recovery failed");yield break;}
            yield return ClickArt("icon-close");yield return ClickArt("icon-home");yield return ClickNamed("Play Level 7");
            if(!view.Data.player.challenge||view.Data.player.failed||view.Data.player.remainingSeconds<238){Fail("Refill did not restore timed entry");yield break;}
            yield return Capture("refilled-timed-720x1280");yield return Click(665,55);yield return ClickNamed("How to play");yield return Capture("timed-rules-help-720x1280");yield return ClickArt("icon-close");
            Screen.SetResolution(360,785,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);view.EnterHome();yield return Capture("home-map-tall-360x785");Screen.SetResolution(720,1280,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            // Review every major screen at the reported phone ratio and with simulated cutouts.
            foreach(var size in new[]{new Vector2Int(360,785),new Vector2Int(360,640),new Vector2Int(360,720),new Vector2Int(480,800),new Vector2Int(1080,1920),new Vector2Int(1080,2160),new Vector2Int(1272,2772)})
            {
                // Windows limits a window to the monitor height. Render the exact phone
                // ratio within that limit and supersample the capture to native pixels.
                captureScale=size.y>1400?2:1;int renderWidth=size.x/captureScale,renderHeight=size.y/captureScale;
                Screen.SetResolution(renderWidth,renderHeight,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
                if(Screen.width!=renderWidth||Screen.height!=renderHeight){Fail("Framebuffer did not match requested verification size");yield break;}
                view.SetVerificationSafeArea(new Rect(0,18f/captureScale,renderWidth,renderHeight-46f/captureScale));yield return new WaitForSeconds(.2f);
                string suffix=size.x+"x"+size.y+"-safe";
                Debug.Log("UI capture request "+suffix+"; actual framebuffer "+Screen.width+"x"+Screen.height+"; screenshot supersampling "+captureScale);
                view.LoadLevel("level-04");yield return Capture("layout-game-"+suffix);yield return ClickNamed("Learn");yield return Capture("layout-tutorial-"+suffix);
                view.LoadLevel("level-07");view.EnterHome();yield return Capture("layout-home-"+suffix);
                yield return ClickNamed("7");yield return Capture("layout-level-"+suffix);yield return ClickArt("icon-close");
                yield return ClickArt("avatar-7");yield return ClickNamed("Characters");yield return Capture("layout-profile-"+suffix);yield return ClickNamed("Frames");yield return Capture("layout-frames-"+suffix);yield return ClickNamed("Done");
                yield return ClickArt("icon-gear");yield return Capture("layout-settings-"+suffix);yield return ClickNamed("Done");
                yield return ClickArt("icon-plus",true);yield return Capture("layout-workshop-"+suffix);
                view.EnterHome();yield return ClickArt("icon-plus",true);yield return ClickNamed("150 coins");yield return Capture("layout-lives-"+suffix);yield return ClickArt("icon-close");
                yield return ClickArt("icon-trophy");yield return Capture("layout-collection-"+suffix);
                view.SetVerificationSafeArea(null);
            }
            captureScale=1;Screen.SetResolution(720,1280,FullScreenMode.Windowed);yield return new WaitForSeconds(.7f);
            if(layoutFailed){Fail("UI geometry regression; inspect captured layout screens");yield break;}
            foreach(var level in Content.All.Where(l=>l.id.StartsWith("level-"))){view.LoadLevel(level.id);foreach(var c in level.solution){if(!view.Commit(c)){Fail("Scene rejected trace "+level.id);yield break;}yield return null;}if(!Rules.Won(level,view.Data.session.state)){Fail("Scene trace did not finish "+level.id);yield break;}}
            view.LoadLevel("level-01");view.Data.player.remainingSeconds=30;int challengeWins=view.Data.player.challengeWins;int lifeBalance=view.Data.player.lives;
            foreach(var c in view.Level.solution){view.Commit(c);yield return null;}yield return new WaitForSeconds(1);view.Undo();
            if(!view.Data.player.challenge||!Rules.Won(view.Level,view.Data.session.state)||view.Data.player.lives!=lifeBalance||view.Data.player.challengeWins!=challengeWins+1){Fail("Completed result reopened as untimed or duplicated rewards");yield break;}
            yield return ClickNamed("Next level");
            if(view.Level.id!="level-02"||!view.Data.player.challenge||view.Data.player.remainingSeconds<178){Fail("Next level did not enter default timed play");yield break;}
            if(errors>0){Fail(errors+" runtime errors logged; inspect timed-default-player.log");yield break;}
            foreach(var file in Directory.GetFiles(Shots,"*.png")){var capture=new Texture2D(2,2);capture.LoadImage(File.ReadAllBytes(file));float dark=1,light=0;for(int y=0;y<capture.height;y+=31)for(int x=0;x<capture.width;x+=31){float value=capture.GetPixel(x,y).grayscale;dark=Mathf.Min(dark,value);light=Mathf.Max(light,value);}Destroy(capture);if(light-dark<.2f){Fail("Blank screenshot "+file);yield break;}}
            File.WriteAllText("Verification/scene-test.txt","PASS: actual Input System tap/drag, rejected drops, remembered rotation from lanes and reserves at the recording aspect ratio, undo, tutorial/hint and suspended puzzle restoration, queue inspection. All ten levels completed through the scene.\nPASS: home map, character/frame selection, profile name callback, daily gift exactly once, earned-coin purchases, life refill, timer boost, verified auto-fit, paused clock, cancelled/confirmed restart, timeout charging exactly once, actual save restoration, collection and tall home layout. Timed-only Play/Continue/Next Level, zero-life gates, gift/refill recovery, saved level clocks, tutorial exemption and finalized results verified.\nZero runtime errors. "+Directory.GetFiles(Shots,"*.png").Length+" nonblank captures under "+Shots+". UI import dimensions, aspect-preserving images, unsquashed corner borders and uniform stage scaling verified across portrait layouts and simulated safe areas. Android keyboard typing and device performance still need device QA.");Application.Quit();
        }
        Vector2 ScreenPoint(float x,float y){var safe=FindAnyObjectByType<WorkshopView>().VerificationSafeArea;float scale=Mathf.Min(safe.width/720f,safe.height/1280f);return safe.center+new Vector2(x-360,safe.height/scale*.5f-y)*scale;}
        Vector2 Anchor(WorkshopView v,int x,int y,bool lifted){var r=v.VerificationBoard;return new Vector2(r.x+(x+.2f)*r.width/4,r.y+(y+.2f)*r.width/4+(lifted?v.tuning.fingerOffset:0));}
        IEnumerator BoardClick(WorkshopView v,int x,int y){var p=Anchor(v,x,y,false);yield return Click(p.x,p.y);}
        IEnumerator BoardDrag(WorkshopView v,int source,int x,int y){var p=Anchor(v,x,y,true);yield return SourceDrag(v,source,p);}
        IEnumerator SourceClick(WorkshopView v,int source){var p=v.VerificationSource(source).center;yield return Click(p.x,p.y);}
        IEnumerator SourceDrag(WorkshopView v,int source,Vector2 target){var p=v.VerificationSource(source).center;yield return Drag(p.x,p.y,target.x,target.y);}
        IEnumerator Rotate(WorkshopView v){yield return Click(220,1190+v.VerificationExtra);}
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();ValidateGeometry(name);
            var titleSamples=new List<Vector2>();
            if(!FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Any(r=>r.gameObject.activeInHierarchy&&r.name=="Dialog"))
                foreach(var slot in FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r=>r.gameObject.activeInHierarchy&&r.name=="Lane title safe area"))
                    foreach(var p in new[]{new Vector2(slot.rect.xMin+2,slot.rect.yMin+2),new Vector2(slot.rect.xMin+2,slot.rect.yMax-2),new Vector2(slot.rect.xMax-2,slot.rect.yMin+2),new Vector2(slot.rect.xMax-2,slot.rect.yMax-2)})
                        titleSamples.Add(RectTransformUtility.WorldToScreenPoint(null,slot.TransformPoint(p))*captureScale);
            if(captureScale==1)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Shots+"/"+name+".png"));
            else
            {
                // Overlay UI is only enlarged by CaptureScreenshot(superSize).
                // A camera canvas renders font geometry at genuine native pixels.
                var canvas=FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c=>c.name=="Workshop Canvas");var scaler=canvas.GetComponent<CanvasScaler>();
                var view=FindAnyObjectByType<WorkshopView>();view.enabled=false;
                var cameraObject=new GameObject("Native UI capture camera");var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(216,231,255,255);camera.cullingMask=~(1<<30);
                var target=new RenderTexture(Screen.width*captureScale,Screen.height*captureScale,24);camera.targetTexture=target;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=100;scaler.scaleFactor=captureScale;canvas.scaleFactor=captureScale;
                Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();yield return new WaitForEndOfFrame();camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath(Shots+"/"+name+".png"),pixels.EncodeToPNG());RenderTexture.active=previous;
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;scaler.scaleFactor=1;canvas.scaleFactor=1;Canvas.ForceUpdateCanvases();view.enabled=true;Destroy(pixels);camera.targetTexture=null;target.Release();Destroy(target);Destroy(cameraObject);
            }
            yield return new WaitForSeconds(.35f);
            if(titleSamples.Count>0)
            {
                var screenshot=new Texture2D(2,2);screenshot.LoadImage(File.ReadAllBytes(Path.GetFullPath(Shots+"/"+name+".png")));
                foreach(var p in titleSamples)
                {
                    Color32 color=screenshot.GetPixel(Mathf.Clamp(Mathf.RoundToInt(p.x),0,screenshot.width-1),Mathf.Clamp(Mathf.RoundToInt(p.y),0,screenshot.height-1));
                    if(Mathf.Abs(color.r-46)>10||Mathf.Abs(color.g-102)>10||Mathf.Abs(color.b-195)>10){Debug.LogError(name+": lane title is outside dark blue fill; pixel "+p+" is "+color);layoutFailed=true;}
                }
                Destroy(screenshot);
            }
        }
        void ValidateGeometry(string screen)
        {
            foreach(var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.gameObject.activeInHierarchy))
            {
                text.ForceMeshUpdate();
                if(text.text.Contains("Free play")||text.text.Contains("Relaxed play")||text.text.Contains("Play relaxed")||text.text.Contains("without the clock")){Debug.LogError(screen+": obsolete untimed choice '"+text.text+"'");layoutFailed=true;}
                if(text.font==null||text.font.atlasRenderMode!=UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA){Debug.LogError(screen+": missing scalable font "+text.name);layoutFailed=true;}
                var input=text.GetComponentInParent<TMP_InputField>();
                // Editable names scroll horizontally inside their masked viewport.
                if(input!=null){if(input.textViewport==null||input.textViewport.GetComponent<RectMask2D>()==null){Debug.LogError(screen+": unmasked input text");layoutFailed=true;}continue;}
                if(text.text.StartsWith("LANE ")&&(text.transform.parent.name!="Lane title safe area"||text.transform.parent.GetComponent<RectMask2D>()==null)){Debug.LogError(screen+": lane title lacks its protected blue area");layoutFailed=true;}
                if(text.isTextOverflowing||text.isTextTruncated){Debug.LogError(screen+": overflowing text '"+text.text+"' in "+text.rectTransform.rect);layoutFailed=true;}
                var bounds=text.rectTransform.rect;
                foreach(var glyph in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible))
                    if(glyph.bottomLeft.x<bounds.xMin-1||glyph.topRight.x>bounds.xMax+1||glyph.bottomLeft.y<bounds.yMin-1||glyph.topRight.y>bounds.yMax+1){Debug.LogError(screen+": glyph outside text bounds '"+text.text+"'");layoutFailed=true;break;}
            }
            if(FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.gameObject.activeInHierarchy)){Debug.LogError(screen+": legacy bitmap UI text");layoutFailed=true;}
            foreach(var slot in FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r=>r.gameObject.activeInHierarchy&&(r.name=="Front piece slot"||r.name=="Queue piece slot")))
                foreach(var graphic in slot.GetComponentsInChildren<Graphic>())
                {
                    var corners=new Vector3[4];graphic.rectTransform.GetWorldCorners(corners);
                    if(corners.Any(c=>!new Rect(slot.rect.xMin-.1f,slot.rect.yMin-.1f,slot.rect.width+.2f,slot.rect.height+.2f).Contains(slot.InverseTransformPoint(c)))){Debug.LogError(screen+": piece preview escapes padded slot "+graphic.name);layoutFailed=true;}
                }
            foreach(var image in FindObjectsByType<Image>(FindObjectsSortMode.None).Where(i=>i.gameObject.activeInHierarchy&&i.sprite!=null&&i.name!="Surface"))
            {
                if(image.type==Image.Type.Simple&&!image.name.StartsWith("Full-screen")&&!image.preserveAspect){Debug.LogError(screen+": stretched simple image "+image.name);layoutFailed=true;}
                if(image.type==Image.Type.Sliced){var b=image.sprite.border/image.pixelsPerUnitMultiplier;var r=image.rectTransform.rect;if(b.x+b.z>r.width+.1f||b.y+b.w>r.height+.1f){Debug.LogError(screen+": compressed corner borders "+image.name);layoutFailed=true;}}
            }
            var stage=FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r=>r.name=="Safe portrait stage");
            if(Mathf.Abs(stage.localScale.x-stage.localScale.y)>.0001f){Debug.LogError(screen+": nonuniform stage scale");layoutFailed=true;}
            var background=FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r=>r.name=="Full-screen backdrop");var cornersBackground=new Vector3[4];background.GetWorldCorners(cornersBackground);
            if(cornersBackground[0].x>1||cornersBackground[0].y>1||cornersBackground[2].x<Screen.width-1||cornersBackground[2].y<Screen.height-1){Debug.LogError(screen+": uncovered screen background");layoutFailed=true;}
        }
        IEnumerator ClickArt(string name,bool last=false)
        {
            yield return null;Canvas.ForceUpdateCanvases();
            var images=FindObjectsByType<Image>(FindObjectsSortMode.None).Where(i=>i.gameObject.activeInHierarchy&&i.name==name&&i.GetComponentInParent<Button>()!=null).OrderBy(i=>RectTransformUtility.WorldToScreenPoint(null,i.rectTransform.position).x).ToArray();
            if(images.Length==0){Fail("Art button missing: "+name);yield break;}var button=(last?images.Last():images.First()).GetComponentInParent<Button>().GetComponent<RectTransform>();yield return ClickRaw(RectTransformUtility.WorldToScreenPoint(null,button.TransformPoint(button.rect.center)));
        }
        IEnumerator ClickNamed(string name)
        {
            yield return null;Canvas.ForceUpdateCanvases();
            var text=FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.gameObject.activeInHierarchy&&t.text==name&&t.GetComponentInParent<Button>()!=null).OrderBy(t=>t.transform.GetSiblingIndex()).LastOrDefault();if(text==null){Fail("Button not found: "+name);yield break;}var rt=text.GetComponentInParent<Button>().GetComponent<RectTransform>();yield return ClickRaw(RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center)));
        }
        IEnumerator Click(float x,float y){yield return ClickRaw(ScreenPoint(x,y));}
        IEnumerator ClickRaw(Vector2 point){yield return null;var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=0});yield return null;yield return null;yield return new WaitForSeconds(.08f);}
        IEnumerator Drag(float x,float y,float tx,float ty){var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(mouse,new MouseState{position=ScreenPoint(x,y),buttons=1});yield return null;yield return null;for(int i=1;i<=12;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=ScreenPoint(Mathf.Lerp(x,tx,i/12f),Mathf.Lerp(y,ty,i/12f)),buttons=1});yield return null;}InputSystem.QueueStateEvent(mouse,new MouseState{position=ScreenPoint(tx,ty),buttons=0});yield return null;yield return null;yield return new WaitForSeconds(.08f);}
        void Fail(string message){var view=FindAnyObjectByType<WorkshopView>();File.WriteAllText("Verification/scene-test.txt","FAIL: "+message+"\n"+(view==null?"":JsonUtility.ToJson(view.Data)));Debug.LogError(message);StartCoroutine(FailExit());}
        IEnumerator FailExit(){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Shots+"/failure.png"));yield return new WaitForSeconds(.4f);Application.Quit(1);}
    }
}
#endif
