using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocketToyworks
{
    public partial class WorkshopView
    {
        TMP_FontAsset displaySdf,bodySdf;Material titleMaterial;
        void InitTypography()
        {
            displaySdf=Resources.Load<TMP_FontAsset>("Fonts/DisplaySDF");bodySdf=Resources.Load<TMP_FontAsset>("Fonts/BodySDF");
            if(displaySdf==null||bodySdf==null)throw new System.InvalidOperationException("Generate the Toyworks SDF fonts before playing.");
            var outlined=Resources.Load<Material>("Fonts/DisplayOutline");
            if(outlined==null||!outlined.IsKeywordEnabled("OUTLINE_ON"))throw new System.InvalidOperationException("Missing outlined font preset. Regenerate the UI fonts.");
            titleMaterial=new Material(outlined);
        }
        TMP_Text Label(Transform parent,string value,Rect rect,int size,Color color,FontStyle style=FontStyle.Normal,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var text=Rect(value,parent,rect).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=style==FontStyle.Bold?displaySdf:bodySdf;text.fontStyle=FontStyles.Normal;
            text.text=value;text.richText=false;text.fontSize=size;text.color=color;text.raycastTarget=false;
            text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Truncate;
            text.margin=new Vector4(4,1,4,1);
            TextAlignmentOptions[] alignment={TextAlignmentOptions.TopLeft,TextAlignmentOptions.Top,TextAlignmentOptions.TopRight,TextAlignmentOptions.Left,TextAlignmentOptions.Center,TextAlignmentOptions.Right,TextAlignmentOptions.BottomLeft,TextAlignmentOptions.Bottom,TextAlignmentOptions.BottomRight};
            text.alignment=alignment[(int)align];return text;
        }
        TMP_Text Title(Transform parent,string value,Rect rect,int size=36,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var text=Label(parent,value,rect,size,Color.white,FontStyle.Bold,align);text.fontSharedMaterial=titleMaterial;return text;
        }
        RectTransform PieceSlot(Transform parent,Rect r,string name)
        {
            var slot=Rect(name,parent,r);slot.gameObject.AddComponent<RectMask2D>();return slot;
        }
    }
}
