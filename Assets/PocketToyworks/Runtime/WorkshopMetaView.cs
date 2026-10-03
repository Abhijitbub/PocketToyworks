using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using PocketToyworks.Core;
namespace PocketToyworks
{
    public partial class WorkshopView
    {
        readonly Dictionary<string,Sprite> art=new Dictionary<string,Sprite>();
        string metaScreen="game",metaDialog="",profileTab="avatar",leaveAction="home",metaNotice="";
        int pickedLevel=1,completionCoins;float metaTick,mapPosition=-1;
        TMP_Text lifeLabel,lifeClockLabel,coinLabel,challengeLabel;bool fitting;Image screenDimmer;
        PlayerProgress Player=>Data.player;
        static readonly string[] Crew={"Pip","Nova","Milo","Sunny","Bolt","Luna","Ember","Cloud"};
        static readonly string[] Frames={"Sky","Gold","Royal","Candy","Leaf","Pearl"};
        void InitMeta(){Progression.Ensure(Data,DateTime.UtcNow);InitTypography();metaScreen=Level.id=="tutorial-01"?"game":"home";Persist();}
        bool Playing=>metaScreen=="game"&&!settings&&!levels&&!queueOpen&&metaDialog==""&&!Player.failed&&!Rules.Won(Level,Data.session.state);
        void TickMeta()
        {
            if(Player==null)return;
            if(Playing&&focused&&Player.challenge&&Level.id!="tutorial-01")
            {
                if(!Player.challengeStarted){Player.challengeStarted=true;Persist();}
                Player.remainingSeconds=Math.Max(0,Player.remainingSeconds-Time.unscaledDeltaTime);
                if(Player.remainingSeconds<=0){Player.failed=true;Progression.LoseLife(Player,DateTime.UtcNow);pointerId=int.MinValue;hintCancel?.Cancel();Persist();Draw();}
            }
            if(Time.unscaledTime<metaTick)return;metaTick=Time.unscaledTime+1;
            if(Progression.Refresh(Player,DateTime.UtcNow)){Persist();Draw();}
            if(lifeLabel!=null)lifeLabel.text=Player.lives.ToString();
            if(lifeClockLabel!=null)lifeClockLabel.text=Player.lives==5?"FULL":TimeText(Progression.NextLifeSeconds(Player,DateTime.UtcNow));
            if(coinLabel!=null)coinLabel.text=Player.coins.ToString();
            if(challengeLabel!=null)challengeLabel.text=Player.challenge?TimeText((int)Math.Ceiling(Player.remainingSeconds)):Data.session.state.completed+" / "+Level.moulds.Length+" TOYS";
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(metaDialog!=""){metaDialog="";Draw();}else if(metaScreen=="game"){settings=!settings;Draw();}}
        }
        static string TimeText(int seconds)=>Math.Max(0,seconds/60).ToString("00")+":"+Math.Max(0,seconds%60).ToString("00");
        Sprite ArtAsset(string name)
        {
            if(art.TryGetValue(name,out var sprite))return sprite;
            var texture=Resources.Load<Texture2D>("UI/"+name);if(texture==null)return round;
            Vector4 border=name.StartsWith("button-")?new Vector4(56,56,56,56):name=="panel"?new Vector4(64,64,64,64):name=="tray"?new Vector4(36,36,36,36):Vector4.zero;
            sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);art[name]=sprite;return sprite;
        }
        Image Art(Transform parent,string name,Rect rect,bool raycast=false)
        {
            var image=Rect(name,parent,rect).gameObject.AddComponent<Image>();image.sprite=name.StartsWith("background-")?CoverAsset(name,rect.width,rect.height):ArtAsset(name);image.raycastTarget=raycast;
            if(image.sprite.border!=Vector4.zero)
            {
                image.type=Image.Type.Sliced;
                // Scale corner pixels uniformly with a button's height; only straight
                // middle sections extend horizontally. Panels and trays keep fixed rims.
                image.pixelsPerUnitMultiplier=name.StartsWith("button-")?160f/rect.height:name=="panel"?2f:1.4f;
            }
            else image.preserveAspect=true;
            return image;
        }
        Sprite CoverAsset(string name,float width,float height)
        {
            string key=name+"@"+Mathf.RoundToInt(width)+"x"+Mathf.RoundToInt(height);
            if(art.TryGetValue(key,out var sprite))return sprite;
            var source=ArtAsset(name);var rect=source.rect;float aspect=width/height;
            if(rect.width/rect.height>aspect){float w=rect.height*aspect;rect.x+=(rect.width-w)*.5f;rect.width=w;}
            else{float h=rect.width/aspect;rect.y+=(rect.height-h)*.5f;rect.height=h;}
            sprite=Sprite.Create(source.texture,rect,new Vector2(.5f,.5f),100);art[key]=sprite;return sprite;
        }
        void GButton(Transform parent,string title,Rect rect,Action action,string color="blue",int size=28)
        {
            var image=Art(parent,"button-"+color,rect,true);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{if(pointerId==int.MinValue){Play(true);action();}});
            Title(image.transform,title,new Rect(13,0,rect.width-26,rect.height-7),size);
        }
        void IconButton(Transform parent,string icon,Rect r,Action action,string color="blue")
        {
            var image=Art(parent,"round-"+color,r,true);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{if(pointerId==int.MinValue){Play(true);action();}});
            Art(image.transform,"icon-"+icon,new Rect(r.width*.23f,r.height*.2f,r.width*.54f,r.height*.54f));
        }
        void Portrait(Transform parent,int avatar,int frame,Rect r,Action click=null)
        {
            float side=Mathf.Min(r.width,r.height);r=new Rect(r.center.x-side*.5f,r.center.y-side*.5f,side,side);float inset=side*.0625f;
            var image=Art(parent,"avatar-"+avatar,new Rect(r.x+inset,r.y+inset,side-inset*2,side-inset*2),click!=null);
            Art(parent,"frame-"+frame,r);
            if(click!=null){var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>{if(pointerId==int.MinValue)click();});}
        }
        RectTransform Sheet(string title,float y=170,float size=925,Action close=null,string plaque="blue")
        {
            var screenTop=Local(new Vector2(0,Screen.height));float scale=stage.localScale.x;
            screenDimmer=Panel(overlay,new Rect(screenTop.x,screenTop.y,Screen.width/scale,Screen.height/scale),new Color(.06f,.09f,.20f,.66f),0);
            var root=Rect("Dialog",overlay,new Rect(0,Extra*.5f,720,1280));Art(root,"panel",new Rect(35,y,650,size),true);
            Art(root,"button-"+plaque,new Rect(151,y-25,418,106));Title(root,title,new Rect(170,y-19,380,85),38);
            if(close!=null)IconButton(root,"close",new Rect(610,y-17,79,79),close,"pink");return root;
        }
        void UpdateDimmer()
        {
            if(screenDimmer==null)return;var top=Local(new Vector2(0,Screen.height));float scale=stage.localScale.x;
            screenDimmer.rectTransform.anchoredPosition=new Vector2(top.x,-top.y);
            screenDimmer.rectTransform.sizeDelta=new Vector2(Screen.width/scale,Screen.height/scale);
        }
        void ResetLiveLabels(){lifeLabel=lifeClockLabel=coinLabel=challengeLabel=null;}
        void MainHeader(string title)
        {
            Portrait(content,Player.avatar,Player.frame,new Rect(18,18,100,103),()=>{metaDialog="profile";Draw();});
            Art(content,"button-white",new Rect(129,30,213,75));Art(content,"icon-heart",new Rect(276,36,62,62));lifeLabel=Title(content,Player.lives.ToString(),new Rect(285,49,44,37),28);
            lifeClockLabel=Label(content,Player.lives==5?"FULL":TimeText(Progression.NextLifeSeconds(Player,DateTime.UtcNow)),new Rect(164,43,105,46),28,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            IconButton(content,"plus",new Rect(127,78,41,41),()=>{metaDialog="lives";Draw();},"green");
            Art(content,"button-white",new Rect(367,30,224,75));Art(content,"icon-coin",new Rect(379,39,57,57));coinLabel=Label(content,Player.coins.ToString(),new Rect(434,42,145,47),31,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            IconButton(content,"plus",new Rect(562,77,41,41),()=>{metaScreen="shop";Draw();},"green");
            IconButton(content,"gear",new Rect(620,25,79,79),()=>{settings=true;Draw();});
            Title(content,title,new Rect(190,132,350,56),38);
            GButton(content,"Learn",new Rect(20,139,135,58),StartTutorial,"blue",23);
            IconButton(content,"gift",new Rect(608,125,72,72),()=>{metaDialog="gift";Draw();},"pink");
        }
        void MainNavigation()
        {
            Art(content,"button-blue",new Rect(-20,height-115,760,125));
            string[] icons={"home","coin","trophy","profile"},names={"Home","Workshop","Toys","Profile"};
            for(int i=0;i<4;i++){int tab=i;float x=16+i*179;IconButton(content,icons[i],new Rect(x+48,height-117,80,80),()=>{settings=false;metaDialog="";if(tab==3)metaDialog="profile";else metaScreen=tab==0?"home":tab==1?"shop":"collection";Draw();},(metaScreen=="home"&&i==0)||(metaScreen=="shop"&&i==1)||(metaScreen=="collection"&&i==2)?"green":"blue");Title(content,names[i],new Rect(x,height-36,172,27),19);}
        }
        void DrawMetaScreen()
        {
            if(metaScreen=="home")DrawHome();else if(metaScreen=="shop")DrawShop();else DrawCollection();
            MainNavigation();DrawMetaDialogs();if(settings)DrawSettings();
        }
        void DrawHome()
        {
            var viewport=Rect("Level trail viewport",content,new Rect(0,213,720,height-518));viewport.gameObject.AddComponent<RectMask2D>();var surface=viewport.gameObject.AddComponent<Image>();surface.color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;scroll.viewport=viewport;
            var trail=Rect("Ten-level toy trail",viewport,new Rect(0,0,720,2220));scroll.content=trail;
            Panel(trail,new Rect(342,70,12,2050),new Color32(240,163,27,255),5).raycastTarget=false;Panel(trail,new Rect(366,70,7,2050),new Color32(255,235,138,255),3).raycastTarget=false;
            int current=Progression.CurrentLevel(Data);
            for(int n=1;n<=10;n++)
            {
                int target=n;bool won=Data.completed.Contains("level-"+n.ToString("00")),open=Progression.Unlocked(Data,n);float x=271+(n%3-1)*48,y=(10-n)*210+45;
                var node=Art(trail,"node-"+(won?"gold":n==current?"green":"purple"),new Rect(x,y,178,178),true);var button=node.gameObject.AddComponent<Button>();button.targetGraphic=node;button.onClick.AddListener(()=>{pickedLevel=target;metaDialog="level";Draw();});
                Title(node.transform,n.ToString(),new Rect(0,38,178,87),65);if(!open)Art(node.transform,"icon-lock",new Rect(-17,98,69,72));if(won)Art(node.transform,"icon-star",new Rect(130,118,53,53));
            }
            scroll.verticalNormalizedPosition=mapPosition>=0?mapPosition:(current-1)/9f;scroll.onValueChanged.AddListener(v=>mapPosition=v.y);
            string active=Level.id.StartsWith("level-")&&!Rules.Won(Level,Data.session.state)&&!Player.failed?Level.id:"level-"+current.ToString("00");int.TryParse(active.Replace("level-",""),out int activeNumber);
            GButton(content,(active==Level.id&&!Player.failed&&!Rules.Won(Level,Data.session.state)&&(Data.session.state.actions>0||Player.challengeStarted)?"Continue":"Play")+" Level "+activeNumber,new Rect(109,height-251,502,102),()=>SelectLevel(active),"green",37);
            Label(content,"Drag the trail to explore your next toys",new Rect(80,height-290,560,28),19,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
            MainHeader("TOY TRAIL");
        }
        void DrawShop()
        {
            MainHeader("WORKSHOP");
            Art(content,"panel",new Rect(30,225,660,174));Art(content,"icon-gift",new Rect(58,257,97,97));Title(content,"Daily gift",new Rect(166,251,240,42),30,TextAnchor.MiddleLeft);Label(content,"75 coins, once every 24 hours",new Rect(166,298,240,59),20,Color.white);
            GButton(content,Progression.GiftAvailable(Player,DateTime.UtcNow)?"Claim 75":"Claimed",new Rect(447,267,204,87),ClaimGift,"green",27);
            Title(content,"BOOST YOUR BUILD",new Rect(35,422,650,38),27);
            for(int i=0;i<2;i++){bool freeze=i==0;float x=33+i*334;Art(content,"panel",new Rect(x,475,320,279));Art(content,"icon-"+(freeze?"freeze":"fit"),new Rect(x+107,495,100,100));Title(content,freeze?"Time +30s x2":"Auto-fit x1",new Rect(x+18,595,284,41),27);Label(content,freeze?"Unlocks at Lv.4":"A verified fit - unlocks at Lv.7",new Rect(x+21,639,278,31),17,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(content,freeze?"50 coins":"80 coins",new Rect(x+30,677,260,61),()=>{metaNotice=Progression.BuyBooster(Player,freeze)?"Added to your tools!":"You need more coins. Claim gifts or complete new levels.";Persist();Draw();},"green",23);}
            Art(content,"panel",new Rect(30,780,660,178));Art(content,"icon-heart",new Rect(65,811,100,100));Title(content,"Lives",new Rect(181,805,264,48),28,TextAnchor.MiddleLeft);Label(content,"One life every 30 min",new Rect(181,855,264,40),21,Color.white);GButton(content,"150 coins",new Rect(455,823,197,85),()=>{metaDialog="lives";Draw();},"green",25);
            GButton(content,"Choose a profile frame",new Rect(67,985,586,78),()=>{profileTab="frame";metaDialog="profile";Draw();},"purple",29);
            Label(content,metaNotice==""?"Earn coins by completing new levels and collecting gifts.":metaNotice,new Rect(40,1070,640,62),21,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
        }
        void DrawCollection()
        {
            MainHeader("TOY SHELF");int count=Data.completed.Count(id=>id.StartsWith("level-"));Title(content,count+" / 10 toys made",new Rect(40,211,640,37),26);
            for(int n=1;n<=10;n++){int target=n;string id="level-"+n.ToString("00");bool won=Data.completed.Contains(id);float x=30+((n-1)%2)*337,y=266+((n-1)/2)*153;
                Art(content,"button-"+(won?"blue":"purple"),new Rect(x,y,323,140));var portrait=Art(content,"avatar-0",new Rect(x+15,y+18,95,95));if(!won)portrait.color=new Color(.3f,.36f,.6f,1);Title(content,"Order "+n,new Rect(x+118,y+16,190,43),27,TextAnchor.MiddleLeft);Label(content,won?Content.Get(id).title:"Make this toy to collect it",new Rect(x+118,y+61,183,59),19,Color.white);IconButton(content,won?"play":"lock",new Rect(x+268,y+92,49,49),()=>{pickedLevel=target;metaDialog="level";Draw();},won?"green":"purple");}
            Label(content,"Every completed order earns a place on your shelf.",new Rect(45,1051,630,61),22,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
        }
        void DrawMetaDialogs()
        {
            if(metaDialog=="profile")DrawProfile();if(metaDialog=="lives")DrawLives();if(metaDialog=="level")DrawLevelCard();if(metaDialog=="leave")DrawLeave();if(metaDialog=="gift")DrawGift();if(metaDialog=="help")DrawHelp();
            if(metaScreen=="game"&&Player.failed&&metaDialog=="")DrawTimeUp();
        }
        void CloseMeta(){metaDialog="";metaNotice="";Draw();}
        void DrawProfile()
        {
            var root=Sheet("My profile",170,925,CloseMeta);Art(root,"button-white",new Rect(77,277,566,127));Portrait(root,Player.avatar,Player.frame,new Rect(84,284,111,113));
            var inputRoot=Rect("Player name",root,new Rect(218,309,358,62));var background=inputRoot.gameObject.AddComponent<Image>();background.color=Color.clear;var field=inputRoot.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=background;
            var viewport=Rect("Name viewport",inputRoot,new Rect(0,0,358,62));viewport.gameObject.AddComponent<RectMask2D>();
            var value=Label(viewport,Player.playerName,new Rect(0,0,358,62),32,Ink,FontStyle.Bold);value.richText=false;value.textWrappingMode=TextWrappingModes.NoWrap;value.overflowMode=TextOverflowModes.Overflow;field.textViewport=viewport;field.textComponent=value;field.text=Player.playerName;field.characterLimit=16;field.lineType=TMP_InputField.LineType.SingleLine;field.onEndEdit.AddListener(name=>{Player.playerName=Progression.CleanName(name);field.SetTextWithoutNotify(Player.playerName);Persist();});IconButton(root,"pencil",new Rect(577,306,59,59),()=>field.ActivateInputField());
            Label(root,Data.completed.Count(id=>id.StartsWith("level-"))+" toys made  •  "+Player.challengeWins+" challenge wins",new Rect(78,416,562,34),22,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
            GButton(root,"Characters",new Rect(78,465,274,67),()=>{profileTab="avatar";Draw();},profileTab=="avatar"?"green":"blue",27);GButton(root,"Frames",new Rect(369,465,274,67),()=>{profileTab="frame";Draw();},profileTab=="frame"?"green":"blue",27);
            if(profileTab=="avatar")for(int i=0;i<8;i++){int avatar=i;float x=79+(i%4)*146,y=559+(i/4)*190;Portrait(root,i,Player.frame,new Rect(x,y,128,132),()=>{Player.avatar=avatar;Persist();Draw();});Title(root,Crew[i],new Rect(x,y+139,128,35),22);if(Player.avatar==i)Art(root,"icon-fit",new Rect(x+90,y+93,39,39));}
            else for(int i=0;i<6;i++){int frame=i;bool owned=Player.ownedFrames.Contains(i);float x=79+(i%3)*191,y=557+(i/3)*194;Portrait(root,Player.avatar,i,new Rect(x+18,y,140,141),()=>{if(Player.ownedFrames.Contains(frame)||Progression.BuyFrame(Player,frame)){Player.frame=frame;metaNotice="";}else metaNotice="Not enough coins for this frame.";Persist();Draw();});Title(root,Frames[i]+(owned?"":" - "+Progression.FrameCost(i)),new Rect(x,y+148,176,32),21);if(Player.frame==i)Art(root,"icon-fit",new Rect(x+124,y+100,38,38));}
            Label(root,metaNotice==""?"Tap your name to edit. Pick a character and frame.":metaNotice,new Rect(81,958,558,41),20,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(root,"Done",new Rect(124,1005,472,69),CloseMeta,"green",29);
        }
        void DrawLives()
        {
            var root=Sheet("Your lives",230,830,CloseMeta,"pink");Art(root,"icon-heart",new Rect(252,344,215,211));lifeLabel=Title(root,Player.lives.ToString(),new Rect(270,400,179,108),80);
            Title(root,Player.lives==5?"Your hearts are full!":"Next free life",new Rect(90,575,540,45),31);lifeClockLabel=Title(root,Player.lives==5?"FULL":TimeText(Progression.NextLifeSeconds(Player,DateTime.UtcNow)),new Rect(90,630,540,52),41);
            Label(root,"Restart or timeout uses one life.\nOne life returns every 30 minutes.",new Rect(83,700,554,70),22,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(root,"Refill - 150 coins",new Rect(90,788,540,76),RefillLives,Player.lives==5?"purple":"green",28);
            GButton(root,"Visit Workshop",new Rect(90,879,540,76),()=>{metaDialog="";metaScreen="shop";settings=false;Draw();},"blue",28);Label(root,metaNotice,new Rect(81,978,558,45),20,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
        }
        void RefillLives(){metaNotice=Progression.Refill(Player,DateTime.UtcNow)?"All five lives restored!":Player.lives==5?"Your lives are already full.":"Not enough coins. Collect a gift or wait for a free life.";Persist();Draw();}
        void DrawGift()
        {
            var root=Sheet("Daily surprise",275,710,CloseMeta,"pink");Art(root,"icon-gift",new Rect(238,404,244,235));Title(root,"75 workshop coins",new Rect(82,659,556,55),36);Label(root,"A little gift for your next creation.\nReturn in 24 hours for another surprise.",new Rect(90,730,540,80),23,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(root,Progression.GiftAvailable(Player,DateTime.UtcNow)?"Collect gift":"Collected",new Rect(112,843,496,82),ClaimGift,"green",31);
        }
        void ClaimGift(){metaNotice=Progression.ClaimGift(Player,DateTime.UtcNow)?"75 coins added!":"Your next gift arrives 24 hours after your last claim.";Persist();if(metaDialog=="gift")metaDialog="";Draw();}
        void DrawLevelCard()
        {
            var root=Sheet("Level "+pickedLevel,230,825,CloseMeta);bool unlocked=Progression.Unlocked(Data,pickedLevel);string id="level-"+pickedLevel.ToString("00");var level=Content.Get(id);
            Portrait(root,0,1,new Rect(254,353,212,216));Title(root,level.title,new Rect(76,598,568,52),32);Label(root,level.pieces.Length+" pieces  •  "+level.moulds.Length+" toy mould"+(level.moulds.Length==1?"":"s"),new Rect(88,660,544,39),23,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
            Label(root,unlocked?(Player.rewardedLevels.Contains(id)?"Toy collected - replay for fun":"First clear earns 50 coins"):"Complete level "+Math.Max(1,pickedLevel-1)+" to unlock this toy.",new Rect(82,709,556,48),23,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
            bool resume=id==Level.id&&!Player.failed&&!Rules.Won(Level,Data.session.state)&&(Player.challengeStarted||Data.session.state.actions>0)||Progression.Paused(Data,id,true)!=null;
            if(unlocked){Art(root,"icon-clock",new Rect(179,786,63,63));Title(root,"Time limit: "+TimeText(Progression.TimeLimit(id)),new Rect(255,792,320,50),32,TextAnchor.MiddleLeft);GButton(root,resume?"Continue level":"Play level",new Rect(101,889,518,84),()=>SelectLevel(id),"green",32);}else Art(root,"icon-lock",new Rect(313,805,94,103));
            if(unlocked)Label(root,"Lives: "+Player.lives+"  •  Undo does not restore time",new Rect(82,981,556,48),20,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
        }
        void SelectLevel(string id)
        {
            if(id==Level.id&&Player.challenge&&!Rules.Won(Level,Data.session.state)&&!Player.failed&&(Player.challengeStarted||Data.session.state.actions>0)){metaScreen="game";metaDialog="";settings=false;Draw();return;}
            BeginLevel(id);
        }
        void BeginLevel(string id,bool restart=false)
        {
            int.TryParse(id.Replace("level-",""),out int n);if(n>0&&!Progression.Unlocked(Data,n)){metaNotice="Complete the previous toy first.";Draw();return;}
            Progression.Refresh(Player,DateTime.UtcNow);var paused=restart?null:Progression.Paused(Data,id,true);
            if(!Progression.CanBegin(Player,paused!=null)){metaDialog="lives";Draw();return;}
            if(restart)Progression.ForgetAttempt(Data,id,true);else Progression.CacheAttempt(Data,Level);
            LoadLevel(id);
            if(paused!=null){Progression.RestoreAttempt(Data,id,true);status="Welcome back. Your puzzle and clock were saved.";}
            metaScreen="game";metaDialog="";completionCoins=0;Persist();Draw();
        }
        public void EnterHome(){hintCancel?.Cancel();revision++;pointerId=int.MinValue;dragging=false;selected=-1;metaScreen="home";metaDialog="";settings=levels=queueOpen=false;mapPosition=-1;Persist();Draw();}
        void RequestLeave(string action)
        {
            leaveAction=action;settings=false;
            if(action=="home"||Rules.Won(Level,Data.session.state)||Player.failed||(Data.session.state.actions==0&&!Progression.RestartCostsLife(Player,Level.id,false))){PerformLeave(false);return;}
            metaDialog="leave";Draw();
        }
        void DrawLeave()
        {
            bool costs=Progression.RestartCostsLife(Player,Level.id,Rules.Won(Level,Data.session.state));
            var root=Sheet("Restart level?",297,659,CloseMeta);Art(root,"icon-"+(costs?"heart":"rotate"),new Rect(278,414,164,161));Title(root,costs?"Restart costs 1 life":"Restart is free",new Rect(77,598,566,62),34);Label(root,costs?"Reset this board and start a fresh timer.":"Clear this board and try a new approach.",new Rect(89,680,542,48),23,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(root,"Restart",new Rect(106,767,508,84),()=>PerformLeave(costs),"green",33);
        }
        void PerformLeave(bool spend)
        {
            string id=Level.id;bool timed=Player.challenge;
            if(leaveAction=="home"){if(id=="tutorial-01"&&Data.hasSuspendedSession)FinishTutorial();EnterHome();return;}
            if(spend&&Progression.RestartCostsLife(Player,id,Rules.Won(Level,Data.session.state)))Progression.LoseLife(Player,DateTime.UtcNow);
            if(id=="tutorial-01"){LoadLevel(id);return;}
            if(timed&&Player.lives==0){Progression.ForgetAttempt(Data,id,true);Player.failed=true;Player.remainingSeconds=0;EnterHome();metaDialog="lives";Persist();Draw();return;}
            BeginLevel(id,true);
        }
        void DrawHelp()
        {
            var root=Sheet("How to make toys",235,825,CloseMeta);string[] steps={"1. Pick a lane's front piece.","2. Drag it into an empty mould space.","3. Rotate the piece to match the shape.","4. Park pieces to reveal what comes next.","5. Undo and hints help you find a fit."};for(int i=0;i<steps.Length;i++)Label(root,steps[i],new Rect(86,356+i*82,552,68),26,Color.white);
            Label(root,"Beat the timer to finish your toy.\nUndo is free and keeps the clock running. Restart or timeout costs one life. Home saves and pauses.",new Rect(85,790,550,110),22,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(root,"Play the tutorial",new Rect(96,916,528,84),StartTutorial,"green",31);
        }
        void DrawTimeUp()
        {
            var root=Sheet("Time's up!",282,730,null,"pink");Art(root,"icon-clock",new Rect(263,407,194,194));Title(root,"One life used",new Rect(80,636,560,53),35);Label(root,"Try a new approach and finish before time runs out.",new Rect(85,701,550,74),24,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);GButton(root,"Try again",new Rect(94,791,532,83),()=>BeginLevel(Level.id),"green",32);GButton(root,"Home",new Rect(154,888,412,69),EnterHome,"blue",27);
        }
        void DrawResult()
        {
            var root=Sheet("TOY COMPLETE!",182,911,null,"gold");
            var image=Rect("Pip's finished toy",root,new Rect(170,323,380,363)).gameObject.AddComponent<RawImage>();image.texture=toyTexture;image.raycastTarget=false;
            Title(root,"Meet Pip",new Rect(76,705,568,56),43);Art(root,"icon-coin",new Rect(203,777,59,59));Title(root,completionCoins>0?"+"+completionCoins+" coins":"Toy collected",new Rect(278,781,259,50),32);
            int.TryParse(Level.id.Replace("level-",""),out int n);GButton(root,Level.id=="tutorial-01"?"Let's build!":n==10?"Back to the trail":"Next level",new Rect(101,879,518,91),()=>{if(Level.id=="tutorial-01")FinishTutorial();else if(n>=10)EnterHome();else BeginLevel("level-"+(n+1).ToString("00"));},"green",34);
            GButton(root,"Home",new Rect(124,995,472,68),EnterHome,"blue",28);
        }
        void DrawSettings()
        {
            var root=Sheet(metaScreen=="game"?"PAUSED":"SETTINGS",170,925,()=>{settings=false;Draw();});
            string[] icons={"speaker","music","phone","freeze"};string[] labels={"Sound","Music","Haptics","Reduced motion"};bool[] values={Data.settings.sound,!Data.settings.musicMuted,Data.settings.haptics,Data.settings.reducedMotion};
            Action[] toggles={()=>Data.settings.sound=!Data.settings.sound,()=>Data.settings.musicMuted=!Data.settings.musicMuted,()=>Data.settings.haptics=!Data.settings.haptics,()=>Data.settings.reducedMotion=!Data.settings.reducedMotion};
            for(int i=0;i<4;i++){int toggle=i;float y=310+i*103;Art(root,"icon-"+icons[i],new Rect(91,y,69,69));Title(root,labels[i],new Rect(179,y+8,281,56),27,TextAnchor.MiddleLeft);GButton(root,values[i]?"On":"Off",new Rect(478,y+6,153,67),()=>{toggles[toggle]();Persist();Draw();},values[i]?"green":"purple",27);}
            GButton(root,"How to play",new Rect(94,740,532,71),()=>{settings=false;metaDialog="help";Draw();},"blue",29);
            if(metaScreen=="game")GButton(root,"Home",new Rect(94,830,532,76),()=>RequestLeave("home"),"green",31);
            else Label(root,"Pocket Toyworks - make a little joy",new Rect(92,841,536,51),24,Color.white,FontStyle.Normal,TextAnchor.MiddleCenter);
            GButton(root,metaScreen=="game"?"Resume":"Done",new Rect(94,962,532,83),()=>{settings=false;Draw();},"green",32);
        }
        void DrawLevels(){levels=false;RequestLeave("home");}
        int inspectedLane;
        void DrawQueue()
        {
            var root=Sheet("Lane "+(char)('A'+inspectedLane)+" parts",212,840,()=>{queueOpen=false;Draw();});var q=Level.lanes[inspectedLane].pieces.Skip(Data.session.state.heads[inspectedLane]).ToArray();
            for(int i=0;i<q.Length;i++){var r=new Rect(80+(i%3)*190,365+(i/3)*139,175,117);Art(root,"button-white",r);var slot=PieceSlot(root,new Rect(r.x+16,r.y+15,r.width-32,r.height-34),"Queue piece slot");DrawPiece(slot,q[i],new Rect(0,0,r.width-32,r.height-34),28,0,false);Label(root,(i+1).ToString(),new Rect(r.x+9,r.y+4,28,25),17,Ink);}
            GButton(root,"Back to building",new Rect(96,934,528,82),()=>{queueOpen=false;Draw();},"green",30);
        }
        void FreezeTime(){if(Progression.CurrentLevel(Data)<4){status="Time boost unlocks at level 4.";Draw();return;}if(!Player.challenge){status="Time boosts are for timed challenges.";Draw();return;}if(Player.freezeCharges<=0){status="Get time boosts in the Workshop.";Draw();return;}if(!Playing)return;Player.freezeCharges--;Player.remainingSeconds+=30;status="Thirty extra seconds. Take a breath.";Persist();Draw();}
        async void AutoFit()
        {
            if(Progression.CurrentLevel(Data)<7){status="Auto-fit unlocks at level 7.";Draw();return;}if(Player.fitCharges<=0){status="Get an auto-fit in the Workshop.";Draw();return;}if(!Playing||fitting)return;
            fitting=true;int version=revision;var level=Level;var state=Data.session.state.Clone();status="Finding a verified fit...";Draw();
            try {var result=await Task.Run(()=>Solver.Search(level,state,250,40000));if(version!=revision||!Playing||pointerId!=int.MinValue)return;Command? step=result.status==SearchStatus.Solved&&result.trace.Count>0?result.trace[0]:KnownFit(level,state);if(step.HasValue){if(Commit(step.Value)){Player.fitCharges--;Persist();Draw();}}else{status="No verified fit found. Use Undo or Hint. Your tool was kept.";Draw();}}finally{fitting=false;}
        }
        Command? KnownFit(Level level,State state)
        {
            var known=Rules.Start(level);foreach(var c in level.solution){if(Rules.Key(known)==Rules.Key(state))return c;Rules.Apply(level,known,c,out known);}return null;
        }
        void GameHud()
        {
            Art(content,"button-blue",new Rect(74,19,541,107));Art(content,"round-blue",new Rect(13,10,132,132));Title(content,"LEVEL",new Rect(29,30,101,28),21);int.TryParse(Level.id.Replace("level-",""),out int n);Title(content,n>0?n.ToString():"?",new Rect(24,55,111,63),44);
            Title(content,Level.id=="tutorial-01"?"LET'S LEARN":"TIME",new Rect(154,31,425,38),29);
            challengeLabel=Title(content,Player.challenge?TimeText((int)Math.Ceiling(Player.remainingSeconds)):Data.session.state.completed+" / "+Level.moulds.Length+" TOYS",new Rect(185,70,372,45),31);
            IconButton(content,"pause",new Rect(628,20,74,74),()=>{settings=true;Draw();});IconButton(content,"rotate",new Rect(628,102,74,74),()=>RequestLeave("restart"),"gold");
            Portrait(content,Player.avatar,Player.frame,new Rect(24,139,65,68),()=>{metaDialog="profile";Draw();});
            Art(content,"button-white",new Rect(107,143,167,60));
            if(Player.challenge){Art(content,"icon-heart",new Rect(118,151,43,43));lifeLabel=Title(content,Player.lives.ToString(),new Rect(120,155,40,33),25);Label(content,"Lives",new Rect(171,150,93,40),22,Ink,FontStyle.Bold);var hit=Panel(content,new Rect(107,143,167,60),Color.clear,0);var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.onClick.AddListener(()=>{metaDialog="lives";Draw();});}
            else{Art(content,"icon-fit",new Rect(118,151,43,43));Label(content,"Tutorial",new Rect(164,150,103,40),21,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);}
            Art(content,"button-white",new Rect(296,143,183,60));Art(content,"icon-coin",new Rect(305,149,47,47));coinLabel=Label(content,Player.coins.ToString(),new Rect(355,153,110,39),25,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            GButton(content,"Learn",new Rect(499,143,113,60),StartTutorial,"blue",22);
            float progress=(float)Data.session.state.used.Sum(id=>Level.Piece(id).cells.Length)/Level.pieces.Sum(p=>p.cells.Length);Panel(content,new Rect(163,120,420,5),new Color32(42,86,159,255),3);if(progress>0)Panel(content,new Rect(163,120,420*progress,5),new Color32(185,248,64,255),3);
        }
        void GameTools()
        {
            string[] icons={"undo","rotate","hint","freeze","fit"},names={"Undo","Rotate","Hint",Progression.CurrentLevel(Data)<4?"Lv.4":"Time",Progression.CurrentLevel(Data)<7?"Lv.7":"Auto-fit"};Action[] actions={Undo,Rotate,Hint,FreezeTime,AutoFit};
            for(int i=0;i<5;i++){IconButton(content,icons[i],new Rect(33+i*139,1143+Extra,98,98),actions[i],i>2?"blue":i==1?"gold":"blue");Title(content,names[i],new Rect(20+i*139,1237+Extra,124,32),21);if(i>2){bool unlocked=Progression.CurrentLevel(Data)>=(i==3?4:7);if(unlocked){Art(content,"round-green",new Rect(102+i*139,1206+Extra,38,38));Title(content,(i==3?Player.freezeCharges:Player.fitCharges).ToString(),new Rect(102+i*139,1209+Extra,38,28),20);}else Art(content,"icon-lock",new Rect(99+i*139,1197+Extra,33,36));}}
        }
    }
}
