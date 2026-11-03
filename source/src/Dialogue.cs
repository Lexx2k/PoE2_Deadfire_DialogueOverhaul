using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Deadfire.DialogOverhaul
{
    // Called by small verified IL hooks. A separate canvas keeps the native
    // conversation engine, response scripts and VO lifecycle intact.
    public static class Hooks
    {
        private static DialogueView view;
        public static void Begin(Component manager) { Guard(delegate { if (view == null) { var go = new GameObject("Deadfire Dialog Overhaul"); UnityEngine.Object.DontDestroyOnLoad(go); view = go.AddComponent<DialogueView>(); } view.Observe(manager); }); }
        public static void Capture(Component manager) { Guard(delegate { if (view != null) view.Capture(manager); }); }
        public static void BeforeCapture(Component manager) { Guard(delegate { if (view != null) view.BeforeNativeRebuild(manager); }); }
        public static void Reply(Component manager, string text) { Guard(delegate { if (view != null) view.Reply(manager, text); }); }
        public static void Advance(Component manager) { Guard(delegate { if (view != null) view.Advance(manager); }); }
        public static void Options(Component manager) { try { DialogueStyleOption.Attach(manager); } catch (Exception ex) { Debug.LogError("[DeadfireDialog] Options checkbox: " + ex); } }
        public static void SetStyle(bool enabled)
        {
            DialoguePreferences.Enabled = enabled;
            Guard(delegate { if (view != null) view.SetStyle(enabled); });
        }
        public static bool End(bool hidden, Component manager) { Guard(delegate { if (view != null) view.End(manager); }); return hidden; }
        public static Vector3 CameraOffset(Vector3 original, Component manager)
        {
            try { return view != null ? view.CameraOffset(original, manager) : original; }
            catch (Exception ex) { Debug.LogError("[DeadfireDialog] Camera offset: " + ex); return original; }
        }
        private static void Guard(Action action) { try { action(); } catch (Exception ex) { Debug.LogError("[DeadfireDialog] " + ex); if (view != null) view.FailOpen(); } }
    }

    public sealed class DialogueView : MonoBehaviour
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private Component manager;
        private Canvas canvas;
        private RectTransform column, content, options;
        private RectTransform optionClip, optionRows;
        private CanvasGroup optionInteraction;
        private bool optionsPendingReveal, optionsRevealing, selectionCaptured;
        private float optionRevealTime, selectedChoiceY;
        private int selectedReplyAnchor = -1;
        private int movingReplyIndex = -1;
        private float replyExtraStart, replyExtraOffset, activeRevealDuration;
        private const float SelectedReplyDuration = .25f;
        private const float OptionRevealDuration = .2f;
        private ScrollRect scroll;
        private RawImage portrait;
        private RectTransform portraitFrameRect;
        private AspectRatioFitter portraitAspect;
        private Image portraitMaskImage;
        private Mask portraitMask;
        private Texture nativePortraitMask;
        private Texture nativePortraitSource;
        private Texture2D maskedPortraitTexture;
        private NativeText portraitName;
        private Component speakerTemplate, responseTemplate;
        private bool active, dirty, follow, busy;
        private int revision, lastRevision = -1;
        private int currentPassageIndex = -1;
        private bool waitingForNextPassage;
        private int replyCommittedFrame;
        private string lastNode, optionsKey;
        private string lastTranscriptSpeaker;
        private object chart;
        private float lastHeight;
        private readonly List<Action> actions = new List<Action>();
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<bool> choicesAvailable = new List<bool>();
        private readonly Dictionary<Component, float> hiddenWidgets = new Dictionary<Component, float>();
        private readonly Dictionary<Collider, bool> hiddenColliders = new Dictionary<Collider, bool>();
        private readonly Dictionary<Collider2D, bool> hiddenColliders2D = new Dictionary<Collider2D, bool>();
        private readonly List<RectTransform> entries = new List<RectTransform>();
        private readonly List<string> transcript = new List<string>();
        private readonly List<string> transcriptNames = new List<string>();
        private bool fallback, nativePreview, frameLogged, quitting;
        private Component observedManager;
        private bool pendingEnd;
        private int endFrame;
        private bool hasLayout, revealing;
        private float revealStart, revealOffset, revealTime;
        private const float RevealDuration = .25f;
        private RectTransform rollClip, rollContent;
        private CanvasGroup rollInteraction;
        private int rollDirection;
        private float rollTime;
        private bool closing;
        private const float RollDuration = .45f;

        public Vector3 CameraOffset(Vector3 original, Component owner)
        {
            if (!active || fallback || owner != manager || column == null) return original;
            Camera world = Camera.main;
            if (world == null) return original;
            Vector3[] corners = new Vector3[4]; column.GetWorldCorners(corners);
            float panelLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]).x;
            float focusX = DialogueCameraLayout.FocusViewportX(world.pixelRect.xMin, world.pixelRect.width, panelLeft, 12f * Screen.height / 1080f);
            GameObject speaker = Read(owner, "ActiveSpeaker") as GameObject;
            float depth = speaker == null ? world.nearClipPlane + 1 : world.WorldToViewportPoint(speaker.transform.position).z;
            depth = Mathf.Max(world.nearClipPlane + .1f, depth);
            Vector3 center = world.ViewportToWorldPoint(new Vector3(.5f, .5f, depth));
            Vector3 shifted = world.ViewportToWorldPoint(new Vector3(1 - focusX, .5f, depth));
            // FocusOnObject already adds half the speaker's height. Replace the
            // legacy bottom-dialogue offset, then move camera aim to the right
            // so the speaker projects onto the centre of the unobstructed area.
            Debug.Log("[DeadfireDialog] Dialogue camera focus viewport x=" + focusX);
            return shifted - center;
        }

        public void Observe(Component owner)
        {
            if (closing) { closing = false; rollDirection = 0; FinishEnd(); }
            observedManager = owner;
            pendingEnd = false;
            if (active) { manager = owner; return; }
            Begin(owner);
        }

        private void Update()
        {
            // No activation hotkey: function keys can also trigger the game's
            // own quickload/UI bindings. Activation follows the native window.
        }

        internal static object Read(object obj, string name)
        {
            if (obj == null) return null;
            for (Type type = obj.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(obj);
                PropertyInfo prop = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (prop != null && prop.GetIndexParameters().Length == 0) return prop.GetValue(obj, null);
            }
            return null;
        }
        private static object Call(object obj, string name, params object[] args)
        {
            return MethodDispatch.Invoke(obj, name, args);
        }
        private static string Str(object value) { return value == null ? "" : value.ToString(); }
        private static GameObject Go(object obj) { Component c = obj as Component; return c != null ? c.gameObject : obj as GameObject; }

        public void Begin(Component owner)
        {
            // ShowWindow can be requested again during a node refresh. It must
            // not reset a running transcript (including an F10 fallback).
            if (active && manager == owner && chart == Read(owner, "m_activeFlowChart")) return;
            Restore(); manager = owner; active = true; fallback = nativePreview = !DialoguePreferences.Enabled; frameLogged = false;
            Formatting.IconLinkLabel = ResolveIconLabel;
            speakerTemplate = Read(Read(owner, "SpeakerBox"), "Label") as Component;
            responseTemplate = Read(Read(owner, "ResponseBox"), "Label") as Component;
            revision = 0; lastRevision = -1; lastNode = null; chart = Read(owner, "m_activeFlowChart"); optionsKey = null;
            currentPassageIndex = -1;
            waitingForNextPassage = false;
            optionsPendingReveal = optionsRevealing = selectionCaptured = false; selectedReplyAnchor = -1;
            movingReplyIndex = -1; replyExtraOffset = 0; activeRevealDuration = RevealDuration;
            lastTranscriptSpeaker = null;
            hasLayout = revealing = false; revealOffset = 0;
            transcript.Clear(); transcriptNames.Clear();
            if (canvas == null)
            {
                var buildTime = System.Diagnostics.Stopwatch.StartNew(); Build();
                Debug.Log("[DeadfireDialog] First overlay construction: " + buildTime.ElapsedMilliseconds + "ms.");
            }
            ClearChildren(content); entries.Clear(); actions.Clear(); buttons.Clear(); choicesAvailable.Clear(); options = null;
            portrait.texture = null; portrait.enabled = false; portraitName.text = "";
            portraitFrameRect.gameObject.SetActive(false);
            canvas.gameObject.SetActive(true); dirty = follow = true;
            if (fallback) canvas.gameObject.SetActive(false); else StartRoll(false);
            Debug.Log("[DeadfireDialog] Conversation opened; vertical transcript enabled.");
        }

        public void BeforeNativeRebuild(Component owner)
        {
            if (active && owner == manager) Restore();
        }
        public void Capture(Component owner)
        {
            if (!active || owner != manager || (fallback && !nativePreview)) return;
            Component speaker = Read(owner, "SpeakerBox") as Component;
            if (speaker == null) return;
            object currentChart = Read(owner, "m_activeFlowChart");
            string key = Str(Read(currentChart, "CurrentNodeID"));
            if (chart != currentChart) { chart = currentChart; lastNode = null; }
            string raw = Str(Read(speaker, "Text"));
            Component displayedLabel = Read(speaker, "Label") as Component;
            GameObject withoutPortrait = Go(Read(speaker, "NoPortraitTextPanel"));
            if (withoutPortrait != null && withoutPortrait.activeSelf) displayedLabel = Read(speaker, "NoPortraitLabel") as Component;
            if (displayedLabel != null)
            {
                speakerTemplate = displayedLabel;
                string displayed = Str(Read(displayedLabel, "text"));
                if (!String.IsNullOrEmpty(displayed)) raw = displayed;
                raw = NativeInlineAssets.TagLinks(raw, displayedLabel);
            }
            if (!String.IsNullOrEmpty(raw) && (lastNode != key || lastRevision != revision))
            {
                string name = SpeakerName(Read(speaker, "SpeakerObject") as GameObject);
                Debug.Log("[DeadfireDialog] Captured native glossary links=" + System.Text.RegularExpressions.Regex.Matches(raw, "<link=", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count);
                string line = Formatting.Entry(name, raw, false, SpeakerColor(Read(speaker, "SpeakerObject") as GameObject));
                GameObject actor = Read(speaker, "SpeakerObject") as GameObject;
                Append(name, line, actor != null ? "actor:" + actor.GetInstanceID() : "narrator:" + name); currentPassageIndex = entries.Count - 1; lastNode = key; lastRevision = revision;
                waitingForNextPassage = false;
            }
            // Companions are interlocutors too. Only the player's own reply
            // should preserve the previous interlocutor portrait.
            GameObject speaking = Read(speaker, "SpeakerObject") as GameObject;
            if (speaking == null || speaking != PlayerCharacter())
            {
                Component texture = Read(speaker, "PortraitTexture") as Component;
                Texture source = speaking != null ? Read(texture, "mainTexture") as Texture : null;
                if (source != null)
                {
                    portrait.texture = source; portrait.enabled = true; portraitName.text = "";
                    object uv = Read(texture, "uvRect"); portrait.uvRect = uv is Rect ? (Rect)uv : new Rect(0, 0, 1, 1);
                    float ratio = source.width * Mathf.Abs(portrait.uvRect.width) / Mathf.Max(1, source.height * Mathf.Abs(portrait.uvRect.height));
                    if (ratio > .01f)
                    {
                        // Keep the chosen portrait width and frame padding; let
                        // its height follow the visible native texture's ratio.
                        float width = portraitFrameRect.sizeDelta.x;
                        portraitFrameRect.sizeDelta = new Vector2(width, (width - 24) / ratio + 24);
                        portraitAspect.aspectRatio = ratio;
                    }
                    SyncPortraitMask(texture);
                }
                else { portrait.texture = null; portrait.enabled = false; portraitName.text = ""; }
            }
            portraitFrameRect.gameObject.SetActive(portrait.enabled && portrait.texture != null);
            optionsKey = null; dirty = true;
            Debug.Log("[DeadfireDialog] Captured node " + key + "; transcript entries=" + transcript.Count);
        }

        private static string ResolveIconLabel(string link)
        {
            try
            {
                int separator = link.IndexOf("://", StringComparison.Ordinal);
                if (separator < 0) return null;
                Guid id = new Guid(link.Substring(separator + 3));
                Type resources = Type.GetType("Game.ResourceManager, Assembly-CSharp");
                MethodInfo lookup = resources == null ? null : resources.GetMethod("GetGameDataObject", Flags, null, new Type[] { typeof(Guid) }, null);
                object data = lookup == null ? null : lookup.Invoke(null, new object[] { id });
                Type gender = Type.GetType("Game.GameData.Gender, Assembly-CSharp");
                if (data == null || gender == null) return null;
                return Str(Call(data, "GetName", Enum.ToObject(gender, 0)));
            }
            catch { return null; }
        }
        private static string SpeakerName(GameObject obj)
        {
            if (obj == null) return "NARRATOR";
            Type utility = Type.GetType("Game.CharacterStatsUtility, Assembly-CSharp");
            MethodInfo method = utility == null ? null : utility.GetMethod("Name", Flags, null, new Type[] { typeof(GameObject) }, null);
            return Formatting.Plain(method == null ? obj.name : Str(method.Invoke(null, new object[] { obj })));
        }
        private static string SpeakerColor(GameObject obj)
        {
            if (obj == null) return null;
            Type utility = Type.GetType("Game.UI.ConversationUtilities, Assembly-CSharp");
            MethodInfo method = utility == null ? null : utility.GetMethod("GetConversationSpeakerColor", Flags, null, new Type[] { typeof(GameObject) }, null);
            if (method == null) return null;
            object result = method.Invoke(null, new object[] { obj });
            return result is Color ? "#" + ColorUtility.ToHtmlStringRGB((Color)result) : null;
        }
        private static bool IsParty(GameObject obj)
        {
            if (obj == null) return false;
            Type type = Type.GetType("Game.PartyManager, Assembly-CSharp");
            if (type == null) return false;
            object party = null;
            for (Type t = type; t != null; t = t.BaseType) { PropertyInfo p = t.GetProperty("Instance", Flags | BindingFlags.DeclaredOnly); if (p != null) { party = p.GetValue(null, null); break; } }
            return party != null && System.Object.Equals(Call(party, "IsActivePartyMember", obj), true);
        }

        private static GameObject PlayerCharacter()
        {
            Type state = Type.GetType("Game.GameState, Assembly-CSharp");
            FieldInfo playerField = state == null ? null : state.GetField("PlayerCharacter", Flags);
            return playerField == null ? null : Go(playerField.GetValue(null));
        }

        public void Reply(Component owner, string text)
        {
            if (!active || owner != manager) return;
            RetireOptions();
            string name = "YOU";
            GameObject player = PlayerCharacter();
            if (player != null) name = SpeakerName(player);
            if (!String.IsNullOrEmpty(text))
            {
                Append(name, Formatting.Entry(name, text, true, SpeakerColor(player)), player != null ? "actor:" + player.GetInstanceID() : "player");
                if (selectionCaptured)
                {
                    selectedReplyAnchor = entries.Count - 1;
                    RectTransform reply = entries[selectedReplyAnchor];
                    float width = Mathf.Max(200, scroll.viewport.rect.width) - 16;
                    Place(reply, 16, content.anchoredPosition.y - selectedChoiceY, width, Height(reply.GetComponent<NativeText>(), width));
                    selectionCaptured = false;
                }
            }
            selectionCaptured = false;
            revision++; optionsKey = null;
        }
        public void Advance(Component owner) { if (active && owner == manager) { selectionCaptured = false; RetireOptions(); revision++; optionsKey = null; } }
        private void RetireOptions()
        {
            // A selected option is now transcript text, not a live choice. Do
            // not measure the old list alongside the new YOU/NPC entries.
            if (options != null) { options.gameObject.SetActive(false); Destroy(options.gameObject); options = null; }
            buttons.Clear(); actions.Clear(); optionsKey = null;
            optionClip = optionRows = null; optionsPendingReveal = optionsRevealing = false;
            waitingForNextPassage = true; replyCommittedFrame = Time.frameCount;
            dirty = follow = true;
        }
        public void End(Component owner)
        {
            if (quitting) return;
            if (owner != manager) return;
            pendingEnd = true; endFrame = Time.frameCount + 2;
            Debug.Log("[DeadfireDialog] Native window hide requested; retaining transcript until conversation end.");
        }

        private void FinishEnd()
        {
            closing = false; rollDirection = 0;
            active = false; Restore(); if (canvas != null) canvas.gameObject.SetActive(false);
            manager = null; observedManager = null; pendingEnd = false; actions.Clear(); buttons.Clear();
            Debug.Log("[DeadfireDialog] Conversation ended; transcript released.");
        }
        public void FailOpen() { fallback = true; nativePreview = false; closing = false; rollDirection = 0; Restore(); if (canvas != null) canvas.gameObject.SetActive(false); }
        private void ToggleInterface()
        {
            DialoguePreferences.Enabled = fallback;
            if (revealing) ShiftTranscript(-revealOffset);
            CompleteReplyMove(); revealing = false; revealOffset = 0;
            selectionCaptured = false; selectedReplyAnchor = -1;
            if (!fallback)
            {
                fallback = nativePreview = true; rollDirection = 0;
                Restore(); canvas.gameObject.SetActive(false);
                RefreshNativeResponses();
                Debug.Log("[DeadfireDialog] F10: native interface; transcript capture continues.");
                return;
            }
            fallback = nativePreview = false; canvas.gameObject.SetActive(true);
            rollDirection = 0; ApplyRoll(1); rollInteraction.interactable = rollInteraction.blocksRaycasts = true;
            Capture(manager); waitingForNextPassage = false; optionsKey = null;
            dirty = follow = true; RebuildOptions(); Arrange();
            if (revealing) ShiftTranscript(-revealOffset);
            CompleteReplyMove(); revealing = false; revealOffset = 0;
            HideNative();
            Debug.Log("[DeadfireDialog] F10: overhaul interface restored with retained transcript.");
        }
        public void SetStyle(bool enabled)
        {
            if (active && !closing && enabled == fallback) ToggleInterface();
        }
        private void RefreshNativeResponses()
        {
            Component box = Read(manager, "ResponseBox") as Component;
            Component responses = Read(box, "Responses") as Component;
            if (responses == null) return;
            // Redraw only the current responses; never advance the flowchart.
            Call(responses, "DrawResponses");
            foreach (Component row in responses.GetComponentsInChildren<Component>(false))
            {
                if (row == null || row.GetType().Name != "UIConversationResponse") continue;
                Component label = Read(row, "Label") as Component;
                if (label == null || !label.gameObject.activeInHierarchy) continue;
                PropertyInfo alpha = label.GetType().GetProperty("alpha", Flags);
                if (alpha != null && alpha.CanWrite) alpha.SetValue(label, 1f, null);
                foreach (Renderer renderer in label.GetComponentsInChildren<Renderer>(false)) renderer.enabled = true;
            }
        }

        private void Build()
        {
            var root = new GameObject("Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1;
            rollClip = Rect("Conversation roll clip", root.transform); rollClip.gameObject.AddComponent<RectMask2D>();
            rollClip.anchorMin = rollClip.anchorMax = Vector2.zero; rollClip.pivot = Vector2.zero;
            rollContent = Rect("Conversation presentation", rollClip); rollContent.anchorMin = rollContent.anchorMax = Vector2.zero; rollContent.pivot = Vector2.zero;
            rollInteraction = rollContent.gameObject.AddComponent<CanvasGroup>();
            column = Rect("Right dialogue column", rollContent); column.anchorMin = new Vector2(1, 0); column.anchorMax = Vector2.one; column.pivot = new Vector2(1, .5f);
            column.sizeDelta = new Vector2(579, -72); column.anchoredPosition = new Vector2(-32, 0);
            Image background = column.gameObject.AddComponent<Image>(); background.color = new Color(.035f, .038f, .04f, .96f);
            Component nativeBackground = Read(manager, "Background") as Component;
            bool nativeBackgroundApplied = NativeSprites.Apply(background, nativeBackground);
            if (!nativeBackgroundApplied) { nativeBackground = Read(Read(manager, "SpeakerBox"), "Background") as Component; nativeBackgroundApplied = NativeSprites.Apply(background, nativeBackground); }
            RectTransform edge = Rect("Left rule", column); Stretch(edge); edge.anchorMax = new Vector2(0, 1); edge.sizeDelta = new Vector2(2, 0);
            edge.gameObject.AddComponent<Image>().color = new Color(.49f, .46f, .38f, .75f);
            if (nativeBackgroundApplied) edge.gameObject.SetActive(false);
            BuildHeaderOrnament();
            var portraitRect = Rect("Interlocutor portrait", column); portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0, 1); portraitRect.pivot = new Vector2(.5f, 1);
            portraitFrameRect = portraitRect;
            Vector2 portraitSize = new Vector2(142, 190) * 1.15f * .95f + new Vector2(12, 12);
            // Artwork, not only its frame, overlaps the column. The native
            // backing meshes share a seam and never blend over one another.
            portraitRect.anchoredPosition = new Vector2(-portraitSize.x * .5f + 40, -181); portraitRect.sizeDelta = portraitSize;
            Image portraitFrame = portraitRect.gameObject.AddComponent<Image>();
            portraitFrame.color = new Color(.035f, .038f, .04f, .96f); portraitFrame.raycastTarget = false;
            Outline frameRule = portraitRect.gameObject.AddComponent<Outline>();
            frameRule.effectColor = new Color(.49f, .46f, .38f, .75f); frameRule.effectDistance = new Vector2(1, -1); frameRule.useGraphicAlpha = false;
            if (nativeBackgroundApplied) { NativeSprites.Apply(portraitFrame, nativeBackground); frameRule.enabled = false; }
            if (nativeBackgroundApplied)
            {
                column.gameObject.AddComponent<JoinedBackdropMesh>().Init(column, portraitRect, false);
                portraitRect.gameObject.AddComponent<JoinedBackdropMesh>().Init(column, portraitRect, true);
            }
            RectTransform inset = Rect("Portrait frame inset", portraitRect); Stretch(inset); inset.offsetMin = new Vector2(12, 12); inset.offsetMax = new Vector2(-12, -12);
            RectTransform masked = Rect("Native portrait edge mask", inset); Stretch(masked);
            portraitMaskImage = masked.gameObject.AddComponent<Image>(); portraitMaskImage.raycastTarget = false; portraitMaskImage.enabled = false;
            portraitMask = masked.gameObject.AddComponent<Mask>(); portraitMask.showMaskGraphic = false; portraitMask.enabled = false;
            RectTransform picture = Rect("Portrait image", masked); Stretch(picture);
            portrait = picture.gameObject.AddComponent<RawImage>(); portrait.raycastTarget = false;
            portraitAspect = masked.gameObject.AddComponent<AspectRatioFitter>(); portraitAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; portraitAspect.aspectRatio = 90f / 141f;
            portraitName = Label("Interlocutor name", portraitRect, "", 14); RectTransform pn = portraitName.rectTransform; pn.anchorMin = pn.anchorMax = new Vector2(0, 0); pn.pivot = new Vector2(0, 1); pn.anchoredPosition = new Vector2(4, -10); pn.sizeDelta = new Vector2(165, 55);
            RectTransform area = Rect("Scroll area", column); Stretch(area); area.offsetMin = new Vector2(42, 100); area.offsetMax = new Vector2(-20, -100);
            scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32; scroll.inertia = true;
            RectTransform viewport = Rect("Viewport", area); Stretch(viewport); viewport.offsetMax = new Vector2(-34, 0); viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .005f); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            content = Rect("Chronological transcript", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content;
            RectTransform rail = Rect("Scrollbar", area); Stretch(rail); rail.anchorMin = new Vector2(1, 0); rail.pivot = new Vector2(1, .5f); rail.sizeDelta = new Vector2(5, 0);
            Image railImage = rail.gameObject.AddComponent<Image>(); railImage.color = new Color(.18f, .18f, .16f, .6f);
            Scrollbar bar = rail.gameObject.AddComponent<Scrollbar>(); RectTransform handle = Rect("Handle", rail); Stretch(handle); Image hg = handle.gameObject.AddComponent<Image>(); hg.color = new Color(.65f, .61f, .51f, .9f);
            bar.handleRect = handle; bar.targetGraphic = hg; bar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = bar;
            ApplyNativeScrollbar(bar, railImage, hg);
            scroll.onValueChanged.AddListener(delegate(Vector2 pos) { if (!follow) return; if (pos.y > .06f && Input.GetMouseButton(0)) follow = false; });
            if (EventSystem.current == null) { var es = new GameObject("Dialogue Event System", typeof(EventSystem), typeof(StandaloneInputModule)); es.transform.SetParent(transform, false); es.GetComponent<EventSystem>().sendNavigationEvents = false; }
        }

        private static RectTransform Rect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        private void ApplyNativeScrollbar(Scrollbar bar, Image track, Image handle)
        {
            // Current Deadfire conversations use UIPuckScrollbar, not the old
            // NGUI UIScrollBar references on UIDraggablePanel.
            foreach (string rootName in new string[] { "ConversationBox", "Window", "SpeakerBox", "ResponseBox", "HistoryPanel" })
            {
                GameObject root = Go(Read(manager, rootName)); if (root == null) continue;
                foreach (Component candidate in root.GetComponentsInChildren<Component>(true))
                {
                    if (candidate == null || candidate.GetType().Name != "UIPuckScrollbar") continue;
                    object vertical = Read(candidate, "IsVertical"); if (vertical is bool && !(bool)vertical) continue;
                    bar.enabled = false; scroll.verticalScrollbar = null; handle.gameObject.SetActive(false);
                    NativePuckScrollbar puck = track.gameObject.AddComponent<NativePuckScrollbar>();
                    puck.Init(scroll, candidate, delegate { if (revealing) ShiftTranscript(-revealOffset); CompleteReplyMove(); revealing = false; revealOffset = 0; follow = false; });
                    return;
                }
            }
            Component native = null;
            foreach (string box in new string[] { "SpeakerBox", "ResponseBox", "HistoryPanel" })
            {
                object owner = Read(manager, box);
                foreach (string field in new string[] { "DragPanel", "DragPanelNoPortrait" })
                {
                    native = Read(Read(owner, field), "verticalScrollBar") as Component;
                    if (native != null) break;
                }
                if (native != null) break;
            }
            if (native == null) { Debug.Log("[DeadfireDialog] Native scrollbar not found; retaining fallback."); return; }
            Component bg = Read(native, "background") as Component, fg = Read(native, "foreground") as Component;
            bool trackApplied = NativeSprites.Apply(track, bg), handleApplied = NativeSprites.Apply(handle, fg);
            if (trackApplied || handleApplied) { track.rectTransform.sizeDelta = new Vector2(16, 0); bar.transition = Selectable.Transition.None; }
            // Preserve any separate end ornaments in the native scrollbar tree.
            foreach (Component ornament in native.GetComponentsInChildren<Component>(true))
            {
                if (ornament == null || ornament == bg || ornament == fg || ornament.GetType().Name != "UISprite") continue;
                Sprite sprite = NativeSprites.Copy(ornament); if (sprite == null || bg == null) continue;
                Vector3 relative = bg.transform.InverseTransformPoint(ornament.transform.position);
                RectTransform rect = Rect("Native scrollbar " + ornament.name, track.transform);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, Mathf.Clamp01(relative.y + .5f)); rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(Mathf.Min(28, sprite.rect.width), Mathf.Min(40, sprite.rect.height));
                Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
            }
        }
        private void BuildHeaderOrnament()
        {
            Component historyButton = Read(manager, "HistoryButton") as Component;
            if (historyButton == null) return;
            Component[] candidates = historyButton.GetComponentsInChildren<Component>(true);
            foreach (string name in new string[] { "conversation_drag_nugget_bg", "conversation_drag_nugget", "conversation_drag_nugget_inner" })
                foreach (Component candidate in candidates)
                {
                    if (candidate == null || candidate.GetType().Name != "UISprite" || Str(Read(candidate, "spriteName")) != name) continue;
                    Sprite sprite = NativeSprites.Copy(candidate); if (sprite == null) continue;
                    RectTransform rect = Rect("Native header " + name, column);
                    Vector3 nativePosition = candidate.transform.localPosition;
                    Vector3 nativeScale = candidate.transform.localScale;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
                    rect.anchoredPosition = new Vector2(nativePosition.x, 12 + nativePosition.y);
                    rect.localRotation = candidate.transform.localRotation;
                    bool line = name == "conversation_drag_nugget_bg";
                    rect.sizeDelta = new Vector2(line ? column.sizeDelta.x - 16 : Mathf.Abs(nativeScale.x), Mathf.Abs(nativeScale.y));
                    Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Simple; image.raycastTarget = false;
                    // The gold/brown shading is already in the atlas artwork.
                    // Native inactive-button tint must not darken it a second time.
                    image.color = Color.white;
                    Debug.Log("[DeadfireDialog] Reused header ornament " + name);
                    break;
                }
        }
        private void SyncPortraitMask(Component original)
        {
            Texture mask = null;
            for (Transform parent = original.transform; parent != null && mask == null; parent = parent.parent)
            {
                Component panel = parent.GetComponent("UIPanel");
                if (panel != null) mask = Read(panel, "clipMask") as Texture;
            }
            if (mask == null)
            {
                Material material = Read(original, "material") as Material;
                if (material != null)
                    foreach (string property in new string[] { "_MaskTex", "_Mask", "_MaskTexture", "_AlphaTex" })
                        if (material.HasProperty(property)) { mask = material.GetTexture(property); if (mask != null) break; }
            }
            Texture source = Read(original, "mainTexture") as Texture;
            if (source == null) return;
            if (mask == nativePortraitMask && source == nativePortraitSource && maskedPortraitTexture != null) { portrait.texture = maskedPortraitTexture; return; }
            nativePortraitMask = mask;
            nativePortraitSource = source;
            // Native NGUI stores masks separately from portrait PNGs. Transfer
            // that same mask into a uGUI stencil, without editing game assets.
            RenderTexture previous = RenderTexture.active;
            RenderTexture buffer = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Texture2D alpha = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            Texture2D image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(mask != null ? mask : Texture2D.whiteTexture, buffer); RenderTexture.active = buffer;
                alpha.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); alpha.Apply();
                Graphics.Blit(source, buffer); RenderTexture.active = buffer;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(buffer); }
            Color32[] pixels = alpha.GetPixels32(); bool usesAlpha = false;
            foreach (Color32 pixel in pixels) if (pixel.a < 250) { usesAlpha = true; break; }
            Color32[] imagePixels = image.GetPixels32();
            PortraitFilter.Sharpen(imagePixels, source.width, source.height);
            for (int i = 0; i < pixels.Length; i++) imagePixels[i].a = (byte)(imagePixels[i].a * (usesAlpha ? pixels[i].a : pixels[i].r) / 255);
            image.SetPixels32(imagePixels); image.filterMode = FilterMode.Bilinear; image.Apply();
            if (maskedPortraitTexture != null) Destroy(maskedPortraitTexture);
            maskedPortraitTexture = image; portrait.texture = image; Destroy(alpha);
            // Apply the native alpha directly rather than relying on the old
            // game's UI stencil shader to discard transparent mask fragments.
            portraitMaskImage.enabled = false; portraitMask.enabled = false;
            Debug.Log("[DeadfireDialog] Portrait sharpened; native edge mask: " + (mask != null ? mask.name : "none"));
        }
        private NativeText Label(string name, Transform parent, string value, int size)
        {
            RectTransform rt = Rect(name, parent); NativeText text = rt.gameObject.AddComponent<NativeText>();
            text.Init(name == "Response text" && responseTemplate != null ? responseTemplate : speakerTemplate, size);
            if (scroll != null && scroll.content != null && rt.IsChildOf(scroll.content)) rt.gameObject.AddComponent<TranscriptFade>().Viewport = scroll.viewport;
            text.text = value; return text;
        }
        private void Append(string name, string line, string speakerKey)
        {
            bool continuation = lastTranscriptSpeaker == speakerKey;
            if (continuation) line = Formatting.Continuation(line);
            lastTranscriptSpeaker = speakerKey;
            // Only completed transcript entries are muted. Current options are
            // separate objects and retain their own read/hover palette.
            if (entries.Count > 0 && entries[entries.Count - 1] != null)
            {
                GameObject previous = entries[entries.Count - 1].gameObject;
                NativeText historyText = previous.GetComponent<NativeText>();
                if (historyText != null) historyText.text = Formatting.History(transcript[entries.Count - 1]);
                CanvasGroup history = previous.GetComponent<CanvasGroup>();
                if (history != null) history.alpha = 1;
            }
            transcript.Add(line); transcriptNames.Add(name);
            NativeText label = Label("Transcript " + transcript.Count, content, line, 22); label.Hanging = !continuation; label.text = label.text; entries.Add(label.rectTransform);
            if (options != null) options.SetAsLastSibling(); dirty = follow = true;
        }
        private static void ClearChildren(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) { Transform child = parent.GetChild(i); child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); } }

        private void RebuildOptions()
        {
            Component responseBox = Read(manager, "ResponseBox") as Component;
            Component responses = Read(responseBox, "Responses") as Component;
            if (responses == null) return;
            var native = new List<Component>(); string key = "";
            foreach (Component c in responses.GetComponentsInChildren<Component>(false))
                if (c != null && c.GetType().Name == "UIConversationResponse")
                {
                    Component label = Read(c, "Label") as Component; string raw = Str(Read(label, "text"));
                    if (String.IsNullOrEmpty(raw)) continue; native.Add(c); key += c.GetInstanceID() + ":" + raw + ":" + ChoiceAvailable(c) + ":" + ChoiceDisabledColor(c) + "\n";
                }
            native.Sort(delegate(Component a, Component b) { return Convert.ToInt32(Read(a, "Index")).CompareTo(Convert.ToInt32(Read(b, "Index"))); });
            Component cont = Read(responses, "ContinueButton") as Component;
            bool showContinue = cont != null && cont.gameObject.activeInHierarchy;
            key += showContinue + ":" + revision;
            if (optionsKey == key) return;
            optionsKey = key;
            if (options != null) { options.gameObject.SetActive(false); Destroy(options.gameObject); }
            options = Rect("Current responses", content); options.anchorMin = new Vector2(0, 1); options.anchorMax = Vector2.one; options.pivot = new Vector2(.5f, 1);
            optionClip = Rect("Response reveal clip", options); optionClip.gameObject.AddComponent<RectMask2D>();
            optionRows = Rect("Response rows", optionClip);
            optionInteraction = optionRows.gameObject.AddComponent<CanvasGroup>(); optionInteraction.interactable = false; optionInteraction.blocksRaycasts = false;
            optionsPendingReveal = true; optionsRevealing = false; optionRevealTime = 0;
            Place(optionClip, 0, 0, Mathf.Max(200, scroll.viewport.rect.width), 0);
            buttons.Clear(); actions.Clear(); choicesAvailable.Clear();
            foreach (Component c in native)
            {
                Component captured = c;
                responseTemplate = Read(c, "Label") as Component;
                string text = Str(Read(Read(c, "Label"), "text"));
                bool read = false; object node = Read(c, "Node"); if (node != null) read = System.Object.Equals(Call(c, "MarkedAsRead", node), true);
                bool available = ChoiceAvailable(c);
                AddChoice(Formatting.Option(text, read, available, ChoiceDisabledColor(c)), delegate { if (captured != null && captured.gameObject.activeInHierarchy && ChoiceAvailable(captured)) Call(captured, "SelectResponse"); }, available);
            }
            if (showContinue && native.Count == 0)
            {
                string text = Str(Read(Read(cont, "TextLabel"), "text"));
                AddChoice(String.IsNullOrEmpty(text) ? "CONTINUE" : text.ToUpperInvariant(), delegate { Call(responses, "OnContinueButton", cont.gameObject); });
            }
            options.SetAsLastSibling(); dirty = true; follow = true;
        }
        private static bool ChoiceAvailable(Component response)
        {
            Behaviour nativeButton = Read(response, "m_imageButton") as Behaviour;
            return nativeButton != null && nativeButton.enabled;
        }
        private static string ChoiceDisabledColor(Component response)
        {
            object color = Read(Read(response, "m_imageButton"), "OverrideDisabledColor");
            return color is Color ? "#" + ColorUtility.ToHtmlStringRGB((Color)color) : null;
        }
        private void AddChoice(string value, Action action, bool available = true)
        {
            RectTransform row = Rect("Response", optionRows); Image image = row.gameObject.AddComponent<Image>(); image.color = new Color(1, 1, 1, .015f);
            image.color = Color.clear;
            Button button = row.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None; button.targetGraphic = image;
            button.interactable = available;
            NativeText label = Label("Response text", row, value, 22); Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(16, 0); label.Hanging = true; label.text = label.text;
            label.color = Color.white;
            label.GetComponent<InlineTextVisuals>().UseLinkColors = available;
            ChoiceHover hover = row.gameObject.AddComponent<ChoiceHover>(); hover.Label = label; hover.enabled = available;
            int index = actions.Count; actions.Add(action); buttons.Add(button); choicesAvailable.Add(available);
            button.onClick.AddListener(delegate { Queue(index); });
        }
        private void Queue(int index)
        {
            if (busy || index < 0 || index >= actions.Count || !Ready()) return;
            if (index >= buttons.Count || buttons[index] == null || !buttons[index].interactable) return;
            if (optionsPendingReveal || optionsRevealing) return;
            if (index < buttons.Count && buttons[index] != null)
            {
                NativeText label = buttons[index].GetComponentInChildren<NativeText>();
                Vector3[] corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                selectedChoiceY = scroll.viewport.InverseTransformPoint(corners[1]).y - scroll.viewport.rect.yMax;
                selectionCaptured = true; revealing = false;
                for (int i = 0; i < buttons.Count; i++) if (i != index && buttons[i] != null) buttons[i].gameObject.SetActive(false);
                Debug.Log("[DeadfireDialog] Choice selected; remaining choices hidden.");
            }
            StartCoroutine(InvokeChoice(actions[index]));
        }
        private IEnumerator InvokeChoice(Action action)
        {
            busy = true; foreach (Button b in buttons) if (b != null) b.interactable = false;
            yield return null;
            try { if (active && manager != null) action(); } catch (Exception ex) { Debug.LogError("[DeadfireDialog] Choice: " + ex); FailOpen(); }
            busy = false; optionsKey = null;
            if (selectionCaptured && !waitingForNextPassage)
            {
                selectionCaptured = false;
                foreach (Button button in buttons) if (button != null) button.gameObject.SetActive(true);
            }
        }
        private bool Ready() { object ready = Read(manager, "AreResultsComplete"); return !(ready is bool) || (bool)ready; }

        private void Arrange()
        {
            // Preserve an existing line's current on-screen position before
            // measuring the expanded transcript, including an in-flight reveal.
            // Preserve old history independently of the clicked row. Anchoring
            // the entire transcript to that row caused history to jump downward
            // when the response-list gap collapsed into paragraph spacing.
            RectTransform anchor = hasLayout && entries.Count > 0 ? entries[0] : null;
            float oldY = anchor == null ? 0 : anchor.anchoredPosition.y + content.anchoredPosition.y;
            bool newSelection = selectedReplyAnchor >= 0 && selectedReplyAnchor < entries.Count;
            int replyIndex = newSelection ? selectedReplyAnchor : movingReplyIndex;
            float oldReplyY = newSelection ? selectedChoiceY : (replyIndex >= 0 && replyIndex < entries.Count ? entries[replyIndex].anchoredPosition.y + content.anchoredPosition.y : 0);
            selectedReplyAnchor = -1;
            float width = Mathf.Max(200, scroll.viewport.rect.width); float y = 24;
            foreach (RectTransform entry in entries)
            {
                if (entry == null) continue; NativeText text = entry.GetComponent<NativeText>(); float h = Height(text, width - 16);
                Place(entry, 16, y, width - 16, h); y += h + 18;
            }
            // Reserve this gap even in the intermediate frame with no live
            // options, so the passage never drops down then moves up again.
            float firstChoiceTop = y + TranscriptLayout.ResponseTopInset;
            if (options != null && buttons.Count > 0)
            {
                // Last transcript entry already contributes 18px: 18+30=48px,
                // twice the previous 18+6=24px passage-to-responses gap.
                float oy = TranscriptLayout.ResponseTopInset;
                firstChoiceTop = y + oy;
                foreach (Button button in buttons)
                {
                    if (button == null) continue; NativeText text = button.GetComponentInChildren<NativeText>(); float h = Mathf.Max(34, Height(text, width - 16) + 10);
                    Place(button.GetComponent<RectTransform>(), 0, oy, width, h); oy += h + 3;
                }
                Place(options, 0, y, width, oy); y += oy;
                if (optionRows != null) Place(optionRows, 0, 0, width, oy);
                if (optionClip != null) Place(optionClip, 0, 0, width, optionsPendingReveal || optionsRevealing ? optionClip.rect.height : oy);
            }
            else y = firstChoiceTop;
            // Choice position, not NPC paragraph height, determines the anchor.
            // The 48px dialogue/choice gap stays above the first choice and does
            // not reduce the room reserved below the response list.
            float naturalHeight = TranscriptLayout.ContentHeight(scroll.viewport.rect.height, firstChoiceTop - TranscriptLayout.ResponseTopInset, y - firstChoiceTop);
            float topSpace = Mathf.Max(0, scroll.viewport.rect.height - naturalHeight);
            if (topSpace > 0)
            {
                foreach (RectTransform entry in entries) if (entry != null) entry.anchoredPosition -= new Vector2(0, topSpace);
                if (options != null) options.anchoredPosition -= new Vector2(0, topSpace);
            }
            content.sizeDelta = new Vector2(0, Mathf.Max(scroll.viewport.rect.height, naturalHeight)); dirty = false; lastHeight = Screen.height;
            // Establish final scroll bounds immediately, then reveal that final
            // layout visually. This covers both bottom-padding changes in short
            // conversations and actual scrolling once the transcript overflows.
            revealOffset = 0; revealStart = 0; revealing = false; activeRevealDuration = RevealDuration;
            if (follow)
            {
                scroll.StopMovement(); scroll.verticalNormalizedPosition = 0;
                if (anchor != null)
                {
                    revealStart = oldY - (anchor.anchoredPosition.y + content.anchoredPosition.y);
                    if (Mathf.Abs(revealStart) > .5f)
                    {
                        ShiftTranscript(revealStart); revealOffset = revealStart;
                        revealTime = 0; revealing = true;
                    }
                }
                if (replyIndex >= 0 && replyIndex < entries.Count)
                {
                    movingReplyIndex = replyIndex;
                    RectTransform reply = entries[replyIndex];
                    replyExtraStart = oldReplyY - (reply.anchoredPosition.y + content.anchoredPosition.y);
                    replyExtraOffset = replyExtraStart; reply.anchoredPosition += new Vector2(0, replyExtraStart);
                    activeRevealDuration = SelectedReplyDuration; revealTime = 0; revealing = true;
                    for (int i = replyIndex + 1; i < entries.Count; i++) if (entries[i] != null) entries[i].gameObject.SetActive(false);
                    if (newSelection) Debug.Log("[DeadfireDialog] Selected reply movement: " + replyExtraStart + "px; duration=" + SelectedReplyDuration);
                }
            }
            hasLayout = true;
        }
        private void ShiftTranscript(float delta)
        {
            Vector2 shift = new Vector2(0, delta);
            foreach (RectTransform entry in entries) if (entry != null) entry.anchoredPosition += shift;
            if (options != null) options.anchoredPosition += shift;
        }
        private void AnimateTranscript()
        {
            if (!revealing) return;
            // Unscaled time is required because dialogue pauses the world.
            revealTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(revealTime / activeRevealDuration);
            float next = Mathf.Lerp(revealStart, 0, Mathf.SmoothStep(0, 1, t));
            ShiftTranscript(next - revealOffset); revealOffset = next;
            if (movingReplyIndex >= 0 && movingReplyIndex < entries.Count)
            {
                float extra = Mathf.Lerp(replyExtraStart, 0, Mathf.SmoothStep(0, 1, t));
                entries[movingReplyIndex].anchoredPosition += new Vector2(0, extra - replyExtraOffset); replyExtraOffset = extra;
            }
            if (t >= 1) { revealing = false; revealOffset = 0; follow = false; CompleteReplyMove(); }
        }
        private void CompleteReplyMove()
        {
            if (movingReplyIndex >= 0 && movingReplyIndex < entries.Count)
            {
                entries[movingReplyIndex].anchoredPosition -= new Vector2(0, replyExtraOffset);
                for (int i = movingReplyIndex + 1; i < entries.Count; i++) if (entries[i] != null) entries[i].gameObject.SetActive(true);
            }
            movingReplyIndex = -1; replyExtraOffset = 0;
        }
        private void RevealOptions()
        {
            if (optionClip == null || optionRows == null) return;
            if (optionsPendingReveal)
            {
                if (revealing || dirty || waitingForNextPassage || rollDirection != 0) return;
                optionsPendingReveal = false; optionsRevealing = true; optionRevealTime = 0;
                Debug.Log("[DeadfireDialog] Text settled; revealing " + buttons.Count + " choices top-to-bottom.");
            }
            if (!optionsRevealing) return;
            optionRevealTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(optionRevealTime / OptionRevealDuration);
            optionClip.sizeDelta = new Vector2(optionRows.rect.width, optionRows.rect.height * Mathf.SmoothStep(0, 1, t));
            if (t >= 1)
            {
                optionsRevealing = false;
                optionInteraction.interactable = true; optionInteraction.blocksRaycasts = true;
            }
        }
        private void StartRoll(bool close)
        {
            closing = close; rollDirection = close ? -1 : 1; rollTime = 0;
            canvas.gameObject.SetActive(true); rollInteraction.interactable = false; rollInteraction.blocksRaycasts = false;
            Canvas.ForceUpdateCanvases(); ApplyRoll(0);
        }
        private void ApplyRoll(float progress)
        {
            RectTransform root = (RectTransform)canvas.transform;
            float width = root.rect.width, height = root.rect.height;
            float eased = Mathf.SmoothStep(0, 1, progress);
            // Opening: top edge climbs from bottom. Closing: bottom edge climbs
            // to the top. Keep the content fixed so nothing stretches or reflows.
            float bottom = rollDirection < 0 ? height * eased : 0;
            float top = rollDirection > 0 ? height * eased : height;
            rollClip.anchoredPosition = new Vector2(0, bottom); rollClip.sizeDelta = new Vector2(width, Mathf.Max(0, top - bottom));
            rollContent.anchoredPosition = new Vector2(0, -bottom); rollContent.sizeDelta = new Vector2(width, height);
        }
        private bool TickRoll()
        {
            if (rollDirection == 0) { ApplyRoll(1); return false; }
            rollTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(rollTime / RollDuration); ApplyRoll(progress);
            if (progress >= 1)
            {
                if (closing) { FinishEnd(); return true; }
                rollDirection = 0; rollInteraction.interactable = true; rollInteraction.blocksRaycasts = true;
            }
            return closing;
        }
        private static float Height(NativeText text, float width)
        {
            return text.Height(width);
        }
        private static void Place(RectTransform rt, float x, float y, float width, float height)
        { rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height); }

        private void LateUpdate()
        {
            if (!active) return;
            try
            {
                if (OtherInterfaceOpen())
                {
                    if (closing || (pendingEnd && NativeConversation() == null)) FinishEnd();
                    else if (canvas != null) canvas.gameObject.SetActive(false);
                    return;
                }
                if (closing) { TickRoll(); return; }
                if (pendingEnd && Time.frameCount >= endFrame)
                {
                    object native = NativeConversation();
                    if (native == null) { if (fallback) FinishEnd(); else StartRoll(true); return; }
                    if (System.Object.Equals(Call(manager, "WindowActive"), true)) pendingEnd = false;
                }
                bool windowVisible = manager != null && manager.gameObject.activeInHierarchy && !System.Object.Equals(Call(manager, "WindowActive"), false);
                if (windowVisible && !busy && Input.GetKeyDown(KeyCode.F10)) { ToggleInterface(); return; }
                if (fallback) return;
                TickRoll();
                if (manager == null || !manager.gameObject.activeInHierarchy || System.Object.Equals(Call(manager, "WindowActive"), false)) { if (!pendingEnd && canvas != null) canvas.gameObject.SetActive(false); return; }
                canvas.gameObject.SetActive(true);
                if (!waitingForNextPassage) RebuildOptions();
                if ((dirty || lastHeight != Screen.height) && (!waitingForNextPassage || Time.frameCount >= replyCommittedFrame + 2)) Arrange();
                bool manualScroll = Math.Abs(Input.GetAxis("Mouse ScrollWheel")) > .001f || (Input.GetMouseButton(0) && !follow);
                if (manualScroll)
                {
                    if (revealing) ShiftTranscript(-revealOffset);
                    CompleteReplyMove();
                    revealing = false; revealOffset = 0; follow = false;
                }
                AnimateTranscript();
                RevealOptions();
                for (int i = 0; i < buttons.Count; i++) if (buttons[i] != null) buttons[i].interactable = choicesAvailable[i] && !busy && Ready();
                HideNative();
                if (!frameLogged) { frameLogged = true; Debug.Log("[DeadfireDialog] Overlay ready: column=" + column.rect.width + "x" + column.rect.height + "; choices=" + buttons.Count); }
            }
            catch (Exception ex) { Debug.LogError("[DeadfireDialog] Update: " + ex); FailOpen(); }
        }

        private static object NativeConversation()
        {
            Type type = Type.GetType("Game.ConversationManager, Assembly-CSharp");
            for (Type t = type; t != null; t = t.BaseType)
            {
                PropertyInfo instance = t.GetProperty("Instance", Flags | BindingFlags.DeclaredOnly);
                if (instance != null) return Call(instance.GetValue(null, null), "GetActiveConversationForHUD");
            }
            return null;
        }
        private bool OtherInterfaceOpen()
        {
            Type type = Type.GetType("Game.UI.UIWindowManager, Assembly-CSharp");
            object windows = null;
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo instance = current.GetProperty("Instance", Flags | BindingFlags.DeclaredOnly);
                if (instance != null) { windows = instance.GetValue(null, null); break; }
            }
            IEnumerable list = Read(windows, "m_Windows") as IEnumerable;
            if (list == null) return false;
            foreach (object candidate in list)
            {
                Component window = candidate as Component;
                if (window == null || window == manager || !window.gameObject.activeInHierarchy) continue;
                if (!System.Object.Equals(Read(window, "EatsKeyInput"), true) && !System.Object.Equals(Read(window, "HidesHud"), true) && !System.Object.Equals(Read(window, "InReplaceGroup"), true)) continue;
                if (System.Object.Equals(Call(window, "WindowActive"), true)) return true;
            }
            return false;
        }

        private void HideNative()
        {
            // Only touch concrete dialogue surfaces. ConversationBox can be a
            // broader shared hierarchy; traversing it risks unrelated HUD state.
            foreach (string field in new string[] { "SpeakerBox", "ResponseBox", "Background", "HistoryPanel", "HistoryButton" })
            {
                GameObject root = Go(Read(manager, field)); if (root == null) continue;
                foreach (Component c in root.GetComponentsInChildren<Component>(false))
                {
                    if (c == null) continue;
                    PropertyInfo alpha = c.GetType().GetProperty("alpha", Flags);
                    if (alpha != null && alpha.PropertyType == typeof(float) && alpha.CanRead && alpha.CanWrite)
                    { if (!hiddenWidgets.ContainsKey(c)) hiddenWidgets[c] = (float)alpha.GetValue(c, null); alpha.SetValue(c, 0f, null); }
                    Renderer renderer = c as Renderer;
                    // TMP world labels are not NGUI widgets. Hide only their renderers.
                    if (renderer != null && c.gameObject.GetComponent("TextMeshPro") != null)
                    { if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers[renderer] = renderer.enabled; renderer.enabled = false; }
                    Collider collider = c as Collider; if (collider != null) { if (!hiddenColliders.ContainsKey(collider)) hiddenColliders[collider] = collider.enabled; collider.enabled = false; }
                    Collider2D collider2 = c as Collider2D; if (collider2 != null) { if (!hiddenColliders2D.ContainsKey(collider2)) hiddenColliders2D[collider2] = collider2.enabled; collider2.enabled = false; }
                }
            }
        }
        private readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        private void Restore()
        {
            foreach (KeyValuePair<Component, float> kv in hiddenWidgets) if (kv.Key != null) kv.Key.GetType().GetProperty("alpha", Flags).SetValue(kv.Key, kv.Value, null);
            foreach (KeyValuePair<Renderer, bool> kv in hiddenRenderers) if (kv.Key != null) kv.Key.enabled = kv.Value;
            foreach (KeyValuePair<Collider, bool> kv in hiddenColliders) if (kv.Key != null) kv.Key.enabled = kv.Value;
            foreach (KeyValuePair<Collider2D, bool> kv in hiddenColliders2D) if (kv.Key != null) kv.Key.enabled = kv.Value;
            hiddenWidgets.Clear(); hiddenRenderers.Clear(); hiddenColliders.Clear(); hiddenColliders2D.Clear();
        }
        private void OnApplicationQuit()
        {
            // Unity 5 tears down native NGUI objects during shutdown. Do not
            // invoke their reflected setters from our later OnDestroy callback.
            quitting = true; active = false;
        }
        private void OnDestroy() { if (!quitting) Restore(); }
    }

    public sealed class ChoiceHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public NativeText Label;
        private Color original;
        private string originalText;
        private bool hovering;
        public void OnPointerEnter(PointerEventData data)
        {
            if (Label == null || hovering) return;
            Button button = GetComponent<Button>();
            if (button != null && !button.interactable) return;
            original = Label.color; originalText = Label.text; hovering = true;
            Label.text = Formatting.Hover(originalText); Label.color = Color.white;
        }
        public void OnPointerExit(PointerEventData data)
        {
            if (Label != null && hovering) { Label.text = originalText; Label.color = original; }
            hovering = false;
        }
    }
}


