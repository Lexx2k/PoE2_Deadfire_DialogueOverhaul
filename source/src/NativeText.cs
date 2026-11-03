using UnityEngine;
using UnityEngine.UI;
namespace Deadfire.DialogOverhaul
{
 // Stable legacy uGUI renderer. Exact-native-font experiments are kept out
 // of this runtime build until they have verified visual results.
 public sealed class NativeText : MonoBehaviour
 {
  private Text graphic;
  private HangingIndent indent;
  private InlineTextVisuals inline;
  private string value="";
  private static Font font;
  public bool Hanging;
  public RectTransform rectTransform {get{return (RectTransform)transform;}}
  public string text
  {
   get{return value;}
   set{this.value=value??"";if(graphic!=null){graphic.text=inline.Prepare(this.value);indent.enabled=Hanging;graphic.SetVerticesDirty();}}
  }
  public Color color {get{return graphic==null?Color.white:graphic.color;}set{if(graphic!=null)graphic.color=value;}}
  public void Init(Component source,int fallbackSize)
  {
   if(font==null)font=Font.CreateDynamicFontFromOSFont(new string[]{"EB Garamond","Georgia","Times New Roman"},22);
   if(font==null)font=Resources.GetBuiltinResource<Font>("Arial.ttf");
   graphic=gameObject.AddComponent<Text>();graphic.font=font;graphic.fontSize=fallbackSize;graphic.lineSpacing=1.16f;
   object nativeStyle=DialogueView.Read(source,"fontStyle");
   int style=nativeStyle==null?0:System.Convert.ToInt32(nativeStyle);
   graphic.fontStyle=(style&3)==3?FontStyle.BoldAndItalic:(style&1)!=0?FontStyle.Bold:(style&2)!=0?FontStyle.Italic:FontStyle.Normal;
   graphic.alignment=TextAnchor.UpperLeft;graphic.supportRichText=true;graphic.raycastTarget=false;
   graphic.horizontalOverflow=HorizontalWrapMode.Wrap;graphic.verticalOverflow=VerticalWrapMode.Overflow;graphic.color=new Color(.83f,.81f,.75f);
   indent=gameObject.AddComponent<HangingIndent>();indent.enabled=Hanging;
   inline=gameObject.AddComponent<InlineTextVisuals>();inline.Init(graphic,source);
   // Apply after indentation so the outline follows the finished glyph positions.
   gameObject.AddComponent<SoftTextOutline>();
  }
  public float Height(float width)
  {
   TextGenerationSettings settings=graphic.GetGenerationSettings(new Vector2(width,0));settings.verticalOverflow=VerticalWrapMode.Overflow;
   return Mathf.Ceil(graphic.cachedTextGeneratorForLayout.GetPreferredHeight(graphic.text,settings)/graphic.pixelsPerUnit)+4;
  }
 }
}
