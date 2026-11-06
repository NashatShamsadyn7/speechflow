using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpeechFlow.Core;

/// <summary>
/// Turns one transcribed phrase into the exact text to type.
/// Whisper already capitalises and punctuates, so this class only adds spoken punctuation
/// commands, Arabic punctuation marks, snippets and the space that joins one phrase to the text
/// already typed. It never inserts spaces inside tokens, so emails, URLs and numbers like 3.5
/// or 1,000 pass through untouched.
/// </summary>
public sealed class TextFormatter
{
    const string Letter = @"[\p{L}\p{M}\p{N}]";

    static readonly (Regex Pattern, string Symbol)[] EnglishCommands = BuildCommands(new (string, string)[]
    {
        ("new paragraph", "\n\n"), ("new line", "\n"),
        ("question mark", "?"), ("exclamation mark", "!"), ("exclamation point", "!"),
        ("open bracket", "("), ("close bracket", ")"),
        ("comma", ","),
    }, RegexOptions.IgnoreCase);

    // "period" and "full stop" are ordinary words too; like "نقطة" they only count at the end.
    static readonly Regex TrailingEnglishFullStop = new($@"(?<!{Letter})(period|full stop)[\s.,]*$", RegexOptions.IgnoreCase);

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
    // Whisper often punctuates the spoken command too: "comma," → ",," / "سطر جديد." → "\n."
    static readonly Regex DuplicateAfterMark = new(@"(?<=[،؛؟!:,?\n])[ \t]*[.,،]");
    // A number standing on its own (35, 3.5, 1,000, 3:30), not part of an email, URL or code like "v2".
    static readonly Regex StandaloneNumber = new(@"(?<![A-Za-z0-9@_\-/.])[0-9]+(?:[.,:][0-9]+)*(?![A-Za-z0-9@_\-])");
    static readonly Regex ArabicLetters = new(@"[؀-ۿ]");
    static readonly Regex LatinLetters = new(@"[A-Za-z]");
    static readonly Regex MissingSpaceAfterArabicMark = new(@"(?<=[،؛؟])(?=[^\s\d)\]])");
    static readonly Regex MissingSpaceBeforeOpening = new($@"(?<={Letter})(?=\()");
    static readonly Regex MissingSpaceAfterClosing = new($@"(?<=\))(?={Letter})");
    static readonly Regex RepeatedSpaces = new(@"[ \t]{2,}");

    List<(Regex Pattern, string Text)> _snippets = new();

    public bool VoiceCommands { get; set; } = true;
    public bool StripTashkeel { get; set; }
    /// <summary>In Arabic text, write 35 as ٣٥.</summary>
    public bool ArabicDigits { get; set; }

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
            if (ArabicDigits) text = StandaloneNumber.Replace(text, m => ToArabicIndic(m.Value));
        }
        else if (VoiceCommands)
        {
            text = ApplyEnglishCommands(text);
        }

        text = Tidy(text);

        // Snippets go last so their text (an email address, a signature) is typed verbatim.
        foreach (var (pattern, expansion) in _snippets)
            text = pattern.Replace(text, _ => expansion);

        if (text.Length == 0) return "";
        return NeedsLeadingSpace(previousChar, text[0]) ? " " + text : text;
    }

    /// <summary>"ar" if the text is mostly Arabic script, otherwise "en". Used in auto-language mode.</summary>
    public static string DetectLanguage(string text) =>
        ArabicLetters.Count(text) >= LatinLetters.Count(text) && ArabicLetters.IsMatch(text) ? "ar" : "en";

    static string ToArabicIndic(string number)
    {
        var chars = number.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] is >= '0' and <= '9') chars[i] = (char)('٠' + (chars[i] - '0'));
        return new string(chars);
    }

    static string ApplyArabicCommands(string text)
    {
        foreach (var (pattern, symbol) in ArabicCommands)
            text = pattern.Replace(text, symbol);
        return TrailingFullStop.Replace(text, ".");
    }

    static string ApplyEnglishCommands(string text)
    {
        foreach (var (pattern, symbol) in EnglishCommands)
            text = pattern.Replace(text, symbol);
        return TrailingEnglishFullStop.Replace(text, ".");
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
        text = DuplicateAfterMark.Replace(text, "");
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

    static (Regex, string)[] BuildCommands(IEnumerable<(string Phrase, string Symbol)> commands,
        RegexOptions options = RegexOptions.None) =>
        commands
            .OrderByDescending(c => c.Phrase.Length) // "فاصلة منقوطة" must win over "فاصلة"
            .Select(c => (new Regex($@"(?<!{Letter}){Regex.Escape(c.Phrase)}(?!{Letter})", options), c.Symbol))
            .ToArray();
}
