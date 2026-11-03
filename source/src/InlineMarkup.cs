using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Deadfire.DialogOverhaul
{
    public sealed class InlineSpan
    {
        public int Start, Length;
        public string Url, Icon, Color;
    }
    public sealed class InlineDocument
    {
        public string Text, Plain;
        public readonly List<InlineSpan> Spans = new List<InlineSpan>();
        public readonly List<int> SourceIndices = new List<int>();
    }
    public static class InlineMarkup
    {
        public static InlineDocument Parse(string input, Func<string, bool> canDrawIcon)
        { return Parse(input, canDrawIcon, null); }
        public static InlineDocument Parse(string input, Func<string, bool> canDrawIcon, Func<string, string> linkColor)
        {
            InlineDocument result = new InlineDocument();
            StringBuilder output = new StringBuilder(), plain = new StringBuilder();
            string url = null; int linkStart = 0;
            var colors = new List<string>();
            foreach (Match token in Regex.Matches(input ?? "", @"<[^>]*>|[^<]+|<"))
            {
                string value = token.Value;
                Match link = Regex.Match(value, @"^<link=""([^""]+)"">$", RegexOptions.IgnoreCase);
                Match sprite = Regex.Match(value, @"^<sprite[^>]*\bname=""([^""]+)""[^>]*>$", RegexOptions.IgnoreCase);
                if (link.Success) { url = link.Groups[1].Value; linkStart = plain.Length; if (linkColor != null) output.Append("<color=" + linkColor(url) + ">"); }
                else if (value.Equals("</link>", StringComparison.OrdinalIgnoreCase))
                {
                    if (url != null && plain.Length > linkStart) result.Spans.Add(new InlineSpan { Start = linkStart, Length = plain.Length - linkStart, Url = url });
                    if (url != null && linkColor != null) output.Append("</color>");
                    url = null;
                }
                else if (sprite.Success)
                {
                    string name = sprite.Groups[1].Value;
                    if (canDrawIcon != null && canDrawIcon(name))
                    {
                        AppendVisible(output, plain, result.SourceIndices, "\u2009");
                        result.Spans.Add(new InlineSpan { Start = plain.Length, Length = 1, Url = url, Icon = name, Color = colors.Count == 0 ? "#FFFFFF" : colors[colors.Count - 1] });
                        output.Append("<color=#00000000>"); result.SourceIndices.Add(output.Length); output.Append("\u25A0</color>"); plain.Append('\u25A0');
                        AppendVisible(output, plain, result.SourceIndices, "\u2009");
                    }
                    else
                    {
                        string label = url != null && Formatting.IconLinkLabel != null ? Formatting.IconLinkLabel(url) : null;
                        if (String.IsNullOrEmpty(label)) label = Formatting.SpriteLabel(value);
                        AppendVisible(output, plain, result.SourceIndices, label);
                    }
                }
                else
                {
                    if (!value.StartsWith("<")) AppendVisible(output, plain, result.SourceIndices, value);
                    else output.Append(value);
                    Match color = Regex.Match(value, @"^<color=(#[0-9a-f]{6}(?:[0-9a-f]{2})?)>$", RegexOptions.IgnoreCase);
                    if (color.Success) colors.Add(color.Groups[1].Value);
                    else if (value.Equals("</color>", StringComparison.OrdinalIgnoreCase) && colors.Count > 0) colors.RemoveAt(colors.Count - 1);
                }
            }
            if (url != null && plain.Length > linkStart) { result.Spans.Add(new InlineSpan { Start = linkStart, Length = plain.Length - linkStart, Url = url }); if (linkColor != null) output.Append("</color>"); }
            result.Text = output.ToString(); result.Plain = plain.ToString(); return result;
        }
        private static void AppendVisible(StringBuilder output, StringBuilder plain, List<int> indices, string value)
        {
            for (int i = 0; i < value.Length; i++) { indices.Add(output.Length); output.Append(value[i]); plain.Append(value[i]); }
        }
        public static int QuadIndex(InlineDocument document, int character, int quadCount)
        {
            if (quadCount >= document.Text.Length) return document.SourceIndices[character];
            if (quadCount >= document.Plain.Length) return character;
            int index = character;
            for (int i = 0; i < character; i++) if (document.Plain[i] == '\n' || document.Plain[i] == '\r') index--;
            return index;
        }
    }
}
