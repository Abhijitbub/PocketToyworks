using System;
using System.Linq;
using System.Collections.Generic;
using PocketToyworks.Core;
namespace PocketToyworks
{
    [Serializable] public class PlayerProgress
    {
        public bool initialized;public string playerName="Maker";public int avatar,frame,lives=5,coins=150,totalEarned;
        public long lifeClockUtc,lastSeenUtc,lastGiftUtc;public int freezeCharges=2,fitCharges=1;
        public List<int> ownedFrames=new List<int>{0,1};public List<string> rewardedLevels=new List<string>();
        public int challengeWins;public bool attemptCounted;public double bestChallengeSeconds;
        public bool challenge,challengeStarted;public int challengeRulesVersion;public double remainingSeconds;public bool failed;
    }
    [Serializable] public class PausedAttempt
    {
        public string levelId;public bool challenge,challengeStarted,attemptCounted;
        public Session session;public double activeSeconds,remainingSeconds;public int actionCount;
    }
    public static class Progression
    {
        public const int MaxLives=5,LifeMinutes=30,RefillCost=150,GiftCoins=75;
        public static long Stamp(DateTime utc)=>new DateTimeOffset(DateTime.SpecifyKind(utc,DateTimeKind.Utc)).ToUnixTimeSeconds();
        public static void Ensure(SaveData data,DateTime utc)
        {
            if(data.player==null||!data.player.initialized){data.player=new PlayerProgress{initialized=true,lastSeenUtc=Stamp(utc),lifeClockUtc=Stamp(utc),rewardedLevels=data.completed.Where(id=>id.StartsWith("level-")).ToList()};}
            var p=data.player;p.playerName=CleanName(p.playerName);p.avatar=Math.Max(0,Math.Min(7,p.avatar));
            if(p.challengeRulesVersion<1){p.challengeStarted=p.challenge;data.suspendedChallengeStarted=data.suspendedChallenge;p.challengeRulesVersion=1;}
            if(data.pausedAttempts==null)data.pausedAttempts=new List<PausedAttempt>();
            if(p.challengeRulesVersion<2)
            {
                // Preserve legacy boards and currency. Untimed attempts receive a
                // full clock once; existing timed attempts retain their spent time.
                if(data.session?.state!=null&&data.session.state.levelId!="tutorial-01"&&!p.challenge)
                {p.challenge=true;p.challengeStarted=false;p.remainingSeconds=TimeLimit(data.session.state.levelId);p.failed=false;p.attemptCounted=Rules.Won(Content.Get(data.session.state.levelId),data.session.state);}
                if(data.hasSuspendedSession&&data.suspendedSession?.state!=null&&!data.suspendedChallenge)
                {data.suspendedChallenge=true;data.suspendedChallengeStarted=false;data.suspendedRemaining=TimeLimit(data.suspendedSession.state.levelId);data.suspendedFailed=false;data.suspendedAttemptCounted=Rules.Won(Content.Get(data.suspendedSession.state.levelId),data.suspendedSession.state);}
                data.pausedAttempts=data.pausedAttempts.GroupBy(a=>a.levelId).Select(group=>
                {
                    var attempt=group.FirstOrDefault(a=>a.challenge)??group.First();
                    if(!attempt.challenge){attempt.challenge=true;attempt.challengeStarted=false;attempt.remainingSeconds=TimeLimit(attempt.levelId);attempt.attemptCounted=false;}
                    return attempt;
                }).ToList();
                p.challengeRulesVersion=2;
            }
            if(p.ownedFrames==null)p.ownedFrames=new List<int>{0,1};if(!p.ownedFrames.Contains(0))p.ownedFrames.Add(0);
            if(!p.ownedFrames.Contains(p.frame))p.frame=0;if(p.rewardedLevels==null)p.rewardedLevels=new List<string>();
            p.lives=Math.Max(0,Math.Min(MaxLives,p.lives));p.coins=Math.Max(0,p.coins);p.freezeCharges=Math.Max(0,p.freezeCharges);p.fitCharges=Math.Max(0,p.fitCharges);Refresh(p,utc);
        }
        public static string CleanName(string name){var clean=new string((name??"").Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='_'||c=='-').Take(16).ToArray()).Trim();return clean.Length==0?"Maker":clean;}
        public static int TimeLimit(string levelId)=>int.TryParse(levelId.Replace("level-",""),out int n)&&n>5?240:180;
        public static bool CanBegin(PlayerProgress p,bool resuming=false)=>resuming||p.lives>0;
        public static bool RestartCostsLife(PlayerProgress p,string levelId,bool won)=>p.challenge&&p.challengeStarted&&!p.failed&&!p.attemptCounted&&!won&&levelId!="tutorial-01";
        public static PausedAttempt Paused(SaveData data,string id,bool challenge)=>data.pausedAttempts.FirstOrDefault(a=>a.levelId==id&&a.challenge==challenge);
        public static void ForgetAttempt(SaveData data,string id,bool challenge)=>data.pausedAttempts.RemoveAll(a=>a.levelId==id&&a.challenge==challenge);
        public static void CacheAttempt(SaveData data,Level level)
        {
            if(level.id=="tutorial-01")return;ForgetAttempt(data,level.id,data.player.challenge);
            if(data.player.failed||Rules.Won(level,data.session.state)||(!data.player.challengeStarted&&data.session.state.actions==0))return;
            var snapshot=new Session(level){state=data.session.state.Clone(),history=data.session.history.Select(s=>s.Clone()).ToList()};
            data.pausedAttempts.Add(new PausedAttempt{levelId=level.id,challenge=data.player.challenge,challengeStarted=data.player.challengeStarted,attemptCounted=data.player.attemptCounted,session=snapshot,activeSeconds=data.activeSeconds,actionCount=data.actionCount,remainingSeconds=data.player.remainingSeconds});
        }
        public static bool RestoreAttempt(SaveData data,string id,bool challenge)
        {
            var attempt=Paused(data,id,challenge);if(attempt==null)return false;ForgetAttempt(data,id,challenge);
            data.session=attempt.session;data.activeSeconds=attempt.activeSeconds;data.actionCount=attempt.actionCount;
            data.player.challenge=challenge;data.player.challengeStarted=attempt.challengeStarted;data.player.attemptCounted=attempt.attemptCounted;data.player.remainingSeconds=attempt.remainingSeconds;data.player.failed=false;return true;
        }
        public static bool Refresh(PlayerProgress p,DateTime utc)
        {
            long now=Math.Max(Stamp(utc),p.lastSeenUtc);p.lastSeenUtc=now;
            if(p.lives>=MaxLives){p.lifeClockUtc=now;return false;}
            if(p.lifeClockUtc<=0)p.lifeClockUtc=now;long gained=(now-p.lifeClockUtc)/(LifeMinutes*60);
            if(gained<=0)return false;p.lives=Math.Min(MaxLives,p.lives+(int)Math.Min(MaxLives,gained));p.lifeClockUtc=p.lives==MaxLives?now:p.lifeClockUtc+gained*LifeMinutes*60;return true;
        }
        public static int NextLifeSeconds(PlayerProgress p,DateTime utc){Refresh(p,utc);return p.lives==MaxLives?0:(int)Math.Max(0,LifeMinutes*60-(Math.Max(Stamp(utc),p.lastSeenUtc)-p.lifeClockUtc));}
        public static bool LoseLife(PlayerProgress p,DateTime utc){Refresh(p,utc);if(p.lives==0)return false;if(p.lives==MaxLives)p.lifeClockUtc=Math.Max(Stamp(utc),p.lastSeenUtc);p.lives--;return true;}
        public static bool Spend(PlayerProgress p,int cost){if(cost<0||p.coins<cost)return false;p.coins-=cost;return true;}
        public static bool Refill(PlayerProgress p,DateTime utc){Refresh(p,utc);if(p.lives==MaxLives||!Spend(p,RefillCost))return false;p.lives=MaxLives;p.lifeClockUtc=Math.Max(Stamp(utc),p.lastSeenUtc);return true;}
        public static bool GiftAvailable(PlayerProgress p,DateTime utc)=>p.lastGiftUtc==0||Math.Max(Stamp(utc),p.lastSeenUtc)-p.lastGiftUtc>=86400;
        public static bool ClaimGift(PlayerProgress p,DateTime utc){Refresh(p,utc);if(!GiftAvailable(p,utc))return false;p.lastGiftUtc=p.lastSeenUtc;p.coins+=GiftCoins;p.totalEarned+=GiftCoins;return true;}
        public static int Reward(PlayerProgress p,string id){if(!id.StartsWith("level-")||p.rewardedLevels.Contains(id))return 0;p.rewardedLevels.Add(id);p.coins+=50;p.totalEarned+=50;return 50;}
        public static int CurrentLevel(SaveData data){int max=0;foreach(var id in data.completed)if(id.StartsWith("level-")&&int.TryParse(id.Substring(6),out int n))max=Math.Max(max,n);return Math.Min(10,max+1);}
        public static bool Unlocked(SaveData data,int n)=>n>=1&&n<=10&&(n<=CurrentLevel(data)||data.completed.Contains("level-"+n.ToString("00")));
        public static bool BuyFrame(PlayerProgress p,int frame){if(frame<0||frame>5||p.ownedFrames.Contains(frame)||!Spend(p,FrameCost(frame)))return false;p.ownedFrames.Add(frame);return true;}
        public static int FrameCost(int frame)=>frame<2?0:100+(frame-2)*50;
        public static bool BuyBooster(PlayerProgress p,bool freeze){if(!Spend(p,freeze?50:80))return false;if(freeze)p.freezeCharges+=2;else p.fitCharges++;return true;}
    }
}
