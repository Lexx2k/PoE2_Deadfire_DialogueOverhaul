using Deadfire.DialogOverhaul;

static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
var speech = Formatting.Entry("Aloth", "<color=#999999>Aloth: </color><color=#FFFFFF>\"A <i>word</i>.\"</color>", false);
Check(speech.StartsWith("<b><color=#E6E0D4>ALOTH - </color></b>"), "Uppercase speaker / unified separator");
Check(Formatting.Plain(speech) == "ALOTH - \"A word.\"", "Native name must occur only once");
Check(speech.Contains("<i>word</i>"), "Keep emphasis");
Check(Formatting.Plain(Formatting.Entry("Éder", "<color=#aabbcc>Éder - </color>Hello.", false)) == "ÉDER - Hello.", "Unicode name");
Check(Formatting.Entry("Watcher", "Yes.", true).Contains("#8C8068>YOU - "), "Player label uses fixed warm beige YOU");
Check(Formatting.Entry("Watcher", "Yes.", true, "#FF0000").Contains("#8C8068>YOU - "), "Native player colour cannot override YOU");
Check(Formatting.History(Formatting.Entry("Watcher", "Yes.", true, "#FF0000")).Contains("#8C8068>YOU - "), "Historical YOU retains the same fixed colour");
Check(InlineMarkup.Parse(Formatting.Clean("<link=\"lore\">Adra</link> <sprite=2> <b>stone</b>"), null).Text == "Adra  <b>stone</b>", "Separate link metadata from rendered text while stripping unsupported TMP layout tags");
Check(Formatting.Option("1. [Diplomacy 4] Talk.", false).Contains("#21CB03>[Diplomacy 4]"), "Skill colour");
Check(Formatting.Option("[Attack] Fight.", false).Contains("#DD0000>[Attack]"), "Attack colour");
Check(Formatting.Option("Tell me again?", false).StartsWith("<color=#747474>"), "Repeat prompt colour");
Check(Formatting.Option("Already read.", true).StartsWith("<color=#747474>"), "Visited response colour");
Check(Formatting.Balance("</color>Hello <i>there") == "Hello <i>there</i>", "Repair native prefix tag boundaries");
Check(Formatting.Plain("<XG>Humaire</XG>") == "Humaire", "Strip native speaker-name markup");
var hover = Formatting.Hover("<color=#747474>1. <i>Ask</i> <color=#21CB03>[Insight]</color></color>");
Check(hover == "<color=#F3983D>1. <i>Ask</i> [Insight]</color>", "Hover overrides nested read/skill colours while preserving emphasis");
Check(Formatting.Plain(hover) == "1. Ask [Insight]", "Hover preserves visible reply text");
Check(Formatting.Option("<color=#FFFFFF>2. Ask.</color>", false) == "<color=#B4C5D3>2. Ask.</color>", "Reply palette overrides native base colours");
Check(Formatting.Entry("Benweth", "Speak.", false, "#A23C2A").Contains("<color=#A23C2A>BENWETH - "), "Preserve native red speaker name");
Check(Formatting.Entry("Captain Furrante", "Speak.", false, "#447D39").Contains("<color=#447D39>CAPTAIN FURRANTE - "), "Preserve native green speaker name");
var history = Formatting.History("<b><color=#FF0000>NAME - </color></b><color=#FFFFFF>Old <i>line</i>.</color>");
Check(history.Contains("<color=#7A0000>NAME - ") && history.Contains("<color=#7A7A7A>Old <i>line</i>."), "History strongly dims both name and body while retaining hue and emphasis");
Check(Formatting.Plain(history) == "NAME - Old line.", "History preserves visible conversation text");
Check(Formatting.Plain(Formatting.Entry("Fordham", "<color=#FFFFFF>Fordham - </color>Ask.", true)) == "YOU - Ask.", "Strip native player name before displaying YOU");
Check(Formatting.Continuation(Formatting.Entry("Humaire", "\"Before this place...\"", false)) == "\"Before this place...\"", "Consecutive speaker passage starts directly with dialogue");
Check(Formatting.Continuation("<i>Narration.</i>") == "<i>Narration.</i>", "Continuation preserves text without a speaker prefix");
var nativeMixed = Formatting.Entry("Zamar", "<#00AA00>Zamar-</color><#B9B9B9><#FFFFFF>\"Business <b>comes</b> and <i>goes</i>.\"<#B9B9B9> Zamar winks.</color>", false, "#00AA00");
Check(nativeMixed.Contains("<color=#FFFFFF>\"Business <b>comes</b> and <i>goes</i>.\""), "Native speech shorthand colour and authored emphasis survive conversion");
Check(nativeMixed.Contains("<color=#B9B9B9> Zamar winks."), "Narration returns to its native grey after speech");
Check(Formatting.Plain(nativeMixed) == "ZAMAR - \"Business comes and goes.\" Zamar winks.", "Native mixed paragraph keeps wording without duplicate names");
Check(Formatting.History(nativeMixed).Contains("<color=#585858> Zamar winks."), "History dims native narration while retaining speech contrast");
var unavailable = Formatting.Option("2: <#21CB03><i>[Insight 8]</i></color> [Requirement not met]", false, false, "#B74436");
Check(unavailable == "<color=#B74436>2: <i>[Insight 8]</i> [Requirement not met]</color>", "Unavailable answers retain native failure colour and emphasis throughout");
Check(Formatting.Option("1: <#AABBCC>[Insight 8]</color> Ask.", false).Contains("<color=#AABBCC>[Insight 8]</color>"), "Available answers preserve native qualifier colours instead of replacing them");
Formatting.IconLinkLabel = link => link.EndsWith("51f0db73-b83e-4906-a47d-b5fb2f25f27d") ? "Anti-Gods" : null;
var reaction = Formatting.Clean("[<link=\"gamedata://51f0db73-b83e-4906-a47d-b5fb2f25f27d\"><sprite=\"Inline\" name=\"gui/icons/gamesystems/anti_gods.png\" tint=1></link>] A sound of frustration breaks from Xoti's throat.");
Check(InlineMarkup.Parse(reaction, null).Text == "[Anti-Gods] A sound of frustration breaks from Xoti's throat.", "Unavailable icon assets retain localized fallback text");
Formatting.IconLinkLabel = null;
Check(InlineMarkup.Parse(Formatting.Clean("[<link=\"gamedata://unknown\"><sprite=\"Inline\" name=\"gui/icons/gamesystems/anti_gods.png\" tint=1></link>]"), null).Text == "[Anti Gods]", "Unresolved icons retain readable filename fallback");
Check(InlineMarkup.Parse(Formatting.Clean("1: [<sprite=\"Inline\" name=\"gui/icons/insight.png\" tint=1> 8] Ask."), null).Text == "1: [Insight 8] Ask.", "Unlinked requirement icons retain their meaning");
var iconDocument = InlineMarkup.Parse(reaction, name => true);
Check(iconDocument.Plain.StartsWith("[\u2009\u25A0\u2009] A sound"), "Loaded icon reserves balanced horizontal padding inside brackets");
Check(iconDocument.Spans.Exists(s => s.Icon != null && s.Start == 2 && s.Length == 1 && s.Url.EndsWith("51f0db73-b83e-4906-a47d-b5fb2f25f27d")), "Padded reaction icon retains its tooltip URL and character position");
var linkedDocument = InlineMarkup.Parse(Formatting.Entry("Zamar", "<#FFFFFF>Ask about <link=\"glossary://adra\"><i>Adra</i></link>.", false), null);
Check(linkedDocument.Spans.Count == 1 && linkedDocument.Plain.Substring(linkedDocument.Spans[0].Start, linkedDocument.Spans[0].Length) == "Adra", "Glossary hit span excludes markup and speaker prefix");
Check(linkedDocument.Text.Contains("<i>Adra</i>") && !linkedDocument.Text.Contains("<link"), "Rendered link retains authored emphasis without unsupported tags");
Check(InlineMarkup.Parse(Formatting.History(reaction), name => true).Spans.Exists(s => s.Icon != null), "History muting retains reaction icon metadata");
var failedIcon = InlineMarkup.Parse(Formatting.Option("2: [<link=\"gamedata://skill\"><sprite=\"Inline\" name=\"insight\"></link> 8]", false, false, "#B74436"), name => true);
Check(failedIcon.Spans.Exists(s => s.Icon != null && s.Color == "#B74436"), "Unavailable qualifier icon retains the native requirement-failure tint");
var nativeLore = InlineMarkup.Parse(Formatting.Entry("Humaire", "<#778899>Humaire-</color><#B9B9B9><#FFFFFF>\"This is the heart of <link=\"glossary://deity\">Toamowhai</link>'s sacred isle.\"<#B9B9B9></color>", false), null);
Check(nativeLore.Spans.Count == 1 && nativeLore.Plain.Substring(nativeLore.Spans[0].Start, nativeLore.Spans[0].Length) == "Toamowhai", "Native lore link retains exact word span before possessive suffix inside coloured speech");
var colouredLink = InlineMarkup.Parse("<link=\"glossary://deity\">Toamowhai</link>'s", null, url => "#80C0B0");
Check(colouredLink.Text == "<color=#80C0B0>Toamowhai</color>'s", "Link tint is emitted as standard rich text independently of mesh effects");
Check(InlineMarkup.QuadIndex(colouredLink, 0, colouredLink.Text.Length) == colouredLink.Text.IndexOf("Toamowhai"), "Legacy text mesh slots include rich-text source tags");
Check(InlineMarkup.QuadIndex(colouredLink, 8, colouredLink.Text.Length) == colouredLink.Text.IndexOf("Toamowhai") + 8, "Each linked letter maps to its source mesh slot");
Check(InlineMarkup.QuadIndex(colouredLink, 8, colouredLink.Plain.Length) == 8, "Compact mesh path still maps visible characters directly");
Check(iconDocument.SourceIndices.Count == iconDocument.Plain.Length && iconDocument.Text[iconDocument.SourceIndices[2]] == '\u25A0', "Padded icon mapping points at its placeholder glyph, excluding transparent colour tags");
Console.WriteLine("45 formatting and inline metadata checks passed.");
var party = new PartyOverloads();
var actor = new Actor();
Check((bool)MethodDispatch.Invoke(party, "IsActivePartyMember", actor), "Select actor overload rather than equally sized Guid overload");
Check((string)MethodDispatch.Invoke(party, "Pick", "text") == "string", "Prefer exact overload over assignable overload");
Check((string)MethodDispatch.Invoke(party, "Inherited", actor) == "base", "Inherited nonpublic handler");
Check((string)MethodDispatch.Invoke(party, "NoArguments") == "ready", "Zero-argument lifecycle handler");
bool rejected = false;
try { MethodDispatch.Invoke(party, "IsActivePartyMember", 1); } catch (MissingMethodException) { rejected = true; }
Check(rejected, "Reject incompatible overload instead of invoking it");
Console.WriteLine("5 reflection dispatch regression checks passed.");
float Start(float viewport, float block) { return viewport - block - TranscriptLayout.TrailingSpace(viewport, block); }
Check(Math.Abs(Start(800, 50) - Start(800, 100)) < .01f, "Short choice lists share the same first-option position");
Check(Math.Abs(Start(800, 100) - 576) < .01f, "First choice anchor is relative to viewport height");
Check(Start(800, 400) < Start(800, 100), "Long replies move the whole block up");
Check(Math.Abs(Start(800, 400) + 400 - 760) < .01f, "Long reply block retains bottom breathing room");
Check(Math.Abs(Start(1000, 100) / 1000 - Start(800, 100) / 800) < .01f, "Short response anchor scales with viewport");
float ChoiceScreenTop(float historyHeight, float npcHeight, float gap, float choicesHeight)
{
    float first = historyHeight + npcHeight + gap;
    float total = first + choicesHeight + TranscriptLayout.TrailingSpace(800, choicesHeight);
    return first - (total - 800);
}
Check(Math.Abs(ChoiceScreenTop(900, 50, 48, 100) - ChoiceScreenTop(900, 150, 48, 100)) < .01f, "NPC paragraph length does not move choices");
Check(Math.Abs(ChoiceScreenTop(900, 50, 24, 100) - ChoiceScreenTop(900, 50, 48, 100)) < .01f, "Doubling the dialogue/choice gap does not move the option anchor");
Check(Math.Abs(ChoiceScreenTop(900, 50, 48, 100) - ChoiceScreenTop(1400, 50, 48, 100)) < .01f, "Adding player/history text does not move current choices");
Check(Math.Abs(TranscriptLayout.ContentHeight(800, 1000, 0) - TranscriptLayout.ContentHeight(800, 1000, 148)) < .01f, "Empty intermediate response list reserves the same layout extent as a fitting populated list");
Check(TranscriptLayout.ContentHeight(800, 1000, 400) > TranscriptLayout.ContentHeight(800, 1000, 0), "Oversized choices may still push transcript upward");
Console.WriteLine("10 relative transcript layout checks passed.");
Check(Math.Abs(DialogueCameraLayout.FocusViewportX(0, 1920, 1309) * 1920 - 654.5f) < .01f, "Dialogue camera targets centre of visible left area");
Check(Math.Abs(DialogueCameraLayout.FocusViewportX(0, 1920, 1920) - .5f) < .001f, "No occlusion preserves full viewport centre");
Check(Math.Abs(DialogueCameraLayout.FocusViewportX(160, 1600, 1309) * 1600 + 160 - 734.5f) < .01f, "Respect camera viewport origin and letterboxing");
Check(Math.Abs(DialogueCameraLayout.FocusViewportX(0, 2560, 1949) * 2560 - 974.5f) < .01f, "Ultrawide focus follows actual panel boundary");
Check(Math.Abs(DialogueCameraLayout.FocusViewportX(0, 1920, 1309, 12) * 1920 - 666.5f) < .01f, "Small camera-left adjustment places speaker twelve pixels farther right");
Check(Math.Abs(DialogueCameraLayout.FocusViewportX(0, 1920, 1920, 12) - .5f) < .001f, "Visual speaker bias does not shift an unobstructed viewport");
Console.WriteLine("6 dialogue camera framing checks passed.");

class Actor { }
class PartyBase { protected string Inherited(Actor actor) { return "base"; } }
class PartyOverloads : PartyBase
{
    public bool IsActivePartyMember(Guid id) { throw new Exception("Wrong overload invoked"); }
    public bool IsActivePartyMember(Actor actor) { return true; }
    public string Pick(object value) { return "object"; }
    public string Pick(string value) { return "string"; }
    private string NoArguments() { return "ready"; }
}
