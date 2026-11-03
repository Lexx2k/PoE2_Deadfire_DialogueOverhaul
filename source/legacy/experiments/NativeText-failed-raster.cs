using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace Deadfire.DialogueOverhaul
{
 public sealed class NativeText : MonoBehaviour
 {
  private Component renderer;
  private GameObject stage;
  private Camera camera;
  private RawImage image;
  private RenderTexture texture;
  private string value="";
  private float pixelsPerUnit=1, measuredWidth, measuredHeight;
  private bool dirty=true, quitting;
  private Color tint=Color.white;
  private static int stageIndex;
  public bool Hanging;
  public RectTransform rectTransform {get{return (RectTransform)transform;}}
  private static void Set(object target,string name,object val){PropertyInfo p=target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null&&p.CanWrite)p.SetValue(target,val,null);}
  public string text {get{return value;}set{this.value=value??"";dirty=true;}}
  public Color color {get{return tint;}set{tint=value;if(image!=null)image.color=value;}}
  public void Init(Component source,int fallbackSize)
  {
   if(source==null)throw new InvalidOperationException("Native dialogue text template unavailable");
   // Keep Deadfire's original world-space TMP glyph/shader pipeline intact.
   // The canvas displays its transparent render target, not an adapted mesh.
   var display=new GameObject("Native text image",typeof(RectTransform),typeof(RawImage));display.transform.SetParent(transform,false);image=display.GetComponent<RawImage>();image.raycastTarget=false;RectTransform displayRect=(RectTransform)display.transform;displayRect.anchorMin=Vector2.zero;displayRect.anchorMax=Vector2.one;displayRect.offsetMin=displayRect.offsetMax=Vector2.zero;
   object sc=DialogueView.Read(source,"color");if(sc is Color){tint=(Color)sc;tint.a=1;image.color=tint;}
   stage=new GameObject("Native dialogue text render stage");UnityEngine.Object.DontDestroyOnLoad(stage);
   stage.transform.position=new Vector3(100000+(++stageIndex)*2000,100000,0);
   var go=new GameObject("Native TMP mesh",typeof(RectTransform));go.layer=31;go.transform.SetParent(stage.transform,false);
   renderer=go.AddComponent(Type.GetType("TMPro.TextMeshPro, Assembly-CSharp",true));
   foreach(string property in new string[]{"font","fontSize","fontStyle","fontWeight","alignment","characterSpacing","wordSpacing","lineSpacing","paragraphSpacing","extraPadding","isOrthographic"}){object setting=DialogueView.Read(source,property);if(setting!=null)Set(renderer,property,setting);}
   Set(renderer,"enableAutoSizing",false);Set(renderer,"richText",true);Set(renderer,"enableWordWrapping",true);Set(renderer,"margin",Vector4.zero);Set(renderer,"color",Color.white);
   object font=DialogueView.Read(source,"font");Material basis=DialogueView.Read(font,"material") as Material;
   if(basis==null)basis=DialogueView.Read(source,"fontSharedMaterial") as Material;
   if(basis!=null){var material=new Material(basis);foreach(string keyword in new string[]{"MASK_HARD","MASK_SOFT","MASK_TEX","UNITY_UI_CLIP_RECT","UNITY_UI_ALPHACLIP"})material.DisableKeyword(keyword);if(material.HasProperty("_ClipRect"))material.SetVector("_ClipRect",new Vector4(-32767,-32767,32767,32767));if(material.HasProperty("_FaceColor"))material.SetColor("_FaceColor",Color.white);Set(renderer,"fontSharedMaterial",material);}
   Type ngui=Type.GetType("NGUITools, Assembly-CSharp");MethodInfo find=ngui==null?null:ngui.GetMethod("FindCameraForLayer",BindingFlags.Static|BindingFlags.Public,null,new Type[]{typeof(int)},null);
   Camera original=find==null?null:find.Invoke(null,new object[]{source.gameObject.layer}) as Camera;
   if(original!=null){Vector3 origin=source.transform.TransformPoint(Vector3.zero);float px=Vector3.Distance(original.WorldToScreenPoint(origin),original.WorldToScreenPoint(source.transform.TransformPoint(Vector3.up)));Canvas canvas=GetComponentInParent<Canvas>();pixelsPerUnit=px/Mathf.Max(.001f,canvas==null?1:canvas.scaleFactor);}
   if(pixelsPerUnit<.001f)pixelsPerUnit=1;
   var cg=new GameObject("Native dialogue text camera",typeof(Camera));cg.transform.SetParent(stage.transform,false);camera=cg.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.allowHDR=false;camera.allowMSAA=false;
   Debug.Log("[DeadfireDialogue] Original world-space TMP renderer; units-to-pixels="+pixelsPerUnit);
  }
  private string RenderText(){return(Hanging?"<pos="+(-16/pixelsPerUnit).ToString(CultureInfo.InvariantCulture)+">":"")+value;}
  public float Height(float width)
  {
   Vector2 preferred=(Vector2)MethodDispatch.Invoke(renderer,"GetPreferredValues",RenderText(),width/pixelsPerUnit,100000f);
   float height=Mathf.Max(1,Mathf.Ceil(preferred.y*pixelsPerUnit)+6);
   if(Mathf.Abs(measuredWidth-width)>.1f||Mathf.Abs(measuredHeight-height)>.1f)dirty=true;
   measuredWidth=width;measuredHeight=height;return height;
  }
  private void LateUpdate()
  {
   if(!dirty||renderer==null||measuredWidth<1||measuredHeight<1||quitting)return;
   try{Render();}catch(Exception ex){Debug.LogError("[DeadfireDialogue] Native text rasterization: "+ex);dirty=false;DialogueView view=GetComponentInParent<DialogueView>();if(view!=null)view.FailOpen();}
  }
  private void Render()
  {
   Canvas canvas=GetComponentInParent<Canvas>();float scale=canvas==null?1:canvas.scaleFactor;
   // Extra transparent space keeps the outdented first line inside the texture.
   float left=Hanging?16:0;image.rectTransform.offsetMin=new Vector2(-left,0);image.rectTransform.offsetMax=Vector2.zero;
   int width=Mathf.Max(1,Mathf.CeilToInt((measuredWidth+left)*scale));int height=Mathf.Max(1,Mathf.CeilToInt(measuredHeight*scale));
   if(texture==null||texture.width!=width||texture.height!=height){if(texture!=null){texture.Release();Destroy(texture);}texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32);texture.antiAliasing=1;texture.Create();image.texture=texture;}
   float lw=measuredWidth/pixelsPerUnit,lh=measuredHeight/pixelsPerUnit;
   RectTransform rect=(RectTransform)renderer.transform;rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(lw,lh);rect.localPosition=new Vector3(0,lh/2,0);
   Set(renderer,"text",RenderText());MethodDispatch.Invoke(renderer,"ForceMeshUpdate");
   camera.transform.localPosition=new Vector3((lw-left/pixelsPerUnit)/2,0,-10);camera.orthographicSize=lh/2;camera.aspect=(measuredWidth+left)/measuredHeight;camera.targetTexture=texture;camera.Render();dirty=false;
  }
  private void OnApplicationQuit(){quitting=true;}
  private void OnDestroy(){if(quitting)return;if(stage!=null)Destroy(stage);if(texture!=null){texture.Release();Destroy(texture);}}
 }
}

