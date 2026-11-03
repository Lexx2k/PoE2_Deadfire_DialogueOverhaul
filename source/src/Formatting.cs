using System;
using System.Text.RegularExpressions;

namespace Deadfire.DialogOverhaul
{
    public static class Formatting
    {
        private const string PlayerNameColor = "#8C8068";
        public static Func<string, string> IconLinkLabel;
        // Keep the game's visible wording, emphasis and speech/narration colours.
        // Keep inline metadata for the renderer's icon/link adapter.
        public static string Clean(string text)
        {
            text = text ?? String.Empty;
            // Deadfire's TextTagging.CreateColorTag emits TMP shorthand.
            text = Regex.Replace(text, @"<#([0-9a-f]{6}(?:[0-9a-f]{2})?)>", "<color=#$1>", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\[url[^\]]*\]|\[/url\]", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]*>", delegate(Match m) {
                string tag = m.Value;
                return Regex.IsMatch(tag, @"^</?(b|i)>$|^</color>$|^<color=#[0-9a-f]{6}([0-9a-f]{2})?>$|^<link=""[^""]+"">$|^</link>$|^<sprite[^>]*\bname=""[^""]+""[^>]*>$", RegexOptions.IgnoreCase) ? tag : "";
            });
            return text;
        }

        public static string SpriteLabel(string text)
        {
            Match name = Regex.Match(text, @"\bname=""([^""]+)""", RegexOptions.IgnoreCase);
            if (!name.Success) return "";
            string path = name.Groups[1].Value.Replace('\\', '/');
            string label = path.Substring(path.LastIndexOf('/') + 1);
            label = Regex.Replace(label, @"\.[a-z0-9]+$", "", RegexOptions.IgnoreCase).Replace('_', ' ');
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);
        }

        public static string Plain(string text) { return Regex.Replace(Clean(text), "<[^>]+>", "").Trim(); }

        public static string Hover(string text)
        {
            // An embedded read-state colour overrides a Graphic's tint. Replace
            // those colours temporarily while keeping emphasis and visible text.
            return "<color=#F3983D>" + Regex.Replace(Clean(text), @"</?color(?:=[^>]+)?>", "", RegexOptions.IgnoreCase) + "</color>";
        }

        public static string History(string text)
        {
            // CanvasGroup opacity is not reliable with every shader in this
            // Unity build. Muting the actual rich-text colours is deterministic.
            string muted = Regex.Replace(Clean(text), @"<color=#([0-9a-f]{6})([0-9a-f]{2})?>", delegate(Match m) {
                string hex = m.Groups[1].Value;
                int r = Convert.ToInt32(hex.Substring(0, 2), 16), g = Convert.ToInt32(hex.Substring(2, 2), 16), b = Convert.ToInt32(hex.Substring(4, 2), 16);
                // Keep the speaker's colour identity while reducing brightness.
                return "<color=#" + ((int)(r * .48)).ToString("X2") + ((int)(g * .48)).ToString("X2") + ((int)(b * .48)).ToString("X2") + m.Groups[2].Value + ">";
            }, RegexOptions.IgnoreCase);
            muted = Regex.Replace(muted, @"<b><color=#[0-9a-f]{6}>YOU - </color></b>", "<b><color=" + PlayerNameColor + ">YOU - </color></b>", RegexOptions.IgnoreCase);
            return "<color=#787878>" + muted + "</color>";
        }

        public static string Continuation(string text)
        {
            return Regex.Replace(text ?? "", @"^<b><color=#[0-9a-f]{6}(?:[0-9a-f]{2})?>[^<]* - </color></b>", "", RegexOptions.IgnoreCase);
        }

        public static string Entry(string name, string text, bool player) { return Entry(name, text, player, null); }
        public static string Entry(string name, string text, bool player, string nameColor)
        {
            string clean = Clean(text);
            // Native ProcessSpeakerText can already prepend a coloured name.
            string plainName = Plain(name).TrimEnd(':', '-', ' ', '\u2013', '\u2014');
            if (!String.IsNullOrEmpty(plainName))
            {
                string pattern = @"^\s*(?:<[^>]+>)*" + Regex.Escape(plainName) + @"\s*(?::|\-|\u2013|\u2014)\s*(?:</color>)?";
                clean = Regex.Replace(clean, pattern, "", RegexOptions.IgnoreCase);
            }
            // Strip name-prefix colour closures only as balanced input below;
            // normalize remaining tags through Balance so clipped prefixes are safe.
            clean = Balance(clean);
            if (String.IsNullOrEmpty(plainName)) return clean;
            return "<b><color=" + (player ? PlayerNameColor : (String.IsNullOrEmpty(nameColor) ? "#E6E0D4" : nameColor)) + ">" + (player ? "YOU" : plainName.ToUpperInvariant()) + " - </color></b>" + clean;
        }

        public static string Balance(string text)
        {
            var stack = new System.Collections.Generic.List<string>();
            text = Regex.Replace(text, @"</?(?:b|i|color)(?:=[^>]+)?>", delegate(Match m) {
                string tag = m.Value;
                string name = Regex.Match(tag, @"[a-z]+", RegexOptions.IgnoreCase).Value.ToLowerInvariant();
                if (!tag.StartsWith("</")) { stack.Add(name); return tag; }
                if (stack.Count == 0 || stack[stack.Count - 1] != name) return "";
                stack.RemoveAt(stack.Count - 1); return tag;
            });
            for (int i = stack.Count - 1; i >= 0; i--) text += "</" + stack[i] + ">";
            return text;
        }

        public static string Option(string text, bool read)
        { return Option(text, read, true, null); }
        public static string Option(string text, bool read, bool available, string disabledColor)
        {
            text = Clean(text);
            if (!available)
                return "<color=" + (String.IsNullOrEmpty(disabledColor) ? "#C34F46" : disabledColor) + ">" + Regex.Replace(text, @"</?color(?:=[^>]+)?>", "", RegexOptions.IgnoreCase) + "</color>";
            // Replace only a plain whole-label base colour. Native qualifier
            // spans inside the answer retain their authored colours.
            text = Regex.Replace(text, @"^<color=#[0-9a-f]{6}(?:[0-9a-f]{2})?>([^<>]*)</color>$", "$1", RegexOptions.IgnoreCase);
            if (!Regex.IsMatch(text, @"<color=", RegexOptions.IgnoreCase))
            {
                text = Regex.Replace(text, @"\[(Attack)\]", "<color=#DD0000>[$1]</color>", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"\[(Athletics|Alchemy|Arcana|Bluff|Diplomacy|Explosives|History|Insight|Intimidate|Mechanics|Metaphysics|Religion|Sleight of Hand|Stealth|Streetwise|Survival|Might|Constitution|Dexterity|Perception|Intellect|Resolve)([^\]]*)\]", "<color=#21CB03>[$1$2]</color>", RegexOptions.IgnoreCase);
            }
            string plain = Plain(text);
            bool muted = read || Regex.IsMatch(plain, @"again\?|(?:tell|explain).*again|remind me|more questions about", RegexOptions.IgnoreCase);
            text = "<color=" + (muted ? "#747474" : "#B4C5D3") + ">" + text + "</color>";
            return Balance(text);
        }
    }
}
