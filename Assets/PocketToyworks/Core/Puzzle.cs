using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace PocketToyworks.Core
{
    [Serializable] public struct Cell : IEquatable<Cell>
    {
        public int x, y;
        public Cell(int x, int y) { this.x = x; this.y = y; }
        public bool Equals(Cell c) => x == c.x && y == c.y;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => x * 31 + y;
    }
    [Serializable] public class Piece { public string id; public int color; public Cell[] cells; }
    [Serializable] public class Mould { public string id, toyId = "pip-robot"; public int mask; }
    [Serializable] public class Lane { public string[] pieces; }
    [Serializable] public class Level
    {
        public string id, title, teaching;
        public int version = 1, activeLimit = 1;
        public Piece[] pieces; public Mould[] moulds; public Lane[] lanes; public Command[] solution;
        public Piece Piece(string id) => pieces.First(p => p.id == id);
    }
    // source 0..2 = lane; 3..4 = reserve. target = board slot or reserve slot.
    [Serializable] public struct Command
    {
        public bool reserve; public int source, target, x, y, rotation;
        public static Command Place(int source, int target, int x, int y, int rotation = 0) => new Command { source = source, target = target, x = x, y = y, rotation = rotation };
        public static Command Park(int source, int target) => new Command { reserve = true, source = source, target = target };
        public override string ToString() => reserve ? $"Move lane {(char)('A'+source)} to reserve {target+1}" : $"{(source < 3 ? "Lane " + (char)('A'+source) : "Reserve " + (source-2))}: rotate {rotation*90}°, mould {target+1}, row {y+1}, column {x+1}";
    }
    [Serializable] public class Board
    {
        public int mould = -1; public string[] cells = new string[16];
        public Board Clone() => new Board { mould = mould, cells = (string[])cells.Clone() };
    }
    [Serializable] public class State
    {
        public string levelId; public int[] heads = new int[3]; public string[] reserves = new string[2];
        public Board[] boards; public int nextMould, completed, actions; public List<string> used = new List<string>();
        public State Clone() => new State { levelId = levelId, heads = (int[])heads.Clone(), reserves = (string[])reserves.Clone(), boards = boards.Select(b => b.Clone()).ToArray(), nextMould = nextMould, completed = completed, actions = actions, used = new List<string>(used) };
    }
    public static class Rules
    {
        public static Cell[] Rotate(Cell[] cells, int turns)
        {
            var result = cells.ToArray();
            for (int r = 0; r < ((turns % 4 + 4) % 4); r++) result = result.Select(c => new Cell(-c.y, c.x)).ToArray();
            int minX = result.Min(c => c.x), minY = result.Min(c => c.y);
            return result.Select(c => new Cell(c.x-minX, c.y-minY)).OrderBy(c => c.y).ThenBy(c => c.x).ToArray();
        }
        public static State Start(Level level)
        {
            var s = new State { levelId = level.id, boards = new Board[level.activeLimit] };
            for (int i = 0; i < s.boards.Length; i++) { s.boards[i] = new Board(); Replace(level, s, s.boards[i]); }
            return s;
        }
        static void Replace(Level l, State s, Board b) { b.mould = s.nextMould < l.moulds.Length ? s.nextMould++ : -1; b.cells = new string[16]; }
        public static string Front(Level l, State s, int source)
        {
            if (source < 0 || source > 4) return null;
            if (source >= 3) return s.reserves[source-3];
            return s.heads[source] < l.lanes[source].pieces.Length ? l.lanes[source].pieces[s.heads[source]] : null;
        }
        public static bool Legal(Level l, State s, Command c)
        {
            var id = Front(l,s,c.source); if (string.IsNullOrEmpty(id)) return false;
            if (c.reserve) return c.source < 3 && c.target >= 0 && c.target < 2 && string.IsNullOrEmpty(s.reserves[c.target]);
            if (c.target < 0 || c.target >= s.boards.Length || c.rotation < 0 || c.rotation > 3) return false;
            var b = s.boards[c.target]; if (b.mould < 0) return false;
            foreach (var cell in Rotate(l.Piece(id).cells,c.rotation))
            {
                int x = c.x+cell.x, y = c.y+cell.y;
                if (x < 0 || x >= 4 || y < 0 || y >= 4) return false;
                int k = y*4+x;
                if ((l.moulds[b.mould].mask & (1 << k)) == 0 || !string.IsNullOrEmpty(b.cells[k])) return false;
            }
            return true;
        }
        public static bool Apply(Level l, State s, Command c, out State next)
        {
            next = s; if (!Legal(l,s,c)) return false;
            next = s.Clone(); var id = Front(l,s,c.source);
            if (c.source < 3) next.heads[c.source]++; else next.reserves[c.source-3] = null;
            if (c.reserve) next.reserves[c.target] = id;
            else
            {
                var b = next.boards[c.target];
                foreach (var cell in Rotate(l.Piece(id).cells,c.rotation)) b.cells[(c.y+cell.y)*4+c.x+cell.x] = id;
                next.used.Add(id);
                int occupied = 0; for (int i=0;i<16;i++) if (!string.IsNullOrEmpty(b.cells[i])) occupied |= 1<<i;
                if (occupied == l.moulds[b.mould].mask) { next.completed++; Replace(l,next,b); }
            }
            next.actions++; return true;
        }
        public static bool Won(Level l, State s) => s.completed == l.moulds.Length && s.reserves.All(string.IsNullOrEmpty) && Enumerable.Range(0,3).All(i => s.heads[i] == l.lanes[i].pieces.Length);
        public static IEnumerable<Command> Moves(Level l, State s)
        {
            for (int source=0;source<5;source++)
            {
                string id = Front(l,s,source); if (string.IsNullOrEmpty(id)) continue;
                var seen = new HashSet<string>();
                for (int r=0;r<4;r++)
                {
                    string key = string.Join(";", Rotate(l.Piece(id).cells,r).Select(c=>$"{c.x},{c.y}")); if (!seen.Add(key)) continue;
                    for (int b=0;b<s.boards.Length;b++) for(int y=0;y<4;y++) for(int x=0;x<4;x++)
                    { var c=Command.Place(source,b,x,y,r); if(Legal(l,s,c)) yield return c; }
                }
                if(source<3) for(int r=0;r<2;r++) { var c=Command.Park(source,r); if(Legal(l,s,c)) yield return c; }
            }
        }
        public static bool Jammed(Level l, State s) => !Won(l,s) && !Moves(l,s).Any();
        public static string Key(State s) => string.Join(",",s.heads)+"/"+string.Join(",",s.reserves)+"/"+s.nextMould+"/"+string.Join("/",s.boards.Select(b=>b.mould+":"+string.Join(",",b.cells.Select(x=>string.IsNullOrEmpty(x)?"0":"1"))));
        public static void AssertConservation(Level l, State s)
        {
            var ids = new List<string>(s.used); ids.AddRange(s.reserves.Where(x=>!string.IsNullOrEmpty(x)));
            for(int i=0;i<3;i++) ids.AddRange(l.lanes[i].pieces.Skip(s.heads[i]));
            if(ids.Count != l.pieces.Length || ids.Distinct().Count()!=ids.Count || !ids.OrderBy(x=>x).SequenceEqual(l.pieces.Select(p=>p.id).OrderBy(x=>x))) throw new InvalidOperationException("Piece conservation failed");
        }
    }
    [Serializable] public class Session
    {
        public State state; public List<State> history = new List<State>();
        public Session(Level l) { state=Rules.Start(l); }
        public bool Commit(Level l, Command c) { if(!Rules.Apply(l,state,c,out var next)) return false; history.Add(state); state=next; return true; }
        public bool Undo() { if(history.Count==0)return false; state=history[history.Count-1];history.RemoveAt(history.Count-1);return true; }
    }
    public enum SearchStatus { Solved, Impossible, Unknown }
    public class SearchResult { public SearchStatus status; public List<Command> trace; public int nodes; }
    public static class Solver
    {
        public static SearchResult Search(Level l, State start, int milliseconds=100, int nodeBudget=20000, CancellationToken cancellation=default)
        {
            var watch=Stopwatch.StartNew(); var seen=new HashSet<string>(); var path=new List<Command>(); int nodes=0; bool timedOut=false;
            bool Visit(State s)
            {
                if(Rules.Won(l,s))return true;
                if(cancellation.IsCancellationRequested || ++nodes>nodeBudget || watch.ElapsedMilliseconds>=milliseconds) { timedOut=true; return false; }
                if(!seen.Add(Rules.Key(s)))return false;
                foreach(var c in Rules.Moves(l,s)) { Rules.Apply(l,s,c,out var next); path.Add(c); if(Visit(next)) return true; path.RemoveAt(path.Count-1); if(timedOut)return false; }
                return false;
            }
            bool solved=Visit(start); return new SearchResult{ status=solved?SearchStatus.Solved:timedOut?SearchStatus.Unknown:SearchStatus.Impossible,trace=solved?path:null,nodes=nodes };
        }
    }
    public static class Validator
    {
        public static string Fingerprint(Level l)
        {
            string Shape(Piece p)=>Enumerable.Range(0,4).Select(r=>string.Join(";",Rules.Rotate(p.cells,r).Select(c=>c.x+","+c.y))).OrderBy(s=>s,StringComparer.Ordinal).First();
            string queues=string.Join("/",l.lanes.Select(q=>string.Join("|",q.pieces.Select(id=>Shape(l.Piece(id))))).OrderBy(s=>s,StringComparer.Ordinal));
            var variants=new List<string>();
            for(int r=0;r<4;r++)
            {
                var masks=new List<int>();
                foreach(var m in l.moulds)
                {
                    int mask=0;for(int k=0;k<16;k++)if((m.mask&(1<<k))!=0){int x=k%4,y=k/4;for(int t=0;t<r;t++){int old=x;x=3-y;y=old;}mask|=1<<(y*4+x);}masks.Add(mask);
                }
                variants.Add(string.Join(",",masks));
            }
            return l.activeLimit+":"+variants.OrderBy(s=>s,StringComparer.Ordinal).First()+":"+queues;
        }
        public static void Validate(Level l)
        {
            void Require(bool value,string message) { if(!value)throw new InvalidOperationException(l.id+": "+message); }
            Require(l.activeLimit>=1&&l.activeLimit<=2,"active limit"); Require(l.lanes.Length==3,"three lanes");
            Require(l.pieces.Length>0&&l.pieces.Length<=24,"piece budget"); Require(l.pieces.Select(p=>p.id).Distinct().Count()==l.pieces.Length,"unique IDs");
            foreach(var p in l.pieces)
            {
                Require(p.cells.Length>=1&&p.cells.Length<=4&&p.cells.Distinct().Count()==p.cells.Length,"piece size");
                var reached=new HashSet<Cell>{p.cells[0]}; bool changed=true;
                while(changed) { changed=false; foreach(var c in p.cells) if(!reached.Contains(c)&&reached.Any(q=>Math.Abs(q.x-c.x)+Math.Abs(q.y-c.y)==1)) { reached.Add(c);changed=true; } }
                Require(reached.Count==p.cells.Length,"connected pieces");
            }
            Require(l.moulds.All(m=>m.mask>0&&m.mask<=65535),"mould mask");
            Require(l.pieces.Sum(p=>p.cells.Length)==l.moulds.Sum(m=>Enumerable.Range(0,16).Count(i=>(m.mask&(1<<i))!=0)),"area");
            var s=Rules.Start(l); Rules.AssertConservation(l,s);
            foreach(var c in l.solution) { Require(Rules.Apply(l,s,c,out var next),"illegal solution action "+c); s=next; Rules.AssertConservation(l,s); }
            Require(Rules.Won(l,s),"trace does not win");
        }
    }
}
