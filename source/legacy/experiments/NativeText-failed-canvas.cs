using System;
using System.Reflection;
using UnityEngine;

namespace Deadfire.DialogueOverhaul
{
    // Render with the game's own TMP atlas/material. A uGUI Text substitute
    // cannot reproduce a signed-distance-field font's weight and outlines.
    public sealed class NativeText : MonoBehaviour
    {
        private Component renderer;
        private Material canvasMaterial;
        private string value = "";
        public bool Hanging;
        public RectTransform rectTransform { get { return (RectTransform)transform; } }
        private static void Set(object target, string name, object val)
        {
            PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite) p.SetValue(target, val, null);
        }
        public string text
        {
            get { return value; }
            set { this.value = value ?? ""; if (renderer != null) Set(renderer, "text", (Hanging ? "<pos=-16>" : "") + this.value); }
        }
        public Color color
        {
            get { object c = DialogueView.Read(renderer, "color"); return c is Color ? (Color)c : Color.white; }
            set { if (renderer != null) Set(renderer, "color", value); }
        }
        public void Init(Component source, int fallbackSize)
        {
            Type type = Type.GetType("TMPro.TextMeshProUGUI, Assembly-CSharp", true);
            renderer = gameObject.AddComponent(type);
            Set(renderer, "raycastTarget", false); Set(renderer, "enableAutoSizing", false);
            Set(renderer, "richText", true); Set(renderer, "enableWordWrapping", true);
            if (source == null) throw new InvalidOperationException("Native dialogue text template unavailable");
            foreach (string property in new string[] { "font", "fontSize", "fontStyle", "fontWeight", "alignment", "color", "characterSpacing", "wordSpacing", "lineSpacing", "paragraphSpacing", "extraPadding" })
            {
                object setting = DialogueView.Read(source, property);
                if (setting != null) Set(renderer, property, setting);
            }
            // The NGUI label's shared material contains its live clipping and
            // opacity state. Reusing that material clips arbitrary glyphs in a
            // different canvas. Use the same font asset's clean base material.
            object font = DialogueView.Read(source, "font");
            Material basis = DialogueView.Read(font, "material") as Material;
            if (basis == null) basis = DialogueView.Read(source, "fontSharedMaterial") as Material;
            if (basis != null)
            {
                canvasMaterial = new Material(basis);
                Shader uiShader = Shader.Find("TextMeshPro/Distance Field");
                if (uiShader != null) canvasMaterial.shader = uiShader;
                foreach (string keyword in new string[] { "MASK_HARD", "MASK_SOFT", "MASK_TEX", "UNITY_UI_CLIP_RECT", "UNITY_UI_ALPHACLIP" }) canvasMaterial.DisableKeyword(keyword);
                if (canvasMaterial.HasProperty("_ClipRect")) canvasMaterial.SetVector("_ClipRect", new Vector4(-32767, -32767, 32767, 32767));
                if (canvasMaterial.HasProperty("_FaceColor")) canvasMaterial.SetColor("_FaceColor", Color.white);
                if (canvasMaterial.HasProperty("_Stencil")) canvasMaterial.SetFloat("_Stencil", 0);
                if (canvasMaterial.HasProperty("_StencilComp")) canvasMaterial.SetFloat("_StencilComp", 8);
                Set(renderer, "fontSharedMaterial", canvasMaterial);
                Debug.Log("[DeadfireDialogue] Canvas font material: " + canvasMaterial.shader.name);
            }
            Set(renderer, "margin", Vector4.zero);
            // Convert native world-space TMP units into canvas reference units.
            Type ngui = Type.GetType("NGUITools, Assembly-CSharp");
            MethodInfo findCamera = ngui == null ? null : ngui.GetMethod("FindCameraForLayer", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(int) }, null);
            Camera camera = findCamera == null ? null : findCamera.Invoke(null, new object[] { source.gameObject.layer }) as Camera;
            if (camera != null)
            {
                Vector3 origin = source.transform.TransformPoint(Vector3.zero);
                float pixels = Vector3.Distance(camera.WorldToScreenPoint(origin), camera.WorldToScreenPoint(source.transform.TransformPoint(Vector3.up)));
                Vector2 nativeMeasure = (Vector2)MethodDispatch.Invoke(source, "GetPreferredValues", "M", 10000f, 10000f);
                Vector2 uiMeasure = (Vector2)MethodDispatch.Invoke(renderer, "GetPreferredValues", "M", 10000f, 10000f);
                Canvas canvas = GetComponentInParent<Canvas>();
                float scale = canvas == null ? 1 : canvas.scaleFactor;
                if (uiMeasure.y > .001f && pixels > .001f)
                {
                    float size = Convert.ToSingle(DialogueView.Read(source, "fontSize"));
                    Set(renderer, "fontSize", size * nativeMeasure.y * pixels / (uiMeasure.y * Mathf.Max(.001f, scale)));
                }
            }
        }
        public float Height(float width)
        {
            Vector2 size = (Vector2)MethodDispatch.Invoke(renderer, "GetPreferredValues", (Hanging ? "<pos=-16>" : "") + value, width, 100000f);
            return Mathf.Ceil(size.y) + 4;
        }
    }
}
