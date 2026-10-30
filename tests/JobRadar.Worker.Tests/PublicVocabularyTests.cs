using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace JobRadar.Worker.Tests;

/// <summary>
/// The repository is public and the scoring engine generic: who may apply to a market's jobs is the owner's data, in the
/// local configuration (<c>Matching:Markets:&lt;market&gt;:Eligibility</c>). This test keeps the first market's legal
/// vocabulary out of every versioned file. A new phrase of that kind goes to the local block; a test with real wording,
/// to <c>scoring-cases.Local.json</c>.
/// </summary>
public partial class PublicVocabularyTests
{
    // The only file allowed to spell the words: it has to, to ban them.
    private static string Self([CallerFilePath] string path = "") => path;

    [GeneratedRegex(
        @"\b(?:visas?|vistos?|working\s+holiday|whv|backpackers?|work(?:ing)?\s+rights|direitos?\s+de\s+trabalho|88\s+(?:days|dias)"
        + @"|citizen\w*|cidadania|sponsor\w*|patroc[ií]nio|permanent\s+residen\w*|second[\s-]year|nv1|imigra\w*|migrat[óo]ri[ao])\b"
        + @"|(?<![\w.\-])(?:417|462|482|491|494)(?![\w.\-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Banned();

    private static readonly string[] BinaryExtensions = [".png", ".jpg", ".jpeg", ".gif", ".ico", ".woff", ".woff2", ".ttf", ".pdf", ".zip"];

    [Fact]
    public void No_versioned_file_uses_the_first_market_legal_vocabulary()
    {
        var root = ExampleConfigurationTests.RepositoryRoot();
        var self = Path.GetRelativePath(root, Self()).Replace('\\', '/');
        var hits = new List<string>();
        foreach (var file in TrackedFiles(root))
        {
            if (file == self || BinaryExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;
            var path = Path.Combine(root, file);
            if (!File.Exists(path))
                continue;   // deleted in the working tree, not yet committed
            var bytes = File.ReadAllBytes(path);
            if (bytes.AsSpan(0, Math.Min(bytes.Length, 8000)).Contains((byte)0))
                continue;
            var lines = Encoding.UTF8.GetString(bytes).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (Banned().Match(lines[i]) is { Success: true } m)
                    hits.Add($"{file}:{i + 1}: \"{m.Value}\" in {lines[i].Trim()[..Math.Min(lines[i].Trim().Length, 120)]}");
            }
        }

        Assert.True(hits.Count == 0,
            $"{hits.Count} line(s) use words that belong to the local configuration:{Environment.NewLine}"
            + string.Join(Environment.NewLine, hits.Take(100)));
    }

    [Theory]
    [InlineData("Version 1.482.0 of the package")]
    [InlineData("postcode 4170, 0462 and v417")]
    [InlineData("Anúncios encontrados no mês")]
    [InlineData("a restrição regional da vaga")]
    public void Ordinary_words_and_numbers_pass(string line) => Assert.DoesNotMatch(Banned(), line);

    private static List<string> TrackedFiles(string root)
    {
        var start = new ProcessStartInfo("git", ["-C", root, "ls-files", "-z"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var output = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        // A skip would hide a regression: without git the test fails.
        Assert.True(git.ExitCode == 0, $"git ls-files failed: {error}");
        return [.. output.Split('\0', StringSplitOptions.RemoveEmptyEntries)];
    }
}
