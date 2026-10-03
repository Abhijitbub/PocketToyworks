using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PocketToyworks.Core;
namespace PocketToyworks.Tests
{
    public class ProgressionTests
    {
        readonly DateTime now=new DateTime(2026,10,1,0,0,0,DateTimeKind.Utc);
        SaveData Fresh(){var data=new SaveData{session=new Session(Content.Get("level-01"))};Progression.Ensure(data,now);return data;}
        [Test] public void LegacyUpgradeKeepsPuzzleSettingsAndDoesNotRepeatOldRewards(){var data=new SaveData{session=new Session(Content.Get("level-03"))};data.session.Commit(Content.Get("level-03"),Command.Park(0,0));data.settings.musicMuted=true;data.completed.Add("level-01");string before=JsonUtility.ToJson(data.session);Progression.Ensure(data,now);Assert.AreEqual(before,JsonUtility.ToJson(data.session));Assert.IsTrue(data.settings.musicMuted);Assert.AreEqual(150,data.player.coins);Assert.AreEqual(0,Progression.Reward(data.player,"level-01"));Progression.Ensure(data,now.AddHours(1));Assert.AreEqual(150,data.player.coins);}
        [Test] public void OfflineLivesRegenerateWithRemainderAndCapAtFive(){var p=Fresh().player;Progression.LoseLife(p,now);Progression.LoseLife(p,now);Assert.AreEqual(3,p.lives);Progression.Refresh(p,now.AddMinutes(45));Assert.AreEqual(4,p.lives);Assert.AreEqual(900,Progression.NextLifeSeconds(p,now.AddMinutes(45)));Progression.Refresh(p,now.AddHours(5));Assert.AreEqual(5,p.lives);Assert.AreEqual(0,Progression.NextLifeSeconds(p,now.AddHours(5)));}
        [Test] public void ClockRollbackCannotAwardLivesOrDuplicateGift(){var p=Fresh().player;Progression.LoseLife(p,now);Progression.Refresh(p,now.AddMinutes(20));Assert.AreEqual(600,Progression.NextLifeSeconds(p,now.AddMinutes(10)));Assert.AreEqual(4,p.lives);Assert.IsTrue(Progression.ClaimGift(p,now.AddMinutes(20)));Assert.IsFalse(Progression.ClaimGift(p,now.AddDays(-1)));}
        [Test] public void FullLifeRefillCannotSpendAndInsufficientFundsLeaveStateIntact(){var p=Fresh().player;Assert.IsFalse(Progression.Refill(p,now));Assert.AreEqual(150,p.coins);Progression.LoseLife(p,now);p.coins=149;Assert.IsFalse(Progression.Refill(p,now));Assert.AreEqual(4,p.lives);Assert.AreEqual(149,p.coins);p.coins=150;Assert.IsTrue(Progression.Refill(p,now));Assert.AreEqual(5,p.lives);Assert.AreEqual(0,p.coins);}
        [Test] public void GiftsAndFirstClearRewardsCannotBeRepeated(){var p=Fresh().player;Assert.IsTrue(Progression.ClaimGift(p,now));Assert.IsFalse(Progression.ClaimGift(p,now.AddHours(23)));Assert.IsTrue(Progression.ClaimGift(p,now.AddHours(24)));Assert.AreEqual(50,Progression.Reward(p,"level-01"));Assert.AreEqual(0,Progression.Reward(p,"level-01"));Assert.AreEqual(0,Progression.Reward(p,"tutorial-01"));Assert.AreEqual(350,p.coins);}
        [Test] public void ProgressionUnlocksNextLevelAndPreservesLegacyHigherClears(){var data=Fresh();Assert.IsTrue(Progression.Unlocked(data,1));Assert.IsFalse(Progression.Unlocked(data,2));data.completed.Add("level-01");Assert.IsTrue(Progression.Unlocked(data,2));data.completed.Add("level-07");Assert.AreEqual(8,Progression.CurrentLevel(data));Assert.IsTrue(Progression.Unlocked(data,7));Assert.IsFalse(Progression.Unlocked(data,9));}
        [Test] public void FrameAndToolPurchasesAreAtomicAndOwnedFramesCannotChargeAgain(){var p=Fresh().player;Assert.IsTrue(Progression.BuyFrame(p,2));Assert.AreEqual(50,p.coins);Assert.IsFalse(Progression.BuyFrame(p,2));Assert.AreEqual(50,p.coins);Assert.IsFalse(Progression.BuyFrame(p,5));Assert.IsTrue(Progression.BuyBooster(p,true));Assert.AreEqual(4,p.freezeCharges);Assert.AreEqual(0,p.coins);Assert.IsFalse(Progression.BuyBooster(p,false));Assert.AreEqual(1,p.fitCharges);}
        [Test] public void ProfileEconomyAndChallengeResumeAcrossSave(){var data=Fresh();data.player.playerName="Pocket Maker";data.player.avatar=7;data.player.frame=1;data.player.challenge=true;data.player.remainingSeconds=71.25;Progression.LoseLife(data.player,now);Progression.ClaimGift(data.player,now);var store=new SaveStore(Path.Combine(Application.temporaryCachePath,"toyworks-meta-"+Guid.NewGuid()));Assert.IsTrue(store.Write(data));var restored=store.Read(Content.Get);Assert.NotNull(restored);Assert.AreEqual(JsonUtility.ToJson(data.player),JsonUtility.ToJson(restored.player));Assert.AreEqual(71.25,restored.player.remainingSeconds);}
        [Test] public void ProfileNameAndRequiredArtAreValid(){Assert.AreEqual("Maker",Progression.CleanName("<><>"));Assert.AreEqual("Hello",Progression.CleanName("  Hello! "));Assert.NotNull(Resources.Load<Font>("Fonts/ToyworksRounded"));for(int i=0;i<8;i++)Assert.NotNull(Resources.Load<Texture2D>("UI/avatar-"+i));for(int i=0;i<6;i++)Assert.NotNull(Resources.Load<Texture2D>("UI/frame-"+i));Assert.NotNull(Resources.Load<Texture2D>("UI/background-home"));}
        [Test] public void TimedEntryNeedsLifeButSavedAttemptCanResumeAtZero()
        {
            var p=Fresh().player;p.lives=0;Assert.IsFalse(Progression.CanBegin(p));Assert.IsTrue(Progression.CanBegin(p,true));
            p.challengeStarted=true;Assert.IsTrue(Progression.RestartCostsLife(p,"level-01",false));
            Assert.IsFalse(Progression.RestartCostsLife(p,"tutorial-01",false));Assert.IsFalse(Progression.RestartCostsLife(p,"level-01",true));p.failed=true;Assert.IsFalse(Progression.RestartCostsLife(p,"level-01",false));
        }
        [Test] public void UndoingToZeroOrAddingTimeCannotMakeChallengeRestartFree()
        {
            var data=Fresh();var level=Content.Get("level-01");data.player.challenge=true;data.player.challengeStarted=true;data.player.remainingSeconds=210;
            data.session.Commit(level,level.solution[0]);data.session.Undo();Assert.AreEqual(0,data.session.state.actions);Assert.IsTrue(Progression.RestartCostsLife(data.player,level.id,false));
            data.player.challengeStarted=false;Assert.IsFalse(Progression.RestartCostsLife(data.player,level.id,false));data.player.challengeStarted=true;data.player.attemptCounted=true;Assert.IsFalse(Progression.RestartCostsLife(data.player,level.id,false));
        }
        [Test] public void PausedLevelsPreserveBoardsAndTimersAcrossSave()
        {
            var data=Fresh();var level=Content.Get("level-01");data.session.Commit(level,level.solution[0]);data.player.challengeStarted=true;data.player.remainingSeconds=71.25;data.activeSeconds=18;data.actionCount=3;
            Progression.CacheAttempt(data,level);data.session.Undo();Assert.AreEqual(1,Progression.Paused(data,level.id,true).session.state.actions,"cached state must be isolated");
            var other=Content.Get("level-03");data.session=new Session(other);data.player.challengeStarted=true;data.player.remainingSeconds=123;Progression.CacheAttempt(data,other);Progression.CacheAttempt(data,other);Assert.AreEqual(2,data.pausedAttempts.Count);
            var store=new SaveStore(Path.Combine(Application.temporaryCachePath,"toyworks-timed-"+Guid.NewGuid()));Assert.IsTrue(store.Write(data));var restored=store.Read(Content.Get);Assert.NotNull(restored);Assert.IsTrue(Progression.RestoreAttempt(restored,level.id,true));Assert.AreEqual(1,restored.session.state.actions);Assert.AreEqual(71.25,restored.player.remainingSeconds);Assert.IsTrue(restored.player.challengeStarted);Assert.AreEqual(18,restored.activeSeconds);Assert.AreEqual(3,restored.actionCount);
            Assert.IsTrue(Progression.RestoreAttempt(restored,other.id,true));Assert.AreEqual(0,restored.session.state.actions);Assert.IsTrue(restored.player.challenge);Assert.AreEqual(123,restored.player.remainingSeconds);
        }
        [Test] public void UntimedMigrationPreservesPuzzleAndGrantsClockOnce()
        {
            var data=Fresh();data.session.Commit(Content.Get("level-01"),Command.Park(0,0));data.player.challengeRulesVersion=1;data.player.challenge=false;data.player.remainingSeconds=0;data.player.lives=0;data.player.coins=245;data.player.attemptCounted=true;
            string board=JsonUtility.ToJson(data.session);Progression.Ensure(data,now);Assert.AreEqual(board,JsonUtility.ToJson(data.session));Assert.IsTrue(data.player.challenge);Assert.IsFalse(data.player.challengeStarted);Assert.IsFalse(data.player.attemptCounted);Assert.AreEqual(180,data.player.remainingSeconds);Assert.AreEqual(0,data.player.lives);Assert.AreEqual(245,data.player.coins);
            data.player.remainingSeconds=91;Progression.Ensure(data,now);Assert.AreEqual(91,data.player.remainingSeconds);
        }
        [Test] public void MigrationKeepsTimedCacheAndUpgradesSuspendedTutorialPuzzle()
        {
            var data=Fresh();var level=Content.Get("level-07");data.player.challengeRulesVersion=1;data.player.challenge=true;data.player.remainingSeconds=53;
            data.session=new Session(Content.Get("tutorial-01"));data.player.challenge=false;data.player.remainingSeconds=0;data.hasSuspendedSession=true;data.suspendedSession=new Session(level);data.suspendedSession.Commit(level,Command.Park(0,0));data.suspendedChallenge=false;
            data.pausedAttempts.Add(new PausedAttempt{levelId=level.id,challenge=false,session=new Session(level)});
            data.pausedAttempts.Add(new PausedAttempt{levelId=level.id,challenge=true,challengeStarted=true,remainingSeconds=23,session=new Session(level)});
            Progression.Ensure(data,now);Assert.IsFalse(data.player.challenge,"tutorial remains untimed");Assert.IsTrue(data.suspendedChallenge);Assert.AreEqual(240,data.suspendedRemaining);Assert.AreEqual(1,data.suspendedSession.state.actions);Assert.AreEqual(1,data.pausedAttempts.Count);Assert.AreEqual(23,data.pausedAttempts[0].remainingSeconds);Assert.IsTrue(data.pausedAttempts[0].challengeStarted);
        }
        [Test] public void UnstartedEmptyBoardCannotCreateResumeAtZeroLives()
        {
            var data=Fresh();Progression.CacheAttempt(data,Content.Get("level-01"));Assert.AreEqual(0,data.pausedAttempts.Count);
            data.player.challengeStarted=true;Progression.CacheAttempt(data,Content.Get("level-01"));Assert.AreEqual(1,data.pausedAttempts.Count);
        }
        [Test] public void LegacyChallengeMigrationKeepsSpentTimeAndMarksAttemptStarted()
        {
            var data=Fresh();data.player.challengeRulesVersion=0;data.player.challenge=true;data.player.remainingSeconds=91;data.hasSuspendedSession=true;data.suspendedChallenge=true;Progression.Ensure(data,now);Assert.IsTrue(data.player.challengeStarted);Assert.IsTrue(data.suspendedChallengeStarted);Assert.AreEqual(91,data.player.remainingSeconds);
            data.player.challengeStarted=false;Progression.Ensure(data,now);Assert.IsFalse(data.player.challengeStarted,"migration must not run on every refresh");
        }
    }
}
