using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using PocketToyworks.Core;

namespace PocketToyworks
{
    public sealed class PointerSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<PointerEventData> down,drag,up;
        public void OnPointerDown(PointerEventData e)=>down?.Invoke(e);
        public void OnDrag(PointerEventData e)=>drag?.Invoke(e);
        public void OnPointerUp(PointerEventData e)=>up?.Invoke(e);
    }
    public partial class WorkshopView : MonoBehaviour
    {
        public WorkshopTuning tuning;public GameObject robotPrefab;public Mesh roundedMesh;
        public Level Level {get;private set;} public SaveData Data {get;private set;}
        SaveStore save;Telemetry telemetry;RectTransform stage,content,overlay,ghost,backdrop;Image backdropImage;Sprite round;TMP_Text message;
        readonly Dictionary<string,int> poses=new Dictionary<string,int>();
        float height=1280;float Extra=>height-1280;
        Rect LayoutSafeArea {
            get {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(verificationSafeArea.HasValue)return verificationSafeArea.Value;
#endif
                return Screen.safeArea;
            }
        }
        float ReserveTop=>height-480;
        float LaneTop=>height-359;
        float BoardCell=>Data.session.state.boards.Length==1?Mathf.Min(126,100+Extra*.08f):74;
        Rect SourceRect(int source)=>source<3?new Rect(45+source*223,LaneTop+45,184,78):new Rect(36+(source-3)*332,ReserveTop,316,90);
        int Pose(string id)=>id!=null&&poses.TryGetValue(id,out var turn)?turn:0;
        void Remember(){var id=Rules.Front(Level,Data.session.state,selected);if(id!=null)poses[id]=rotation;}
        int selected=-1,rotation,revision; bool dragging,settings,levels,queueOpen,focused=true;int pointerId=int.MinValue;
        Vector2 press; CancellationTokenSource hintCancel; AudioSource musicSource; AudioSource audioSource; AudioClip placeClip,completeClip;
        string status; Camera toyCamera;RenderTexture toyTexture;GameObject toy;float revealUntil;int revealBoard=-1;string justPlaced;
        readonly List<Rect> boardRects=new List<Rect>(); readonly List<Rect> reserveRects=new List<Rect>();
        Color Ink=>tuning.ink;Color Teal=>tuning.teal;Color Paper=>new Color32(245,250,255,255);
        public void Start()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;Screen.orientation=ScreenOrientation.Portrait;
            if(tuning==null)tuning=ScriptableObject.CreateInstance<WorkshopTuning>();
            round=MakeRoundedSprite();
            string storage=Environment.GetCommandLineArgs().Contains("--verify-workshop")?System.IO.Path.GetFullPath("Verification/PlayerSave"):Application.persistentDataPath+"/PocketToyworksPrototype";
            save=new SaveStore(storage);telemetry=new Telemetry(storage);
            Data=save.Read(Content.Get)??new SaveData {session=new Session(Content.Get("tutorial-01"))};Level=Content.Get(Data.session.state.levelId);
            var canvas=new GameObject("Workshop Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            backdrop=Rect("Full-screen backdrop",canvas.transform,new Rect(0,0,Screen.width,Screen.height));backdropImage=backdrop.gameObject.AddComponent<Image>();backdropImage.raycastTarget=false;backdropImage.preserveAspect=true;
            stage=Rect("Safe portrait stage",canvas.transform,new Rect(0,0,720,height));stage.anchorMin=stage.anchorMax=new Vector2(.5f,.5f);stage.pivot=new Vector2(.5f,.5f);
            content=Rect("Game",stage,new Rect(0,0,720,height));overlay=Rect("Overlays",stage,new Rect(0,0,720,height));
            if(FindAnyObjectByType<EventSystem>()==null)new GameObject("Input",typeof(EventSystem),typeof(InputSystemUIInputModule));
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;placeClip=WorkshopAudio.Effect(false);completeClip=WorkshopAudio.Effect(true);
            musicSource=gameObject.AddComponent<AudioSource>();musicSource.clip=WorkshopAudio.Music();musicSource.loop=true;musicSource.volume=.16f;musicSource.Play();
            CreateToy();InitMeta();status=Level.teaching;Draw();Record("level_start");
        }
        void Update()
        {
            if(stage==null)return;var safe=LayoutSafeArea;if(safe.width<=0||safe.height<=0)return;float scale=Mathf.Min(safe.width/720f,safe.height/1280f);float nextHeight=safe.height/scale;bool resized=Mathf.Abs(nextHeight-height)>.5f;height=nextHeight;stage.sizeDelta=new Vector2(720,height);content.sizeDelta=overlay.sizeDelta=stage.sizeDelta;stage.localScale=Vector3.one*scale;
            stage.anchoredPosition=new Vector2(safe.center.x-Screen.width*.5f,safe.center.y-Screen.height*.5f);
            if(backdrop.sizeDelta!=new Vector2(Screen.width,Screen.height)){backdrop.sizeDelta=new Vector2(Screen.width,Screen.height);backdropImage.sprite=CoverAsset("background-"+(metaScreen=="game"?"game":"home"),Screen.width,Screen.height);}
            musicSource.mute=Data.settings.musicMuted||!focused; if(resized){pointerId=int.MinValue;dragging=false;Draw();}
            UpdateDimmer();TickMeta();
            if(focused&&Playing)Data.activeSeconds+=Time.unscaledDeltaTime;
            if(toy!=null&&revealUntil>Time.unscaledTime)
            {
                float t=1-(revealUntil-Time.unscaledTime)/tuning.revealSeconds;
                toy.transform.localScale=Vector3.one*(Data.settings.reducedMotion?1:Mathf.SmoothStep(.6f,1,t));
                toy.transform.rotation=Quaternion.Euler(0,Data.settings.reducedMotion?-12:Mathf.Lerp(18,-12,t),0);
            }
            if(revealUntil>0&&Time.unscaledTime>=revealUntil) {revealUntil=0;revealBoard=-1;Draw();}
        }
        void OnApplicationPause(bool pause) {focused=!pause;if(pause&&Data!=null){pointerId=int.MinValue;dragging=false;if(ghost!=null)Destroy(ghost.gameObject);Persist();}}
        void OnApplicationFocus(bool value) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(verificationFocus.HasValue)return;
#endif
            focused=value;
        }
        void OnApplicationQuit() {if(Data!=null){Record("level_exit");Persist();}}
        void OnDestroy() {if(titleMaterial!=null)Destroy(titleMaterial);hintCancel?.Cancel();if(toyTexture!=null)toyTexture.Release();}
        void Record(string name,string detail="")=>telemetry.Record(name,Level,Data,detail);
        void Persist() {if(!save.Write(Data))status="Save unavailable. Your current puzzle is still playable.";}
        public void LoadLevel(string id)
        {
            Record("level_exit");hintCancel?.Cancel();revision++;if(id!="tutorial-01"){Data.hasSuspendedSession=false;Data.suspendedSession=null;}Level=Content.Get(id);Data.session=new Session(Level);Data.activeSeconds=0;Data.actionCount=0;
            metaScreen="game";metaDialog="";Player.challenge=id!="tutorial-01";Player.challengeStarted=false;Player.remainingSeconds=Player.challenge?Progression.TimeLimit(id):0;Player.failed=false;Player.attemptCounted=false;completionCoins=0;
            poses.Clear();selected=-1;rotation=0;hintMove=null;levels=settings=queueOpen=false;status=Level.teaching;revealUntil=0;revealBoard=-1;Record("level_start");Persist();Draw();
        }
        public bool Commit(Command command)
        {
            if(Player.failed)return false;
            if(Level.id=="tutorial-01"&&!TutorialAllows(command)){Debug.Log("Tutorial rejected "+command+" expected "+Level.solution[Math.Min(Data.session.state.actions,3)]);status="Follow the highlighted step, or tap Undo to try it again.";Draw();return false;}
            int completed=Data.session.state.completed;string committingId=Rules.Front(Level,Data.session.state,command.source);
            if(!Data.session.Commit(Level,command)) {status="That shape doesn't fit here. Try another cell or turn.";Play(false);Draw();return false;}
            revision++;hintCancel?.Cancel();hintMove=null;Data.actionCount++;selected=-1;rotation=0;Record("move_commit",command.ToString());
            if(Level.id=="level-01"||command.reserve)Record("tutorial_step",command.reserve?"reserve_used":"first_fit");
            status=command.reserve?"Parked. The next part is ready to pick.":"A lovely fit. Keep building.";Play(true);
            if(Data.session.state.completed>completed) {revealUntil=Time.unscaledTime+tuning.revealSeconds;revealBoard=command.target;status="You fitted those parts. Meet Pip!";audioSource.PlayOneShot(completeClip,Data.settings.sound?.3f:0);}
            if(Rules.Won(Level,Data.session.state)&&!Data.completed.Contains(Level.id)) {Data.completed.Add(Level.id);Record("level_complete");}
            if(Rules.Won(Level,Data.session.state)&&!Player.attemptCounted){Player.attemptCounted=true;completionCoins=Progression.Reward(Player,Level.id);if(Player.challenge){Player.challengeWins++;double duration=Data.activeSeconds;if(Player.bestChallengeSeconds<=0||duration<Player.bestChallengeSeconds)Player.bestChallengeSeconds=Math.Max(.01,duration);}}
            if(Rules.Jammed(Level,Data.session.state)) {status="No forward moves. Undo a choice or choose Restart.";Record("jam_seen");}
            justPlaced=command.reserve?null:committingId;Persist();Draw();justPlaced=null;return true;
        }
        public void Undo()
        {
            if(Player.failed||Rules.Won(Level,Data.session.state)||!Data.session.Undo())return;
            revision++;hintCancel?.Cancel();hintMove=null;selected=-1;revealUntil=0;revealBoard=-1;Data.actionCount++;status=Player.challenge?"One choice back. The clock keeps running.":"One choice back. Everything is restored.";Record("undo");Persist();Draw();
        }
        void Select(int source)
        {
            if(string.IsNullOrEmpty(Rules.Front(Level,Data.session.state,source)))return;
            selected=source;rotation=Pose(Rules.Front(Level,Data.session.state,source));status="Tap a mould cell to place. Rotate changes the footprint.";Draw();
        }
        void Rotate() {if(selected<0){status="Choose a front piece first.";Draw();return;}hintMove=null;rotation=(rotation+1)%4;Remember();Play(true);Draw();}
        async void Hint()
        {
            if(Level.id=="tutorial-01"){if(Data.session.state.actions<Level.solution.Length)ShowHint(Level.solution[Data.session.state.actions]);Draw();return;}
            hintCancel?.Cancel();hintCancel=new CancellationTokenSource();var token=hintCancel.Token;int version=revision;
            var l=Level;var current=Data.session.state.Clone();var history=Data.session.history.Select(s=>s.Clone()).ToList();
            Record("hint_requested");status="Checking a complete solution…";Draw();
            try
            {
                var result=await Task.Run(()=>Solver.Search(l,current,100,15000,token));if(token.IsCancellationRequested||version!=revision)return;
                if(result.status==SearchStatus.Solved&&result.trace.Count>0) {status="Verified fit: "+result.trace[0];ShowHint(result.trace[0]);Record("hint_result","verified_current");}
                else
                {
                    int undo=history.Count;Command first=l.solution[0];
                    // Baseline snapshots are verified by replay, and remain a guaranteed recovery path even after a timeout.
                    var baseline=Rules.Start(l);var known=new Dictionary<string,Command>();
                    foreach(var c in l.solution){known[Rules.Key(baseline)]=c;Rules.Apply(l,baseline,c,out baseline);}
                    for(int i=history.Count-1;i>=0;i--)if(known.TryGetValue(Rules.Key(history[i]),out first)){undo=history.Count-i;break;}
                    if(undo==0&&known.TryGetValue(Rules.Key(current),out first)) {status="Verified fit: "+first;ShowHint(first);}
                    else status=(result.status==SearchStatus.Unknown?"Search budget reached. ":"No full solution from here. ")+"Undo "+undo+" move"+(undo==1?"":"s")+" for a verified route.";
                    Record("hint_result",result.status+";undo="+undo);
                }
                Draw();
            }
            catch(Exception e) {if(!token.IsCancellationRequested){status="Hint unavailable. Undo and restart remain free.";Debug.LogException(e);Draw();}}
        }
        Command? hintMove;
        void ShowHint(Command c){selected=c.source;rotation=c.rotation;Remember();hintMove=c;}
        void Draw()
        {
            if(content==null)return;backdropImage.sprite=CoverAsset("background-"+(metaScreen=="game"?"game":"home"),Screen.width,Screen.height);screenDimmer=null;ResetLiveLabels();Clear(content);Clear(overlay);boardRects.Clear();reserveRects.Clear();
            if(metaScreen!="game"){DrawMetaScreen();return;}
            GameHud();
            for(int b=0;b<Data.session.state.boards.Length;b++)DrawBoard(b);
            Panel(content,new Rect(36,ReserveTop-122,648,74),Paper,20);
            message=Label(content,Level.id=="tutorial-01"?TutorialText():status,new Rect(50,ReserveTop-116,620,62),20,Ink,FontStyle.Normal,TextAnchor.MiddleCenter);
            Label(content,"RESERVE",new Rect(36,ReserveTop-34,200,27),17,Teal,FontStyle.Bold);
            Label(content,"A little room to think",new Rect(295,ReserveTop-34,390,27),18,Ink,FontStyle.Normal,TextAnchor.MiddleRight);
            for(int r=0;r<2;r++)
            {
                int slot=r;var rect=new Rect(36+r*332,ReserveTop,316,90);reserveRects.Add(rect);Art(content,"tray",rect);
                string id=Data.session.state.reserves[r];
                if(string.IsNullOrEmpty(id)) {Label(content,"+    Park one piece",rect,20,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);var hit=Panel(content,rect,new Color(0,0,0,0),0);var p=hit.gameObject.AddComponent<PointerSurface>();p.down=e=>{if(pointerId==int.MinValue&&selected>=0&&selected<3)Commit(Command.Park(selected,slot));};}
                else {DrawPiece(content,id,new Rect(rect.x+20,rect.y+10,160,68),29,Pose(id),selected==r+3);Label(content,"SAVED "+(r+1),new Rect(rect.x+184,rect.y+27,115,35),16,Color.white,FontStyle.Bold);AttachPieceHit(rect,r+3);}
            }
            Label(content,"PARTS",new Rect(36,LaneTop-26,200,26),17,Teal,FontStyle.Bold);
            Label(content,"Pick the front • inspect the queue",new Rect(207,LaneTop-26,477,26),17,Ink,FontStyle.Normal,TextAnchor.MiddleRight);
            for(int lane=0;lane<3;lane++)DrawLane(lane);
            if(Level.id=="tutorial-01"&&Data.session.state.actions<Level.solution.Length){var step=Level.solution[Data.session.state.actions];var source=SourceRect(step.source);Outline(new Rect(source.x-4,source.y-4,source.width+8,source.height+8));if(step.reserve)Outline(reserveRects[step.target]);else {var br=boardRects[0];float c=br.width/4;foreach(var cell in Rules.Rotate(Level.Piece(Rules.Front(Level,Data.session.state,step.source)).cells,step.rotation))Outline(new Rect(br.x+(step.x+cell.x)*c+3,br.y+(step.y+cell.y)*c+3,c-6,c-6));}}
            GameTools();
            if(Rules.Won(Level,Data.session.state)&&revealUntil==0)DrawResult();
            if(settings)DrawSettings();if(levels)DrawLevels();if(queueOpen)DrawQueue();DrawMetaDialogs();
        }
        void Outline(Rect r){var color=new Color32(255,232,115,255);foreach(var edge in new[]{new Rect(r.x,r.y,r.width,3),new Rect(r.x,r.yMax-3,r.width,3),new Rect(r.x,r.y,3,r.height),new Rect(r.xMax-3,r.y,3,r.height)})Panel(content,edge,color,1).raycastTarget=false;}
        string TutorialText(){string[] steps={"1 / 4  •  Drag the yellow square into the glowing top-right space.","2 / 4  •  Drag the L-piece into the glowing reserve to park it.","3 / 4  •  Fit the blue square into the bottom-right space.","4 / 4  •  Select the saved L. Rotate to match the outline, then drag it into place."};return steps[Math.Min(Data.session.state.actions,3)];}
        bool TutorialAllows(Command c){int i=Data.session.state.actions;if(i>=Level.solution.Length)return false;var e=Level.solution[i];if(c.source!=e.source||c.reserve!=e.reserve||c.target!=e.target)return false;if(c.reserve)return true;var piece=Level.Piece(Rules.Front(Level,Data.session.state,c.source));return c.x==e.x&&c.y==e.y&&Rules.Rotate(piece.cells,c.rotation).SequenceEqual(Rules.Rotate(piece.cells,e.rotation));}
        void StartTutorial(){metaScreen="game";metaDialog="";settings=false;if(Level.id=="tutorial-01"){Draw();return;}Data.suspendedAttemptCounted=Player.attemptCounted;Data.suspendedChallenge=Player.challenge;Data.suspendedChallengeStarted=Player.challengeStarted;Data.suspendedRemaining=Player.remainingSeconds;Data.suspendedFailed=Player.failed;Data.hasSuspendedSession=true;Data.suspendedSession=Data.session;Data.suspendedSeconds=Data.activeSeconds;Data.suspendedActions=Data.actionCount;LoadLevel("tutorial-01");}
        void FinishTutorial(){if(!Data.hasSuspendedSession){LoadLevel("level-01");EnterHome();return;}Data.session=Data.suspendedSession;Data.hasSuspendedSession=false;Data.suspendedSession=null;Data.activeSeconds=Data.suspendedSeconds;Data.actionCount=Data.suspendedActions;Player.challenge=Data.suspendedChallenge;Player.challengeStarted=Data.suspendedChallengeStarted;Player.remainingSeconds=Data.suspendedRemaining;Player.failed=Data.suspendedFailed;Player.attemptCounted=Data.suspendedAttemptCounted;metaScreen="game";metaDialog="";Level=Content.Get(Data.session.state.levelId);poses.Clear();selected=-1;rotation=0;revealUntil=0;status="Welcome back. Your puzzle is just as you left it.";Persist();Draw();}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Rect? verificationSafeArea;
        bool? verificationFocus;
        public void SetVerificationFocus(bool? value){verificationFocus=value;if(value.HasValue)focused=value.Value;}
        public Rect VerificationSafeArea=>LayoutSafeArea;
        public void SetVerificationSafeArea(Rect? rect){verificationSafeArea=rect;}
        public Rect VerificationBoard=>boardRects[0];
        public float VerificationExtra=>Extra;
        public Rect VerificationSource(int source)=>SourceRect(source);
        public Rect VerificationQueue=>new Rect(45,LaneTop+116,184,86);
#endif
        void DrawBoard(int index)
        {
            var board=Data.session.state.boards[index];bool single=Data.session.state.boards.Length==1;float cell=BoardCell;
            var rect=new Rect(single?(720-cell*4)*.5f:36+index*336,252+Mathf.Max(0,(ReserveTop-148-252-cell*4)*.5f),cell*4,cell*4);boardRects.Add(rect);
            Art(content,"tray",new Rect(rect.x-12,rect.y-14,rect.width+24,rect.height+30));
            if(board.mould<0){Label(content,"ALL MADE",rect,23,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);return;}
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int k=y*4+x;var cr=new Rect(rect.x+x*cell+3,rect.y+y*cell+3,cell-6,cell-6);
                bool usable=(Level.moulds[board.mould].mask&(1<<k))!=0;
                Panel(content,cr,usable?new Color32(33,53,91,255):new Color32(64,87,130,255),11);
                if(!usable){Label(content,"·",cr,25,new Color32(135,179,172,255),FontStyle.Bold,TextAnchor.MiddleCenter);continue;}
                if(!string.IsNullOrEmpty(board.cells[k]))
                {
                    var pieceRoot=Rect("Placed plastic",content,cr);Plastic(pieceRoot,new Rect(0,0,cr.width,cr.height),tuning.accents[Level.Piece(board.cells[k]).color],false);
                    if(board.cells[k]==justPlaced&&!Data.settings.reducedMotion)StartCoroutine(ScaleFeedback(pieceRoot,.88f,1,tuning.snapSeconds));
                }
                else {Panel(content,new Rect(cr.x+3,cr.y+5,cr.width-6,cr.height-8),new Color32(167,191,219,255),8);}
            }
            if(hintMove.HasValue&&!hintMove.Value.reserve&&hintMove.Value.target==index&&selected>=0)DrawFootprint(content,rect,cell,hintMove.Value,true);
            var hit=Panel(content,rect,new Color(0,0,0,0),0);hit.gameObject.AddComponent<PointerSurface>().down=e=>{if(pointerId==int.MinValue&&selected>=0)PlaceAt(Local(e.position),false);};
            Label(content,"MOULD "+(board.mould+1)+"  /  PIP",new Rect(rect.x,rect.y-40,rect.width,25),16,Teal,FontStyle.Bold,TextAnchor.MiddleCenter);
            if(revealBoard==index&&revealUntil>Time.unscaledTime){var ri=Rect("Made from your parts",content,new Rect(rect.x-12,rect.y-38,rect.width+24,rect.height+60)).gameObject.AddComponent<RawImage>();ri.texture=toyTexture;ri.raycastTarget=false;}
        }
        void DrawLane(int lane)
        {
            float x=36+lane*223;var frame=new Rect(x,LaneTop,202,202);var panel=Art(content,"panel",frame);
            int head=Data.session.state.heads[lane];var q=Level.lanes[lane].pieces;
            // The sliced panel's blue fill begins 16.5 units below its top rim.
            // Keep the complete title inside that fill, separate from the front card.
            var heading=PieceSlot(panel.transform,new Rect(30,20,142,23),"Lane title safe area");
            Label(heading,"LANE "+(char)('A'+lane),new Rect(0,0,142,23),16,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
            if(head<q.Length)
            {
                var card=SourceRect(lane);var cardImage=Art(content,selected==lane?"button-gold":"button-white",card);
                var slot=PieceSlot(cardImage.transform,new Rect(18,13,card.width-36,card.height-30),"Front piece slot");DrawPiece(slot,q[head],new Rect(0,0,card.width-36,card.height-30),31,Pose(q[head]),selected==lane);AttachPieceHit(card,lane);
                for(int j=1;j<=2;j++)if(head+j<q.Length)DrawPiece(content,q[head+j],new Rect(x+12+(j-1)*91,LaneTop+126,80,23),17,0,false);
            }
            else Label(content,"All used",SourceRect(lane),21,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
            int l=lane;var inspect=Panel(content,new Rect(x+9,LaneTop+116,184,86),Color.clear,0);var inspectButton=inspect.gameObject.AddComponent<Button>();inspectButton.targetGraphic=inspect;inspectButton.onClick.AddListener(()=>{inspectedLane=l;queueOpen=true;Draw();});
            Label(content,(q.Length-head)+" left  ·  View",new Rect(x+15,LaneTop+151,172,27),18,Color.white,FontStyle.Bold,TextAnchor.MiddleCenter);
        }
        void AttachPieceHit(Rect rect,int source)
        {
            var hit=Panel(content,rect,new Color(0,0,0,0),0).gameObject.AddComponent<PointerSurface>();
            hit.down=e=>{if(pointerId!=int.MinValue)return;hintCancel?.Cancel();revision++;pointerId=e.pointerId;press=e.position;dragging=false;selected=source;rotation=Pose(Rules.Front(Level,Data.session.state,source));hintMove=null;DrawDrag(e.position);if(!Data.settings.reducedMotion&&ghost!=null)StartCoroutine(ScaleFeedback(ghost,1,1.015f,tuning.liftSeconds));};
            hit.drag=e=>{if(e.pointerId!=pointerId)return;if(Vector2.Distance(press,e.position)>8)dragging=true;if(dragging)DrawDrag(e.position);};
            hit.up=e=>{if(e.pointerId!=pointerId)return;pointerId=int.MinValue;if(dragging){var pos=Local(e.position);bool overReserve=false,accepted=false;int origin=selected,turn=rotation;for(int r=0;r<2;r++)if(reserveRects[r].Contains(pos)){accepted=Commit(Command.Park(selected,r));overReserve=true;break;}if(!overReserve)accepted=PlaceAt(pos,true);if(!accepted&&!Data.settings.reducedMotion)StartCoroutine(ReturnPiece(origin,turn,pos-new Vector2(0,tuning.fingerOffset)));}else {status="Tap a mould cell to place. Rotate changes the footprint.";Draw();}dragging=false;if(ghost!=null)Destroy(ghost.gameObject);};
        }
        Vector2 Local(Vector2 screen) {RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,screen,null,out var p);return new Vector2(p.x+360,height*.5f-p.y);}
        bool PlaceAt(Vector2 point,bool lifted)
        {
            if(selected<0)return false;if(lifted)point.y-=tuning.fingerOffset;hintMove=null;
            for(int b=0;b<boardRects.Count;b++)if(boardRects[b].Contains(point)) {var rect=boardRects[b];int x=Mathf.FloorToInt((point.x-rect.x)/(rect.width/4)),y=Mathf.FloorToInt((point.y-rect.y)/(rect.width/4));return Commit(Command.Place(selected,b,x,y,rotation));}
            status="Drop inside a mould, or use an empty reserve.";Draw();return false;
        }
        IEnumerator ReturnPiece(int source,int turn,Vector2 from)
        {
            string id=Rules.Front(Level,Data.session.state,source);if(string.IsNullOrEmpty(id))yield break;
            var returning=Rect("Invalid drop return",overlay,new Rect(from.x-70,from.y-45,140,90));DrawPiece(returning,id,new Rect(0,0,140,90),30,turn,true);
            foreach(var g in returning.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            var destination=SourceRect(source).center;
            float elapsed=0;while(returning!=null&&elapsed<tuning.returnSeconds){elapsed+=Time.unscaledDeltaTime;var p=Vector2.Lerp(from,destination,Mathf.SmoothStep(0,1,elapsed/tuning.returnSeconds));returning.anchoredPosition=new Vector2(p.x-70,-p.y+45);yield return null;}
            if(returning!=null)Destroy(returning.gameObject);
        }
        void DrawDrag(Vector2 screen)
        {
            if(ghost!=null)Destroy(ghost.gameObject);ghost=Rect("Exact placement ghost",overlay,new Rect(0,0,720,height));
            var p=Local(screen);p.y-=tuning.fingerOffset;bool onBoard=false;
            for(int b=0;b<boardRects.Count;b++)if(boardRects[b].Contains(p)) {var rect=boardRects[b];float cell=rect.width/4;var c=Command.Place(selected,b,Mathf.FloorToInt((p.x-rect.x)/cell),Mathf.FloorToInt((p.y-rect.y)/cell),rotation);DrawFootprint(ghost,rect,cell,c,Rules.Legal(Level,Data.session.state,c));onBoard=true;}
            if(!onBoard&&selected>=0)DrawPiece(ghost,Rules.Front(Level,Data.session.state,selected),new Rect(p.x,p.y,160,100),32,rotation,true);
            foreach(var graphic in ghost.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        }
        void DrawFootprint(Transform parent,Rect board,float cell,Command c,bool legal)
        {
            var id=Rules.Front(Level,Data.session.state,c.source);if(string.IsNullOrEmpty(id))return;
            foreach(var p in Rules.Rotate(Level.Piece(id).cells,c.rotation)) {var r=new Rect(board.x+(c.x+p.x)*cell+4,board.y+(c.y+p.y)*cell+4,cell-8,cell-8);Panel(parent,r,legal?new Color(.3f,.9f,.7f,.78f):new Color(1,.36f,.32f,.78f),9);Art(parent,"icon-"+(legal?"fit":"close"),new Rect(r.x+r.width*.27f,r.y+r.height*.27f,r.width*.46f,r.height*.46f));}
        }
        void DrawPiece(Transform parent,string id,Rect area,float cell,int turn,bool outline)
        {
            if(string.IsNullOrEmpty(id))return;var p=Level.Piece(id);var shape=Rules.Rotate(p.cells,turn);float inset=outline?6:0;cell=Mathf.Min(cell,(area.width-inset)/(shape.Max(c=>c.x)+1),(area.height-inset)/(shape.Max(c=>c.y)+1));float w=(shape.Max(c=>c.x)+1)*cell,h=(shape.Max(c=>c.y)+1)*cell;
            foreach(var c in shape)Plastic(parent,new Rect(area.center.x-w/2+c.x*cell,area.center.y-h/2+c.y*cell,cell-2,cell-2),tuning.accents[p.color],outline);
        }
        void Plastic(Transform parent,Rect r,Color color,bool outline)
        {
            if(outline)Panel(parent,new Rect(r.x-2,r.y-2,r.width+4,r.height+6),Color.white,8);
            int index=Array.FindIndex(tuning.accents,c=>c==color);Art(parent,"block-"+Math.Max(0,index),r);
        }
        void Play(bool valid)
        {
            if(Data.settings.sound)audioSource.PlayOneShot(placeClip,valid?.22f:.08f);
            if(valid&&Data.settings.haptics)Haptics.Pulse();
        }
        IEnumerator ScaleFeedback(Transform target,float from,float to,float duration)
        {
            float elapsed=0;while(target!=null&&elapsed<duration){elapsed+=Time.unscaledDeltaTime;target.localScale=Vector3.one*Mathf.SmoothStep(from,to,elapsed/duration);yield return null;}if(target!=null)target.localScale=Vector3.one*to;
        }
        void CreateToy()
        {
            toyTexture=new RenderTexture(640,640,24){name="Pip portrait"};
            var cameraGO=new GameObject("Toy portrait camera");cameraGO.transform.SetParent(transform);toyCamera=cameraGO.AddComponent<Camera>();toyCamera.transform.position=new Vector3(0,1,-7);toyCamera.transform.rotation=Quaternion.Euler(7,0,0);toyCamera.orthographic=true;toyCamera.orthographicSize=2.4f;toyCamera.clearFlags=CameraClearFlags.SolidColor;toyCamera.backgroundColor=Color.clear;toyCamera.targetTexture=toyTexture;toyCamera.cullingMask=1<<30;
            if(robotPrefab!=null){toy=Instantiate(robotPrefab,new Vector3(0,0,0),Quaternion.Euler(0,-12,0));foreach(var child in toy.GetComponentsInChildren<Transform>())child.gameObject.layer=30;}
        }
        RectTransform Rect(string name,Transform parent,Rect rect)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size;return rt;
        }
        Image Panel(Transform parent,Rect rect,Color color,float radius=12)
        {
            var image=Rect("Surface",parent,rect).gameObject.AddComponent<Image>();image.color=color;if(radius>0){image.sprite=round;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=24/radius;}return image;
        }
        void Button(Transform parent,string title,Rect rect,Action action,Color color,Color textColor,int size=23)
        {
            Panel(parent,new Rect(rect.x,rect.y+5,rect.width,rect.height),Color.Lerp(color,Ink,.28f));
            var panel=Panel(parent,rect,color);var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel;button.onClick.AddListener(()=>{if(pointerId==int.MinValue)action();});Label(panel.transform,title,new Rect(8,0,rect.width-16,rect.height),size,textColor,FontStyle.Bold,TextAnchor.MiddleCenter);
        }
        void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--) {parent.GetChild(i).gameObject.SetActive(false);Destroy(parent.GetChild(i).gameObject);}}
        static Sprite MakeRoundedSprite()
        {
            int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=Mathf.Max(0,Mathf.Abs(x-31.5f)-8),dy=Mathf.Max(0,Mathf.Abs(y-31.5f)-8);float a=Mathf.Clamp01(24-Mathf.Sqrt(dx*dx+dy*dy));texture.SetPixel(x,y,new Color(1,1,1,a));}texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(25,25,25,25));
        }
    }
}
