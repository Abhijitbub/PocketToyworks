using UnityEngine;
namespace PocketToyworks
{
    // Original pentatonic mallet theme. No sampled or reference-game audio.
    public static class WorkshopAudio
    {
        public static AudioClip Music()
        {
            const int rate=22050;const float beat=.48f;int count=(int)(rate*beat*32);var data=new float[count];
            int[] melody={72,76,79,76,74,0,72,79,81,79,76,0,74,76,72,0,76,79,84,79,81,0,79,76,74,72,69,0,72,76,72,0};
            for(int n=0;n<32;n++)
            {
                if(melody[n]>0)Note(data,rate,(int)(n*beat*rate),melody[n],.8f,.23f);
                if(n%4==0)Note(data,rate,(int)(n*beat*rate),new[]{48,53,57,55}[n/8],1.5f,.15f);
            }
            var clip=AudioClip.Create("Little Maker - original looping theme",count,1,rate,false);clip.SetData(data,0);return clip;
        }
        public static AudioClip Effect(bool complete)
        {
            const int rate=22050;var data=new float[(int)(rate*(complete?.85f:.12f))];
            if(complete){Note(data,rate,0,76,.55f,.35f);Note(data,rate,(int)(rate*.09f),79,.55f,.3f);Note(data,rate,(int)(rate*.18f),84,.6f,.32f);}
            else Note(data,rate,0,79,.12f,.45f);
            var clip=AudioClip.Create(complete?"Original toy completion sparkle":"Original plastic pop",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        static void Note(float[] data,int rate,int offset,int midi,float length,float volume)
        {
            float frequency=440*Mathf.Pow(2,(midi-69)/12f);
            for(int i=0;i<(int)(length*rate);i++){float t=i/(float)rate;float envelope=Mathf.Min(1,t*180)*Mathf.Exp(-t*5)*(1-t/length);float value=(Mathf.Sin(2*Mathf.PI*frequency*t)+.25f*Mathf.Sin(2*Mathf.PI*frequency*2*t))*envelope*volume;data[(offset+i)%data.Length]+=value;}
        }
    }
}
