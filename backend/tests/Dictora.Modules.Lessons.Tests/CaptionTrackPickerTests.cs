using Dictora.Modules.Lessons.Infrastructure.YouTube;

namespace Dictora.Modules.Lessons.Tests;

public class CaptionTrackPickerTests
{
    private static int Pick(params (string Code, bool Auto)[] tracks) =>
        CaptionTrackPicker.PickEnglish(tracks.Select(t => new CaptionTrackCandidate(t.Code, t.Auto)).ToList());

    [Fact]
    public void Prefers_manual_en_over_auto_en() =>
        Assert.Equal(1, Pick(("en", true), ("en", false)));

    [Fact]
    public void Prefers_manual_en_over_manual_regional_english() =>
        Assert.Equal(1, Pick(("en-US", false), ("en", false)));

    [Fact]
    public void Prefers_manual_regional_english_over_auto_en() =>
        Assert.Equal(1, Pick(("en", true), ("en-GB", false)));

    [Fact]
    public void Prefers_auto_en_over_auto_regional_english() =>
        Assert.Equal(1, Pick(("en-US", true), ("en", true)));

    [Fact]
    public void Takes_the_first_match_on_duplicates() =>
        Assert.Equal(1, Pick(("es", false), ("en", false), ("en", false), ("en-US", false)));

    [Fact]
    public void Accepts_auto_generated_english_only() =>
        Assert.Equal(0, Pick(("en", true), ("fr", false)));

    [Fact]
    public void Matches_language_codes_case_insensitively() =>
        Assert.Equal(0, Pick(("EN-gb", false)));

    [Theory]
    [InlineData("ko")]
    [InlineData("eng")]
    [InlineData("enx")]
    [InlineData("")]
    public void Returns_minus_one_without_an_english_track(string code) =>
        Assert.Equal(-1, Pick((code, false), ("fr", true)));

    [Fact]
    public void Returns_minus_one_for_no_tracks() =>
        Assert.Equal(-1, Pick());
}
