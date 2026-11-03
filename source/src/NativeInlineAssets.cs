using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Deadfire.DialogOverhaul
{
    internal static class NativeInlineAssets
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static string TagLinks(string text, Component source)
        {
            try
            {
                Component glossary = GlossaryComponent(source);
                object categories = DialogueView.Read(glossary, "AddTagsForCategories");
                Type parameters = Type.GetType("Game.GlossaryInsertParams, Assembly-CSharp");
                Type flags = Type.GetType("Game.GlossaryReplaceFlags, Assembly-CSharp");
                Type glossaryType = Type.GetType("Game.Glossary, Assembly-CSharp");
                object settings;
                // Zero means the label adds no *extra* categories. The base
                // conversation's lore pass must still run for captured text.
                if (categories == null || Convert.ToInt32(categories) == 0) settings = parameters.GetField("ForLore", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
                else
                {
                    MethodInfo create = parameters.GetMethod("ForCategories", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    bool firstOnly = System.Object.Equals(DialogueView.Read(glossary, "FirstOccurrenceOnly"), true);
                    settings = create.Invoke(null, new object[] { categories, Enum.ToObject(flags, firstOnly ? 2 : 0) });
                }
                object ignored = DialogueView.Read(DialogueView.Read(glossary, "IgnoreTerm"), "Data");
                FieldInfo ignoredField = parameters.GetField("Ignored"); if (ignoredField != null && ignored != null) ignoredField.SetValue(settings, ignored);
                MethodInfo tag = glossaryType.GetMethod("AddUrlTags", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), parameters }, null);
                return (string)tag.Invoke(null, new object[] { text, settings });
            }
            catch (Exception ex) { Debug.LogWarning("[DeadfireDialog] Glossary insertion: " + ex.Message); return text; }
        }
        private static Component GlossaryComponent(Component source)
        {
            if (source != null) foreach (Component component in source.GetComponents<Component>()) if (component != null && component.GetType().Name == "UIGlossaryEnabledLabel") return component;
            return null;
        }
        public static Sprite Icon(string name, Component source)
        {
            Sprite cached; if (sprites.TryGetValue(name, out cached) && cached != null) return cached;
            Type type = Type.GetType("TMPro.TMP_SpriteAsset, Assembly-CSharp");
            if (type == null) return null;
            var assets = new List<UnityEngine.Object>();
            UnityEngine.Object direct = DialogueView.Read(source, "spriteAsset") as UnityEngine.Object;
            if (direct != null) assets.Add(direct);
            assets.AddRange(Resources.FindObjectsOfTypeAll(type));
            foreach (UnityEngine.Object asset in assets)
            {
                IEnumerable entries = DialogueView.Read(asset, "spriteInfoList") as IEnumerable;
                if (entries == null) continue;
                foreach (object entry in entries)
                {
                    if (!String.Equals(Convert.ToString(DialogueView.Read(entry, "name")), name, StringComparison.OrdinalIgnoreCase)) continue;
                    Sprite sprite = DialogueView.Read(entry, "sprite") as Sprite;
                    if (sprite == null)
                    {
                        Texture2D texture = DialogueView.Read(asset, "spriteSheet") as Texture2D;
                        float x = Number(entry, "x"), y = Number(entry, "y"), w = Number(entry, "width"), h = Number(entry, "height");
                        if (texture != null && w > 0 && h > 0) sprite = Sprite.Create(texture, new Rect(x, y, w, h), new Vector2(.5f, .5f));
                    }
                    if (sprite != null) { sprites[name] = sprite; return sprite; }
                }
            }
            Debug.LogWarning("[DeadfireDialog] Inline icon not loaded: " + name);
            return null;
        }
        private static float Number(object obj, string field) { object v = DialogueView.Read(obj, field); return v == null ? 0 : Convert.ToSingle(v); }
        public static Color LinkColor(Component source, string url, bool hovered)
        {
            try
            {
                Component glossary = GlossaryComponent(source);
                object colors = DialogueView.Read(glossary, "LinkColors");
                if (colors != null)
                {
                    MethodInfo get = colors.GetType().GetMethod("Get");
                    int category = url.StartsWith("buff", StringComparison.OrdinalIgnoreCase) ? 2 : url.StartsWith("debuff", StringComparison.OrdinalIgnoreCase) ? 3 : 1;
                    return (Color)get.Invoke(colors, new object[] { hovered, Enum.ToObject(get.GetParameters()[1].ParameterType, category) });
                }
            }
            catch (Exception ex) { Debug.LogWarning("[DeadfireDialog] Native link colour: " + ex.Message); }
            return hovered ? new Color(.95f, .75f, .45f) : new Color(.65f, .8f, .8f);
        }
        public static string Tooltip(string url, out string title)
        {
            title = "";
            try
            {
                Type type = Type.GetType("Game.UI.UIHyperlinkManager, Assembly-CSharp");
                MethodInfo get = type == null ? null : type.GetMethod("GetTooltipContent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
                object content = get == null ? null : get.Invoke(null, new object[] { url });
                if (content == null) return null;
                if (content is string) return (string)content;
                Type names = Type.GetType("IDisplayNameUtilities, Assembly-CSharp");
                if (names != null)
                    foreach (MethodInfo method in names.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        if (method.Name == "GetName" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType.IsInstanceOfType(content)) { title = Convert.ToString(method.Invoke(null, new object[] { content })); break; }
                Type gameState = Type.GetType("Game.GameState, Assembly-CSharp");
                FieldInfo playerField = gameState == null ? null : gameState.GetField("PlayerCharacter", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Component player = playerField == null ? null : playerField.GetValue(null) as Component;
                return Convert.ToString(MethodDispatch.Invoke(content, "GetTooltipContent", player == null ? null : player.gameObject, false));
            }
            catch (Exception ex) { Debug.LogWarning("[DeadfireDialog] Inline tooltip: " + ex.Message); return null; }
        }
    }
}
