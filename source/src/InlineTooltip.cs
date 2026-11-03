using UnityEngine;
using UnityEngine.UI;

namespace Deadfire.DialogOverhaul
{
    internal static class InlineTooltip
    {
        private static InlineTextVisuals owner;
        private static string activeUrl;
        private static RectTransform panel;
        private static NativeText label;
        private static NativeText titleLabel;
        private static Image iconImage;
        public static void Hide(InlineTextVisuals caller)
        {
            if (owner != caller) return;
            if (panel != null) panel.gameObject.SetActive(false);
            owner = null; activeUrl = null;
        }
        public static void Show(InlineTextVisuals caller, string url)
        {
            if (owner == caller && activeUrl == url && panel != null && panel.gameObject.activeSelf) return;
            string title;
            string content = NativeInlineAssets.Tooltip(url, out title);
            // Reaction topics have a localized name and icon, but no body.
            if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(title)) return;
            Sprite icon = caller.TooltipIcon(url);
            Canvas canvas = caller.GetComponentInParent<Canvas>(); if (canvas == null) return;
            if (panel == null)
            {
                GameObject go = new GameObject("Dialogue inline tooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                panel = go.GetComponent<RectTransform>();
                Image background = go.GetComponent<Image>(); background.color = new Color(.025f, .025f, .022f, .98f); background.raycastTarget = false;
                Component nativeBackground = null, nativeTitle = null, nativeBody = null, nativeFrame = null;
                System.Type tooltipType = System.Type.GetType("Game.UI.UIAbilityTooltip, Assembly-CSharp");
                if (tooltipType != null)
                    foreach (UnityEngine.Object item in Resources.FindObjectsOfTypeAll(tooltipType))
                    {
                        Component template = DialogueView.Read(item, "ContentTemplate") as Component;
                        Component backing = DialogueView.Read(item, "Background") as Component;
                        if (template == null || backing == null) continue;
                        nativeBackground = backing; nativeTitle = DialogueView.Read(template, "TitleLabel") as Component; nativeBody = DialogueView.Read(template, "DescLabel") as Component;
                        foreach (Component child in ((Component)item).GetComponentsInChildren<Component>(true))
                            if (child != null && System.Convert.ToString(DialogueView.Read(child, "spriteName")) == "tooltip_frame") { nativeFrame = child; break; }
                        break;
                    }
                if (nativeBackground != null)
                {
                    Sprite sprite = NativeSprites.Copy(nativeBackground);
                    if (sprite != null) { background.sprite = sprite; background.type = Image.Type.Sliced; background.color = Color.white; }
                }
                if (nativeFrame != null)
                {
                    Sprite sprite = NativeSprites.Copy(nativeFrame);
                    if (sprite != null)
                    {
                        GameObject border = new GameObject("Native tooltip frame", typeof(RectTransform), typeof(Image)); border.transform.SetParent(panel, false);
                        Image image = border.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Color.white; image.raycastTarget = false;
                        RectTransform rect = image.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                        // Native frame and backing both use a TOP-LEFT pivot.
                        // Their size difference extends right/down, not equally
                        // around all sides of the fill.
                        // The live native tooltip is animated/repositioned.
                        // Its authored frame extends six pixels right/down;
                        // never copy its current screen/layout displacement.
                        rect.offsetMin = new Vector2(0, -6); rect.offsetMax = new Vector2(6, 0);
                        foreach (Component corner in nativeFrame.transform.parent.GetComponentsInChildren<Component>(true))
                        {
                            if (corner == null) continue;
                            string spriteName = System.Convert.ToString(DialogueView.Read(corner, "spriteName"));
                            if (spriteName == "tooltip_cornerTL" || spriteName == "tooltip_cornerBR") AddCorner(corner, nativeBackground, panel, spriteName == "tooltip_cornerTL");
                        }
                        Debug.Log("[DeadfireDialog] Native tooltip_frame border applied.");
                    }
                }
                go.GetComponent<CanvasGroup>().blocksRaycasts = false;
                GameObject iconObject = new GameObject("Native reaction tooltip icon", typeof(RectTransform), typeof(Image)); iconObject.transform.SetParent(panel, false);
                iconImage = iconObject.GetComponent<Image>(); iconImage.raycastTarget = false; iconImage.preserveAspect = true; iconImage.color = Color.white;
                RectTransform iconRect = iconImage.rectTransform; iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0, 1);
                iconRect.anchoredPosition = new Vector2(15, -12); iconRect.sizeDelta = new Vector2(72, 72);
                GameObject heading = new GameObject("Native tooltip title", typeof(RectTransform)); heading.transform.SetParent(panel, false);
                titleLabel = heading.AddComponent<NativeText>(); titleLabel.Init(nativeTitle, FontSize(nativeTitle, 20)); titleLabel.GetComponent<InlineTextVisuals>().Tooltips = false;
                titleLabel.rectTransform.anchorMin = new Vector2(0, 1); titleLabel.rectTransform.anchorMax = Vector2.one; titleLabel.rectTransform.pivot = new Vector2(.5f, 1);
                titleLabel.color = new Color(232f / 255, 203f / 255, 158f / 255);
                GameObject text = new GameObject("Native tooltip content", typeof(RectTransform)); text.transform.SetParent(panel, false);
                label = text.AddComponent<NativeText>(); label.Init(nativeBody, FontSize(nativeBody, 17)); label.GetComponent<InlineTextVisuals>().Tooltips = false;
                label.rectTransform.anchorMin = new Vector2(0, 1); label.rectTransform.anchorMax = Vector2.one; label.rectTransform.pivot = new Vector2(.5f, 1);
            }
            panel.SetParent(canvas.transform, false); panel.SetAsLastSibling(); panel.gameObject.SetActive(true);
            owner = caller; activeUrl = url;
            label.text = Formatting.Clean(content ?? ""); label.color = Color.white;
            label.gameObject.SetActive(!string.IsNullOrEmpty(content));
            iconImage.sprite = icon; iconImage.gameObject.SetActive(icon != null);
            titleLabel.text = Formatting.Clean(title ?? ""); titleLabel.gameObject.SetActive(!string.IsNullOrEmpty(title));
            float width = Mathf.Min(icon != null && string.IsNullOrEmpty(content) ? 270 : 420, ((RectTransform)canvas.transform).rect.width - 40);
            float left = icon == null ? 15 : 102;
            float titleHeight = string.IsNullOrEmpty(title) ? 0 : titleLabel.Height(width - left - 15);
            float bodyHeight = string.IsNullOrEmpty(content) ? 0 : label.Height(width - left - 15);
            float bodyTop = 12 + titleHeight + (titleHeight > 0 ? 6 : 0);
            titleLabel.rectTransform.offsetMin = new Vector2(left, -12 - titleHeight); titleLabel.rectTransform.offsetMax = new Vector2(-15, -12);
            label.rectTransform.offsetMin = new Vector2(left, -bodyTop - bodyHeight); label.rectTransform.offsetMax = new Vector2(-15, -bodyTop);
            float height = Mathf.Max(bodyTop + bodyHeight + 14, icon == null ? 0 : 98);
            panel.sizeDelta = new Vector2(width, height); panel.pivot = new Vector2(0, 1);
            RectTransform root = (RectTransform)canvas.transform; panel.anchorMin = panel.anchorMax = root.pivot;
            Vector2 point; RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, null, out point);
            float cursorY = point.y;
            point += new Vector2(18, height + 12);
            if (point.y > root.rect.yMax - 12) point.y = cursorY - 12;
            if (point.x + width > root.rect.xMax - 12) point.x -= width + 36;
            point.x = Mathf.Max(root.rect.xMin + 12, point.x);
            point.y = Mathf.Clamp(point.y, root.rect.yMin + height + 12, root.rect.yMax - 12);
            panel.anchoredPosition = point;
        }
        private static int FontSize(Component source, int fallback)
        {
            object size = DialogueView.Read(source, "fontSize"); return size == null ? fallback : Mathf.RoundToInt(System.Convert.ToSingle(size));
        }
        private static void AddCorner(Component source, Component backing, RectTransform parent, bool topLeft)
        {
            Sprite sprite = NativeSprites.Copy(source); if (sprite == null) return;
            GameObject go = new GameObject("Native tooltip " + (topLeft ? "top-left trim" : "bottom-right trim"), typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>(); image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
            RectTransform rect = image.rectTransform; Vector2 anchor = topLeft ? new Vector2(0, 1) : new Vector2(1, 0);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = topLeft ? new Vector2(-4, 4) : new Vector2(4, -4);
            rect.sizeDelta = topLeft ? new Vector2(24, 29) : new Vector2(30, 27);
            rect.localRotation = source.transform.localRotation;
        }
    }
}
