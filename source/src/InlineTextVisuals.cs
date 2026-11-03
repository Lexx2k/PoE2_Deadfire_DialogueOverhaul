using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    // Runs after hanging indentation, before the shadow copies are generated.
    public sealed class InlineTextVisuals : BaseMeshEffect
    {
        private sealed class Hit { public Rect Bounds; public string Url; }
        private Text text;
        private Component source;
        private InlineDocument document;
        private readonly List<UIVertex> vertices = new List<UIVertex>();
        private readonly List<Hit> hits = new List<Hit>();
        private readonly Dictionary<int, Image> icons = new Dictionary<int, Image>();
        private string hovered;
        private float hoverStart;
        private bool historical;
        private bool geometryLogged;
        private bool refreshIcons;
        public bool Tooltips = true;
        public bool UseLinkColors = true;
        public Sprite TooltipIcon(string url)
        {
            if (document != null) foreach (InlineSpan span in document.Spans)
                if (span.Url == url && span.Icon != null) return NativeInlineAssets.Icon(span.Icon, source);
            return null;
        }

        public void Init(Text target, Component template) { text = target; source = template; }
        public string Prepare(string value)
        {
            InlineTooltip.Hide(this); hovered = null;
            foreach (Image icon in icons.Values) if (icon != null) { icon.gameObject.SetActive(false); Destroy(icon.gameObject); }
            icons.Clear(); hits.Clear();
            historical = value.StartsWith("<color=#787878>");
            geometryLogged = false;
            document = InlineMarkup.Parse(value, delegate(string name) { return NativeInlineAssets.Icon(name, source) != null; }, UseLinkColors ? (System.Func<string, string>)delegate(string url) {
                Color tint = NativeInlineAssets.LinkColor(source, url, false);
                if (historical) { tint.r *= .48f; tint.g *= .48f; tint.b *= .48f; }
                return "#" + ColorUtility.ToHtmlStringRGB(tint);
            } : null);
            if (document.Spans.Count > 0) foreach (InlineSpan span in document.Spans) Debug.Log("[DeadfireDialog] Inline span: start=" + span.Start + "; length=" + span.Length + "; url=" + span.Url + "; word=" + document.Plain.Substring(span.Start, span.Length));
            foreach (InlineSpan span in document.Spans)
            {
                if (span.Icon == null) continue;
                GameObject go = new GameObject("Native inline " + span.Icon, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                Image image = go.GetComponent<Image>(); image.sprite = NativeInlineAssets.Icon(span.Icon, source); image.preserveAspect = false; image.raycastTarget = false;
                TranscriptFade fade = GetComponent<TranscriptFade>(); if (fade != null) go.AddComponent<TranscriptFade>().Viewport = fade.Viewport;
                RectTransform rect = image.rectTransform; rect.anchorMin = rect.anchorMax = text.rectTransform.pivot;
                // A closing reply can recreate historical text before its next
                // mesh rebuild. Never expose Image's default 100x100 rectangle.
                Vector2 size = image.sprite.rect.size;
                rect.sizeDelta = size * (text.fontSize / Mathf.Max(size.x, size.y));
                Color initial;
                if (span.Url != null && UseLinkColors) initial = NativeInlineAssets.LinkColor(source, span.Url, false);
                else ColorUtility.TryParseHtmlString(span.Color ?? "#FFFFFF", out initial);
                image.color = initial;
                icons[span.Start] = image;
            }
            return document.Text;
        }
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || document == null || document.Spans.Count == 0) return;
            vertices.Clear(); helper.GetUIVertexStream(vertices); hits.Clear();
            int count = vertices.Count / 6;
            foreach (InlineSpan span in document.Spans)
            {
                bool nativeTint = span.Url != null && UseLinkColors;
                Color color = nativeTint ? NativeInlineAssets.LinkColor(source, span.Url, hovered == span.Url) : text.color;
                if (historical && nativeTint && span.Icon == null && hovered != span.Url) { color.r *= .48f; color.g *= .48f; color.b *= .48f; }
                if (span.Icon != null && !nativeTint) ColorUtility.TryParseHtmlString(span.Color ?? "#FFFFFF", out color);
                for (int character = span.Start; character < span.Start + span.Length; character++)
                {
                    int quad = InlineMarkup.QuadIndex(document, character, count);
                    if (quad < 0 || quad >= count) continue;
                    int index = quad * 6;
                    Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                    for (int j = 0; j < 6; j++) { Vector3 p = vertices[index + j].position; min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                    Rect bounds = new Rect(min, max - min);
                    if (span.Url != null && bounds.width > .1f && bounds.height > .1f) hits.Add(new Hit { Bounds = bounds, Url = span.Url });
                    if (span.Icon != null)
                    {
                        Image image; if (!icons.TryGetValue(character, out image) || image == null) continue;
                        // Prepare runs before Place changes the text pivot. Use
                        // mesh-local coordinates, not the stale parent anchor.
                        // Fit the sprite once into a fixed rectangle: closing
                        // clips this geometry rather than recalculating aspect.
                        RectTransform rect = image.rectTransform;
                        rect.anchorMin = rect.anchorMax = text.rectTransform.pivot;
                        rect.pivot = new Vector2(.5f, .5f);
                        Vector2 size = image.sprite.rect.size;
                        size *= text.fontSize / Mathf.Max(size.x, size.y);
                        rect.sizeDelta = size;
                        rect.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0);
                        rect.localScale = Vector3.one;
                        image.color = color;
                        refreshIcons = true;
                        if (span.Url != null) hits.Add(new Hit { Bounds = new Rect(bounds.center - size * .5f, size), Url = span.Url });
                        for (int j = 0; j < 6; j++) { UIVertex v = vertices[index + j]; v.color.a = 0; vertices[index + j] = v; }
                    }
                    else if (nativeTint)
                        for (int j = 0; j < 6; j++) { UIVertex v = vertices[index + j]; Color32 tint = color; tint.a = v.color.a; v.color = tint; vertices[index + j] = v; }
                }
            }
            helper.Clear(); helper.AddUIVertexTriangleStream(vertices);
            if (!geometryLogged) { geometryLogged = true; Debug.Log("[DeadfireDialog] Inline geometry: plain=" + document.Plain.Length + "; source=" + document.Text.Length + "; quads=" + count + "; hit cells=" + hits.Count + "; enabled=" + IsActive()); }
        }
        private void Update()
        {
            foreach (CanvasGroup group in GetComponentsInParent<CanvasGroup>())
                if (!group.interactable)
                {
                    InlineTooltip.Hide(this);
                    if (hovered != null) { hovered = null; if (text != null) text.SetVerticesDirty(); }
                    return;
                }
            if (!Tooltips || text == null || hits.Count == 0) return;
            Vector2 point; string current = null;
            bool visible = RectTransformUtility.ScreenPointToLocalPointInRectangle(text.rectTransform, Input.mousePosition, null, out point);
            Rect textBounds = text.rectTransform.rect;
            visible = visible && point.x >= textBounds.xMin - HangingIndent.Amount && point.x <= textBounds.xMax && point.y >= textBounds.yMin && point.y <= textBounds.yMax;
            if (visible)
                foreach (Mask mask in GetComponentsInParent<Mask>()) if (mask.enabled && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)mask.transform, Input.mousePosition, null)) { visible = false; break; }
            if (visible)
                foreach (RectMask2D mask in GetComponentsInParent<RectMask2D>()) if (mask.enabled && !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)mask.transform, Input.mousePosition, null)) { visible = false; break; }
            if (visible) foreach (Hit hit in hits) if (hit.Bounds.Contains(point)) { current = hit.Url; break; }
            if (current != hovered)
            {
                hovered = current; hoverStart = Time.unscaledTime; InlineTooltip.Hide(this); text.SetVerticesDirty();
                if (current != null) Debug.Log("[DeadfireDialog] Inline hover entered " + current);
            }
            if (hovered != null && Time.unscaledTime - hoverStart >= .35f) InlineTooltip.Show(this, hovered);
        }
        private void LateUpdate()
        {
            if (!refreshIcons) return;
            refreshIcons = false;
            // Child graphic updates requested inside a parent mesh rebuild can
            // be discarded by legacy uGUI. Submit them outside that rebuild.
            foreach (Image image in icons.Values) if (image != null) image.SetVerticesDirty();
        }
        protected override void OnDisable() { InlineTooltip.Hide(this); base.OnDisable(); }
    }
}
