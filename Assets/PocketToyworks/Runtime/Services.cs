using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEngine;
using PocketToyworks.Core;

namespace PocketToyworks
{
    [Serializable] public class Preferences { public bool musicMuted=false,sound=true,haptics=false,reducedMotion=false; }
    [Serializable] public class SaveData
    {
        public PlayerProgress player;
        public bool suspendedChallenge,suspendedFailed,suspendedAttemptCounted,suspendedChallengeStarted;public double suspendedRemaining;
        public bool hasSuspendedSession;public Session suspendedSession;public double suspendedSeconds;public int suspendedActions;
        public int version=1,contentVersion=1; public Session session; public Preferences settings=new Preferences();
        public List<string> completed=new List<string>(); public double activeSeconds; public int actionCount;
        public List<PausedAttempt> pausedAttempts=new List<PausedAttempt>();
    }
    [Serializable] class SaveEnvelope { public string payload,checksum; }
    public sealed class SaveStore
    {
        readonly string path; public string LastError {get;private set;}
        public SaveStore(string directory) { Directory.CreateDirectory(directory);path=Path.Combine(directory,"prototype-v1.json"); }
        static string Hash(string value) { using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        public bool Write(SaveData data)
        {
            try
            {
                var json=JsonUtility.ToJson(data);var envelope=JsonUtility.ToJson(new SaveEnvelope{payload=json,checksum=Hash(json)});
                using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
                {var bytes=Encoding.UTF8.GetBytes(envelope);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(path+".tmp",path,path+".previous",true);else File.Move(path+".tmp",path);
                LastError=null;return true;
            }
            catch(Exception e) { LastError=e.Message;Debug.LogWarning("Save: "+e.Message);return false; }
        }
        public SaveData Read(Func<string,Level> resolve)
        {
            foreach(var candidate in new[]{path,path+".previous"})
            {
                try
                {
                    if(!File.Exists(candidate))continue;
                    var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(candidate));
                    if(envelope==null||envelope.payload==null||Hash(envelope.payload)!=envelope.checksum)continue;
                    var data=JsonUtility.FromJson<SaveData>(envelope.payload);
                    if(data==null||data.version!=1||data.contentVersion!=1||data.settings==null||data.completed==null||data.session==null)continue;
                    var l=resolve(data.session.state.levelId);if(l==null||l.version!=data.contentVersion)continue;
                    ValidateSession(l,data.session);if(data.hasSuspendedSession)ValidateSession(resolve(data.suspendedSession.state.levelId),data.suspendedSession);
                    if(data.pausedAttempts!=null)foreach(var attempt in data.pausedAttempts){var level=resolve(attempt.levelId);if(level==null||attempt.session.state.levelId!=attempt.levelId||double.IsNaN(attempt.remainingSeconds)||double.IsInfinity(attempt.remainingSeconds)||attempt.remainingSeconds<0)throw new InvalidDataException("Invalid paused attempt");ValidateSession(level,attempt.session);}
                    return data;
                }
                catch(Exception e) { LastError=e.Message; }
            }
            return null;
        }
        public static void ValidateSession(Level l,Session session)
        {
            if(session.history==null)throw new InvalidDataException("Missing history");
            var previous=Rules.Start(l);
            var snapshots=session.history.Concat(new[]{session.state}).ToArray();
            for(int i=0;i<snapshots.Length;i++)
            {
                var s=snapshots[i];Rules.AssertConservation(l,s);
                if(i==0) {if(JsonUtility.ToJson(previous)!=JsonUtility.ToJson(s))throw new InvalidDataException("Invalid initial snapshot");}
                else
                {
                    bool reachable=Rules.Moves(l,previous).Any(c=>{Rules.Apply(l,previous,c,out var next);return JsonUtility.ToJson(next)==JsonUtility.ToJson(s);});
                    if(!reachable)throw new InvalidDataException("Invalid history transition");
                }
                previous=s;
            }
        }
    }
    public interface IAds { bool Available {get;} void RequestReward(Action<bool> result); }
    public static class Haptics
    {
        public static void Pulse()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var vibrator=activity.Call<AndroidJavaObject>("getSystemService","vibrator"))
                using(var effects=new AndroidJavaClass("android.os.VibrationEffect"))
                using(var effect=effects.CallStatic<AndroidJavaObject>("createOneShot",12L,42))
                    vibrator.Call("vibrate",effect);
            }
            catch(Exception e) {Debug.LogWarning("Haptics unavailable: "+e.Message);}
#endif
        }
    }
    public sealed class NoAds : IAds { public bool Available=>false; public void RequestReward(Action<bool> result)=>result(false); }
    [Serializable] public class LocalEvent
    {
        public string name,levelId,sessionId,detail,utc,experiment; public int contentVersion,actionCount;public double activePlaySeconds;
    }
    public sealed class Telemetry
    {
        readonly string path;readonly string session=Guid.NewGuid().ToString("N");
        public Telemetry(string directory) { Directory.CreateDirectory(directory);path=Path.Combine(directory,"playtest-events.jsonl"); }
        public void Record(string name,Level level,SaveData data,string detail="")
        {
            try {File.AppendAllText(path,JsonUtility.ToJson(new LocalEvent{name=name,levelId=level.id,contentVersion=level.version,sessionId=session,detail=detail,utc=DateTime.UtcNow.ToString("o"),actionCount=data.actionCount,activePlaySeconds=data.activeSeconds,experiment=level.id.StartsWith("compare")?level.id:"authored"})+Environment.NewLine);}
            catch(Exception e) {Debug.LogWarning("Local telemetry unavailable: "+e.Message);}
        }
    }
    public static class Content
    {
        static Dictionary<string,Level> levels;
        public static IEnumerable<Level> All { get { Load();return levels.Values.OrderBy(l=>l.id); } }
        public static Level Get(string id) {Load();return levels.TryGetValue(id,out var l)?l:null;}
        static void Load()
        {
            if(levels!=null)return;levels=new Dictionary<string,Level>();
            foreach(var asset in Resources.LoadAll<TextAsset>("Levels")) {var level=JsonUtility.FromJson<Level>(asset.text);Validator.Validate(level);levels.Add(level.id,level);}
        }
    }
}
