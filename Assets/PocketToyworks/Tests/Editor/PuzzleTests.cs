using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PocketToyworks.Core;

namespace PocketToyworks.Tests
{
    public class PuzzleTests
    {
        Level L(string id="level-01")=>Content.Get(id);
        [Test] public void EveryStoredTraceWinsAndConservesEveryPiece()
        {Assert.AreEqual(13,Content.All.Count());foreach(var l in Content.All)Validator.Validate(l);}
        [Test] public void AuthoredLevelsAreNotRecolorsQueueSwapsOrBoardRotations()
        {var levels=Content.All.Where(l=>l.id.StartsWith("level")).ToArray();Assert.AreEqual(10,levels.Select(Validator.Fingerprint).Distinct().Count());}
        [Test] public void RotationsNormalizeAndNeverMirror()
        {
            var shape=new[]{new Cell(0,0),new Cell(0,1),new Cell(0,2),new Cell(1,2)};
            CollectionAssert.AreEqual(Rules.Rotate(shape,0),Rules.Rotate(shape,4));
            CollectionAssert.AreEqual(new[]{new Cell(0,0),new Cell(1,0),new Cell(2,0),new Cell(0,1)},Rules.Rotate(shape,1));
        }
        [Test] public void RejectsNegativeOverflowAndOverlappingPlacementsWithoutMutation()
        {
            var l=L();var s=Rules.Start(l);string before=JsonUtility.ToJson(s);
            Assert.IsFalse(Rules.Apply(l,s,Command.Place(0,0,-1,0),out _));Assert.IsFalse(Rules.Apply(l,s,Command.Place(0,0,3,0),out _));Assert.AreEqual(before,JsonUtility.ToJson(s));
            Rules.Apply(l,s,l.solution[0],out s);Assert.IsFalse(Rules.Legal(l,s,Command.Place(1,0,0,0)));
        }
        [Test] public void BlockedCellsRejectPlacement() {var l=L("level-03");Assert.IsFalse(Rules.Legal(l,Rules.Start(l),Command.Place(0,0,1,0)));}
        [Test] public void ReserveAdvancesOnlyOneLaneAndCannotReturnToSupply()
        {
            var l=L();var s=Rules.Start(l);string front=Rules.Front(l,s,0);Assert.IsTrue(Rules.Apply(l,s,Command.Park(0,1),out s));
            Assert.AreEqual(front,s.reserves[1]);CollectionAssert.AreEqual(new[]{1,0,0},s.heads);Assert.IsFalse(Rules.Legal(l,s,Command.Park(0,1)));Assert.IsFalse(Rules.Legal(l,s,Command.Park(4,0)));Rules.AssertConservation(l,s);
        }
        [Test] public void CompletionAndUndoRestoreOriginalMouldQueuesAndPieces()
        {
            var l=L("level-10");var session=new Session(l);string before=null;
            foreach(var c in l.solution){before=JsonUtility.ToJson(session.state);int completed=session.state.completed;session.Commit(l,c);if(session.state.completed>completed)break;}
            Assert.AreEqual(1,session.state.completed);Assert.AreEqual(2,session.state.boards[0].mould);Assert.IsTrue(session.Undo());Assert.AreEqual(before,JsonUtility.ToJson(session.state));Rules.AssertConservation(l,session.state);
        }
        [Test] public void WinRequiresAllSupplyAndReservesEmpty()
        {var l=L();var s=Rules.Start(l);Assert.IsFalse(Rules.Won(l,s));foreach(var c in l.solution)Rules.Apply(l,s,c,out s);Assert.IsTrue(Rules.Won(l,s));Assert.IsFalse(Rules.Jammed(l,s));s.reserves[0]="stray";Assert.IsFalse(Rules.Won(l,s));}
        [Test] public void FreeReserveWithEmptySupplyDoesNotPreventJam()
        {
            var l=L();var s=Rules.Start(l);for(int i=0;i<3;i++)s.heads[i]=l.lanes[i].pieces.Length;s.reserves[0]=l.pieces[0].id;s.boards[0].cells=Enumerable.Repeat("occupied",16).ToArray();Assert.IsTrue(Rules.Jammed(l,s));
        }
        [Test] public void AvailableParkIsAForwardActionEvenWhenNoPlacementFits()
        {var l=L();var s=Rules.Start(l);s.boards[0].cells=Enumerable.Repeat("occupied",16).ToArray();Assert.IsFalse(Rules.Jammed(l,s));s.reserves[0]=l.pieces[0].id;s.reserves[1]=l.pieces[1].id;Assert.IsTrue(Rules.Jammed(l,s));}
        [Test] public void LegalMovesCanExistWithoutACompleteSolution()
        {
            var l=L();var s=Rules.Start(l);Rules.Apply(l,s,Command.Place(0,0,1,1),out s);
            Assert.IsFalse(Rules.Jammed(l,s));Assert.AreEqual(SearchStatus.Impossible,Solver.Search(l,s,2000,100000).status);
        }
        [Test] public void SolverBudgetMeansUnknown()
        {Assert.AreEqual(SearchStatus.Unknown,Solver.Search(L(),Rules.Start(L()),0,0).status);}
        [Test] public void SolverTraceActuallyReplays()
        {var l=L();var s=Rules.Start(l);var result=Solver.Search(l,s,1000,10000);Assert.AreEqual(SearchStatus.Solved,result.status);foreach(var c in result.trace)Assert.IsTrue(Rules.Apply(l,s,c,out s));Assert.IsTrue(Rules.Won(l,s));}
        [Test] public void SaveRoundTripPreservesUndoAcrossCompletionAndSettings()
        {
            var l=L();var data=new SaveData{session=new Session(l)};foreach(var c in l.solution)data.session.Commit(l,c);data.settings.reducedMotion=true;data.completed.Add(l.id);
            var dir=Path.Combine(Application.temporaryCachePath,"toyworks-tests-"+Guid.NewGuid());var store=new SaveStore(dir);Assert.IsTrue(store.Write(data));var restored=store.Read(Content.Get);Assert.NotNull(restored);Assert.AreEqual(JsonUtility.ToJson(data),JsonUtility.ToJson(restored));Assert.IsTrue(restored.session.Undo());Assert.IsFalse(Rules.Won(l,restored.session.state));
        }
        [Test] public void CorruptPrimaryRecoversPreviousAndMissingSaveIsGraceful()
        {
            var dir=Path.Combine(Application.temporaryCachePath,"toyworks-tests-"+Guid.NewGuid());var store=new SaveStore(dir);Assert.IsNull(store.Read(Content.Get));var data=new SaveData{session=new Session(L())};Assert.IsTrue(store.Write(data));data.session.Commit(L(),L().solution[0]);Assert.IsTrue(store.Write(data));File.WriteAllText(Path.Combine(dir,"prototype-v1.json"),"broken");Assert.AreEqual(0,store.Read(Content.Get).session.state.actions);
        }
        [Test] public void TutorialSavePreservesTheSuspendedPuzzleAndMusicPreference()
        {
            var original=new Session(L("level-03"));original.Commit(L("level-03"),Command.Park(0,0));
            var tutorial=Content.Get("tutorial-01");var data=new SaveData{session=new Session(tutorial),hasSuspendedSession=true,suspendedSession=original,suspendedSeconds=42,suspendedActions=1};
            data.session.Commit(tutorial,tutorial.solution[0]);data.settings.musicMuted=true;
            var store=new SaveStore(Path.Combine(Application.temporaryCachePath,"toyworks-tutorial-"+Guid.NewGuid()));Assert.IsTrue(store.Write(data));var restored=store.Read(Content.Get);
            Assert.NotNull(restored);Assert.AreEqual(JsonUtility.ToJson(original),JsonUtility.ToJson(restored.suspendedSession));Assert.IsTrue(restored.settings.musicMuted);Assert.AreEqual(1,restored.session.state.actions);
        }
        [Test] public void OriginalMusicAndEffectsHaveFiniteUnclippedSamples()
        {
            foreach(var clip in new[]{WorkshopAudio.Music(),WorkshopAudio.Effect(false),WorkshopAudio.Effect(true)}){var samples=new float[clip.samples];clip.GetData(samples,0);Assert.IsTrue(samples.Any(v=>Math.Abs(v)>.01f));Assert.IsTrue(samples.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<1));UnityEngine.Object.DestroyImmediate(clip);}
        }
        [Test] public void InvalidHistoryIsRejected()
        {var session=new Session(L());session.Commit(L(),L().solution[0]);session.state.heads[1]++;Assert.Throws<InvalidOperationException>(()=>SaveStore.ValidateSession(L(),session));}
    }
}
