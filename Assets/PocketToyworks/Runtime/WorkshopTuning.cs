using UnityEngine;
namespace PocketToyworks
{
    [CreateAssetMenu(menuName="Pocket Toyworks/Workshop tuning")]
    public class WorkshopTuning : ScriptableObject
    {
        public float liftSeconds=.10f,snapSeconds=.13f,returnSeconds=.15f,revealSeconds=.8f;
        public float fingerOffset=75;
        public Color cream=new Color32(227,239,253,255),teal=new Color32(51,135,225,255),ink=new Color32(32,53,88,255);
        public Color[] accents={new Color32(247,83,102,255),new Color32(255,202,49,255),new Color32(61,154,247,255),new Color32(166,94,236,255)};
    }
}
