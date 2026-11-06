using SpeechFlow.Core;
using Xunit;

namespace SpeechFlow.Tests;

public class TextFormatterTests
{
    readonly TextFormatter _f = new();

    [Theory]
    [InlineData("email me at name@domain.com", "email me at name@domain.com")]
    [InlineData("it costs 3.5 dollars", "it costs 3.5 dollars")]
    [InlineData("visit google.com today.", "visit google.com today.")]
    [InlineData("I said hello, then left.", "I said hello, then left.")]
    public void English_text_is_left_intact(string input, string expected) =>
        Assert.Equal(expected, _f.Format(input, "en", null));

    [Fact]
    public void Arabic_numbers_keep_their_separators() =>
        Assert.Equal("المبلغ 1,000 دينار و 2.5 كيلو", _f.Format("المبلغ 1,000 دينار و 2.5 كيلو", "ar", null));

    [Fact]
    public void Arabic_gets_arabic_punctuation_marks() =>
        Assert.Equal("مرحبا، كيف الحال؟", _f.Format("مرحبا, كيف الحال?", "ar", null));

    [Fact]
    public void Semicolon_command_wins_over_comma() =>
        Assert.Equal("أولا؛ ثانيا", _f.Format("أولا فاصلة منقوطة ثانيا", "ar", null));

    [Fact]
    public void Comma_command_is_converted() =>
        Assert.Equal("نعم، صحيح", _f.Format("نعم فاصلة صحيح", "ar", null));

    [Fact]
    public void Full_stop_only_at_the_end_of_a_phrase()
    {
        Assert.Equal("انتهينا.", _f.Format("انتهينا نقطة", "ar", null));
        Assert.Equal("هذه نقطة مهمة", _f.Format("هذه نقطة مهمة", "ar", null));
    }

    [Fact]
    public void Definite_words_are_not_commands() =>
        Assert.Equal("اللحظة الفاصلة", _f.Format("اللحظة الفاصلة", "ar", null));

    [Fact]
    public void Police_is_not_a_dash() =>
        Assert.Equal("اتصل بالشرطة وشرطة المرور", _f.Format("اتصل بالشرطة وشرطة المرور", "ar", null));

    [Fact]
    public void New_line_command() =>
        Assert.Equal("السطر الأول\nالسطر الثاني", _f.Format("السطر الأول سطر جديد السطر الثاني", "ar", null));

    [Fact]
    public void Question_command_and_deepgram_punctuation_do_not_double() =>
        Assert.Equal("هل أنت بخير؟", _f.Format("هل أنت بخير علامة استفهام.", "ar", null));

    [Fact]
    public void Voice_commands_can_be_disabled()
    {
        var f = new TextFormatter { VoiceCommands = false };
        Assert.Equal("نعم فاصلة صحيح", f.Format("نعم فاصلة صحيح", "ar", null));
    }

    [Fact]
    public void Tashkeel_can_be_stripped()
    {
        var f = new TextFormatter { StripTashkeel = true };
        Assert.Equal("مرحبا بكم", f.Format("مَرْحَبًا بِكُمْ", "ar", null));
    }

    [Fact]
    public void Segments_are_joined_with_one_space()
    {
        Assert.Equal(" world", _f.Format("world", "en", 'o'));
        Assert.Equal("world", _f.Format("world", "en", ' '));
        Assert.Equal("world", _f.Format("world", "en", '\n'));
        Assert.Equal(", then", _f.Format(", then", "en", 'o'));
    }

    [Fact]
    public void Snippets_match_whole_phrases_only()
    {
        _f.SetSnippets(new[] { new Snippet { Trigger = "my email", Text = "me@example.com" } });
        Assert.Equal("send to me@example.com.", _f.Format("send to my email.", "en", null));
        Assert.Equal("my emails are many", _f.Format("my emails are many", "en", null));
    }

    [Fact]
    public void Snippet_text_is_typed_verbatim()
    {
        _f.SetSnippets(new[] { new Snippet { Trigger = "توقيعي", Text = "Nashat , Erbil" } });
        Assert.Equal("Nashat , Erbil", _f.Format("توقيعي", "ar", null));
    }

    [Fact]
    public void English_spoken_commands()
    {
        Assert.Equal("Dear Sam,\nthanks?", _f.Format("Dear Sam comma new line thanks question mark", "en", null));
        Assert.Equal("See you soon.", _f.Format("See you soon period", "en", null));
        Assert.Equal("That period was hard", _f.Format("That period was hard", "en", null));
    }

    [Fact]
    public void Whisper_punctuating_the_command_does_not_double() =>
        Assert.Equal("Hello, world", _f.Format("Hello comma, world", "en", null));

    [Fact]
    public void Language_detection_by_script()
    {
        Assert.Equal("ar", TextFormatter.DetectLanguage("مرحبا بالعالم"));
        Assert.Equal("en", TextFormatter.DetectLanguage("Hello world"));
        Assert.Equal("ar", TextFormatter.DetectLanguage("أرسل الملف PDF"));
    }

    [Fact]
    public void Empty_segment_types_nothing() =>
        Assert.Equal("", _f.Format("   ", "en", 'a'));
}

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("ctrl + shift + l")]
    [InlineData("Ctrl+Shift+Backspace")]
    [InlineData("Alt+1")]
    [InlineData("Win+Alt+F9")]
    public void Valid(string gesture) => Assert.True(HotkeyGesture.TryParse(gesture, out _, out _));

    [Theory]
    [InlineData("")]
    [InlineData("Space")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Nope")]
    [InlineData("Ctrl+A+B")]
    public void Invalid(string gesture) => Assert.False(HotkeyGesture.TryParse(gesture, out _, out _));
}
