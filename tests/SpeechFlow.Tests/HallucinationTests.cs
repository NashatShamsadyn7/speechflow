using SpeechFlow.Core.Speech;
using Xunit;

namespace SpeechFlow.Tests;

public class HallucinationTests
{
    [Fact]
    public void Repetition_loop_is_collapsed() =>
        Assert.Equal("Please send the report to my manager, and send the report to my manager",
            Hallucinations.Clean("Please send the report to my manager, and send the report to my manager, and send the report to my manager, and send the report to my manager"));

    [Fact]
    public void Arabic_loop_is_collapsed() =>
        Assert.Equal("شكرا لكم على المتابعة", Hallucinations.Clean("شكرا لكم على المتابعة شكرا لكم على المتابعة شكرا لكم على المتابعة"));

    [Fact]
    public void Normal_text_is_untouched() =>
        Assert.Equal("The meeting starts at 3.30.", Hallucinations.Clean(" The meeting starts at 3.30. "));

    [Theory]
    [InlineData("ترجمة نانسي قنقر")]
    [InlineData("Thanks for watching!")]
    [InlineData("  ")]
    public void Noise_phrases_are_dropped(string text) => Assert.Equal("", Hallucinations.Clean(text));
}
