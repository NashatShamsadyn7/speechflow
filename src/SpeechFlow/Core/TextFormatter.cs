using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpeechFlow.Core;

/// <summary>
/// Turns one final transcript segment into the exact text to type.
/// Deepgram's smart_format already handles capitalisation, numbers and English punctuation
/// (dictation=true turns "comma", "new line" etc. into symbols), so this class only does what
/// Deepgram can't: Arabic punctuation commands, Arabic punctuation marks, snippets and the
/// space that joins one segment to the text already typed. It never inserts spaces inside
/// tokens, so emails, URLs and numbers like 3.5 or 1,000 pass through untouched.
/// </summary>
public sealed class TextFormatter
{
    const string Letter = @"[\p{L}\p{M}\p{N}]";

    static readonly (Regex Pattern, string Symbol)[] ArabicCommands = BuildCommands(new (string, string)[]
    {
        ("فاصلة منقوطة", "؛"), ("فاصله منقوطه", "؛"),
        ("علامة استفهام", "؟"), ("علامه استفهام", "؟"),
        ("علامة تعجب", "!"), ("علامه تعجب", "!"),
        ("فقرة جديدة", "\n\n"), ("فقره جديده", "\n\n"),
        ("سطر جديد", "\n"),
        ("افتح قوس", "("), ("افتح القوس", "("),
        ("اغلق قوس", ")"), ("أغلق قوس", ")"), ("اقفل القوس", ")"), ("أغلق القوس", ")"),
        ("نقطتان", ":"),
        ("فاصلة", "،"), ("فاصله", "،"),
    });

    // "نقطة" is also an everyday word ("نقطة مهمة"), so it only counts as a full stop
    // when it ends the phrase, which is where people say it.
    static readonly Regex TrailingFullStop = new($@"(?<!{Letter})نقط[ةه][\s.،]*$");

    static readonly Regex Tashkeel = new(@"[ؗ-ًؚ-ْٰ]");
    static readonly Regex SpaceBeforeClosing = new(@"[ \t]+(?=[،؛؟!.,:;?)\]])");
    static readonly Regex SpaceAfterOpening = new(@"(?<=[(\[])[ \t]+");
    static readonly Regex SpaceAroundNewline = new(@"[ \t]*\n[ \t]*");
    static readonly Regex DuplicateAfterArabicMark = new(@"(?<=[،؛؟!:])[ \t]*[.,،]");
    static readonly Regex MissingSpaceAfterArabicMark = new(@"(?<=[،؛؟])(?=[^\s\d)\]])");
    static readonly Regex MissingSpaceBeforeOpening = new($@"(?<={Letter})(?=\()");
    static readonly Regex MissingSpaceAfterClosing = new($@"(?<=\))(?={Letter})");
    static readonly Regex RepeatedSpaces = new(@"[ \t]{2,}");

    List<(Regex Pattern, string Text)> _snippets = new();

    public bool VoiceCommands { get; set; } = true;
    public bool StripTashkeel { get; set; }

    public void SetSnippets(IEnumerable<Snippet> snippets)
    {
        _snippets = snippets
            .Where(s => !string.IsNullOrWhiteSpace(s.Trigger) && !string.IsNullOrEmpty(s.Text))
            .OrderByDescending(s => s.Trigger.Trim().Length)
            .Select(s => (new Regex($@"(?<!{Letter}){Regex.Escape(s.Trigger.Trim())}(?!{Letter})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), s.Text))
            .ToList();
    }

    /// <param name="segment">Final transcript from the speech engine.</param>
    /// <param name="language">"ar" or "en".</param>
    /// <param name="previousChar">Last character already typed in this field, or null if unknown.</param>
    public string Format(string segment, string language, char? previousChar)
    {
        var text = segment.Trim();
        if (text.Length == 0) return "";

        if (language == "ar")
        {
            if (StripTashkeel) text = Tashkeel.Replace(text, "");
            text = ToArabicPunctuation(text);
            if (VoiceCommands) text = ApplyArabicCommands(text);
        }

        text = Tidy(text);

        // Snippets go last so their text (an email address, a signature) is typed verbatim.
        foreach (var (pattern, expansion) in _snippets)
            text = pattern.Replace(text, _ => expansion);

        if (text.Length == 0) return "";
        return NeedsLeadingSpace(previousChar, text[0]) ? " " + text : text;
    }

    static string ApplyArabicCommands(string text)
    {
        foreach (var (pattern, symbol) in ArabicCommands)
            text = pattern.Replace(text, symbol);
        return TrailingFullStop.Replace(text, ".");
    }

    /// <summary>Latin , ? ; → Arabic marks, except inside numbers (1,000) and tokens (a?b).</summary>
    static string ToArabicPunctuation(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char prev = i > 0 ? text[i - 1] : '\0';
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            bool endsWord = next == '\0' || char.IsWhiteSpace(next);
            sb.Append(c switch
            {
                ',' when !(char.IsDigit(prev) && char.IsDigit(next)) => '،',
                '?' when endsWord => '؟',
                ';' when endsWord => '؛',
                _ => c,
            });
        }
        return sb.ToString();
    }

    static string Tidy(string text)
    {
        text = SpaceAroundNewline.Replace(text, "\n");
        text = DuplicateAfterArabicMark.Replace(text, "");
        text = SpaceBeforeClosing.Replace(text, "");
        text = SpaceAfterOpening.Replace(text, "");
        text = MissingSpaceAfterArabicMark.Replace(text, " ");
        text = MissingSpaceBeforeOpening.Replace(text, " ");
        text = MissingSpaceAfterClosing.Replace(text, " ");
        text = RepeatedSpaces.Replace(text, " ");
        return text.Trim(' ', '\t');
    }

    static bool NeedsLeadingSpace(char? previous, char first)
    {
        if (previous is not char prev) return false;
        if (char.IsWhiteSpace(prev) || prev is '(' or '[') return false;
        if (char.IsWhiteSpace(first) || "،؛؟!.,:;?)]".IndexOf(first) >= 0) return false;
        return true;
    }

    static (Regex, string)[] BuildCommands(IEnumerable<(string Phrase, string Symbol)> commands) =>
        commands
            .OrderByDescending(c => c.Phrase.Length) // "فاصلة منقوطة" must win over "فاصلة"
            .Select(c => (new Regex($@"(?<!{Letter}){Regex.Escape(c.Phrase)}(?!{Letter})"), c.Symbol))
            .ToArray();
}
