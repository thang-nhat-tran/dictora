using System.Text.RegularExpressions;

namespace Dictora.ArchitectureTests;

/// <summary>
/// Large third-party libraries stay behind our own interfaces (ADR-002), so they can be swapped
/// by writing one new adapter. Checked on the source, because reflection can't see method bodies.
/// </summary>
public class ThirdPartyIsolationTests
{
    [Fact]
    public void Only_the_caption_adapter_uses_YoutubeExplode()
    {
        var offenders = SourceFiles()
            // The namespace (`using YoutubeExplode;`, `YoutubeExplode.Videos…`), not our adapter's name.
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bYoutubeExplode[.;]"))
            .Select(Path.GetFileName)
            .Where(name => name != "YoutubeExplodeCaptionSource.cs");

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> SourceFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dictora.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Directory.EnumerateFiles(Path.Combine(dir.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }
}
