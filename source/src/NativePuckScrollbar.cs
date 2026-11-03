using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Deadfire.DialogOverhaul
{
    // Deadfire's native control uses a fixed-size puck, not a proportional thumb.
    public sealed class NativePuckScrollbar : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        private ScrollRect scroll;
        private RectTransform rail, puck;
        private CanvasGroup group;
        private Action manual;
        private float endInset = 24;
        public void Init(ScrollRect target, Component native, Action onManual)
        {
            scroll = target; rail = (RectTransform)transform; manual = onManual;
            group = gameObject.AddComponent<CanvasGroup>();
            Image hitbox = GetComponent<Image>(); hitbox.sprite = null; hitbox.color = Color.clear;
            rail.sizeDelta = new Vector2(28, 0);
            Image track = Graphic("Native track", "m_trackSprite", native, false);
            if (track != null) { Stretch(track.rectTransform); track.rectTransform.offsetMin = new Vector2(9, endInset); track.rectTransform.offsetMax = new Vector2(-9, -endInset); }
            Image thumb = Graphic("Native puck", "m_puckSprite", native, false);
            if (thumb != null)
            {
                puck = thumb.rectTransform; puck.anchorMin = puck.anchorMax = new Vector2(.5f, .5f); puck.pivot = new Vector2(.5f, .5f);
                puck.sizeDelta = new Vector2(Mathf.Clamp(thumb.sprite.rect.width, 8, 28), Mathf.Clamp(thumb.sprite.rect.height, 18, 48));
            }
            Arrow("Native up arrow", "m_upArrowSprite", native, true);
            Arrow("Native down arrow", "m_downArrowSprite", native, false);
            scroll.onValueChanged.AddListener(delegate(Vector2 ignored) { Reposition(); });
            Reposition();
            Debug.Log("[DeadfireDialog] Native UIPuckScrollbar skin connected to transcript.");
        }
        private Image Graphic(string name, string field, Component native, bool raycast)
        {
            Component original = DialogueView.Read(native, field) as Component;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform, false);
            Image image = go.GetComponent<Image>(); image.raycastTarget = raycast;
            if (!NativeSprites.Apply(image, original)) { go.SetActive(false); return null; }
            return image;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private void Arrow(string name, string field, Component native, bool up)
        {
            Image image = Graphic(name, field, native, true); if (image == null) return;
            RectTransform r = image.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, up ? 1 : 0); r.pivot = new Vector2(.5f, up ? 1 : 0);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(Mathf.Clamp(image.sprite.rect.width, 10, 26), Mathf.Clamp(image.sprite.rect.height, 14, 24));
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(delegate { UserScroll(); float range = Range(); if (range > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + (up ? 1 : -1) * 48 / range); });
        }
        private float Range() { return Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height); }
        private void UserScroll() { if (manual != null) manual(); scroll.StopMovement(); }
        private void SetFromPointer(PointerEventData data)
        {
            if (Range() <= .5f) return;
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rail, data.position, data.pressEventCamera, out point)) return;
            float halfPuck = puck == null ? 12 : puck.rect.height / 2;
            float low = rail.rect.yMin + endInset + halfPuck, high = rail.rect.yMax - endInset - halfPuck;
            scroll.verticalNormalizedPosition = Mathf.InverseLerp(low, high, point.y);
        }
        public void OnPointerDown(PointerEventData data) { UserScroll(); SetFromPointer(data); }
        public void OnDrag(PointerEventData data) { UserScroll(); SetFromPointer(data); }
        public void OnScroll(PointerEventData data) { UserScroll(); scroll.OnScroll(data); }
        private void LateUpdate() { Reposition(); }
        private void Reposition()
        {
            if (scroll == null) return;
            bool needed = Range() > .5f; group.alpha = needed ? 1 : 0; group.blocksRaycasts = needed;
            if (puck == null) return;
            float halfPuck = puck.rect.height / 2;
            float low = rail.rect.yMin + endInset + halfPuck, high = rail.rect.yMax - endInset - halfPuck;
            puck.anchoredPosition = new Vector2(0, Mathf.Lerp(low, high, Mathf.Clamp01(scroll.verticalNormalizedPosition)));
        }
    }
}
