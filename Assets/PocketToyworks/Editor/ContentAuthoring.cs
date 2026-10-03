using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using PocketToyworks.Core;

namespace PocketToyworks.Editor
{
    public static class ContentAuthoring
    {
        // Each letter is one authored connected piece; dots are blocked cells.
        static readonly string[][] Patterns = {
            new[]{"AABB/AABB/CCDD/CCDD"},
            new[]{"AAAA/BBCC/BBCC/DDDD"},
            new[]{"A.BB/A.BB/AACC/..CC"},
            new[]{"AAAB/ACBB/CCDB/.DDD"},
            new[]{".AAA/BBAC/BDCC/BDDC"},
            new[]{"AABB/AABB/CCDD/CCDD","AA../ABBB/ACCD/.CCD"},
            new[]{"AAAB/ACBB/CCDB/.DDD","AABB/AACB/DDCC/DDC."},
            new[]{".AAA/BBAC/BDCC/BDDC","AAAB/ACBB/CCDB/.DDD"},
            new[]{"AA../ABBB/ACCD/.CCD","AABB/AACB/DDCC/DDC."},
            new[]{"AAAB/ACBB/CCDB/.DDD",".AAA/BBAC/BDCC/BDDC","AA../ABBB/ACCD/.CCD"}
        };
        static readonly string[] Titles={"Hello, little maker","Long and short","A little turn","Corners together","Read the shape","Room for two","Better together","Make some space","Choose your fit","The finishing touch"};
        public static IEnumerable<Level> CreateAll()
        {
            yield return Tutorial();
            for(int i=0;i<10;i++)yield return Create(i,i<5?1:2,false);
            yield return Create(7,1,true); yield return Create(7,2,true);
        }
        static Level Tutorial()
        {
            var l=Create(2,1,false);l.id="tutorial-01";l.title="Fit • park • rotate";l.teaching="Follow the glowing shapes.";
            var a=l.pieces[0];a.cells=Rules.Rotate(a.cells,1);l.pieces[0]=a;
            l.lanes=new[]{new Lane{pieces=new[]{l.pieces[1].id,a.id}},new Lane{pieces=new[]{l.pieces[2].id}},new Lane{pieces=new string[0]}};
            l.solution=new[]{Command.Place(0,0,2,0,0),Command.Park(0,0),Command.Place(1,0,2,2,0),Command.Place(3,0,0,0,1)};Validator.Validate(l);return l;
        }
        static Level Create(int index,int active,bool comparison)
        {
            var l=new Level {id=comparison?"compare-"+active:"level-"+(index+1).ToString("00"), title=Titles[index], activeLimit=active,
                teaching=index<2?"Select a front piece. Tap an empty cell to fit it.":index<5?"Select a piece, then Rotate to find its fit.":"Two moulds, one choice. Park a front piece to reveal the next."};
            var pieces=new List<Piece>();var moulds=new List<Mould>(); var laneLists=new[]{new List<string>(),new List<string>(),new List<string>()};var trace=new List<Command>();
            for(int m=0;m<Patterns[index].Length;m++)
            {
                string flat=Patterns[index][m].Replace("/","");int mask=0;
                for(int k=0;k<16;k++)if(flat[k]!='.')mask|=1<<k;
                moulds.Add(new Mould{id=l.id+"-m"+m,mask=mask});
                var letters=flat.Where(c=>c!='.').Distinct().OrderBy(c=>c).ToArray();
                for(int p=0;p<letters.Length;p++)
                {
                    var cells=Enumerable.Range(0,16).Where(k=>flat[k]==letters[p]).Select(k=>new Cell(k%4,k/4)).ToArray();
                    int x=cells.Min(c=>c.x),y=cells.Min(c=>c.y),turn=index>=2?(p+index)%4:0;
                    string id=l.id+"-m"+m+"-p"+p;
                    pieces.Add(new Piece{id=id,color=(p+m)%4,cells=Rules.Rotate(cells,4-turn)});
                    int lane=(p+m)%3; laneLists[lane].Add(id);
                    // Completed slot 0 receives mould 2; with one slot all replacements stay in slot 0.
                    trace.Add(Command.Place(lane,active==1?0:m%2,x,y,turn));
                }
            }
            // A harmless reserve demonstration exposes B behind A, then returns A to its intended fit.
            if(index>=5)
            {
                string a=pieces[0].id,b=pieces[1].id; laneLists[1].Remove(b);laneLists[0].Insert(1,b);
                var first=trace[0]; first.source=3;var second=trace[1];second.source=0;
                trace.RemoveRange(0,2);trace.InsertRange(0,new[]{Command.Park(0,0),second,first});
            }
            l.pieces=pieces.ToArray();l.moulds=moulds.ToArray();l.lanes=laneLists.Select(q=>new Lane{pieces=q.ToArray()}).ToArray();l.solution=trace.ToArray();
            Validator.Validate(l);return l;
        }
        public static void Export()
        {
            Directory.CreateDirectory("Assets/PocketToyworks/Resources/Levels");
            foreach(var l in CreateAll())File.WriteAllText("Assets/PocketToyworks/Resources/Levels/"+l.id+".json",JsonUtility.ToJson(l,true));
        }
    }
}
