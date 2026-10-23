using System.Reflection;

namespace JobRadar.Web;

/// <summary>What is running: the commit this site was built from and, in production, the deploy record.</summary>
/// <param name="Build">Commit the binary was built from (from the SDK's source revision); null if unknown.</param>
/// <param name="Deployed">
/// The line <c>deploy/deploy.ps1</c> writes to <c>deployed.txt</c>, next to the site's folder: commit, a note
/// if the tree had uncommitted changes, and when. The real profiles are copied by the same script, so its
/// time also says which profile changes are live. Null outside production.
/// </param>
public sealed record Deployment(string? Build, string? Deployed)
{
    public const string FileName = "deployed.txt";

    /// <summary>
    /// Reads the record now. Not cached: the script writes it only after the services are up again, so a
    /// copy taken at start-up would still name the previous deploy.
    /// </summary>
    public static Deployment Read(string contentRoot)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(contentRoot)) ?? contentRoot;
        return new Deployment(BuildCommit(typeof(Deployment).Assembly), FirstLine(Path.Combine(parent, FileName)));
    }

    /// <summary>"1.0.0+3e9b97b…" → "3e9b97b" (short, like the deploy record).</summary>
    internal static string? BuildCommit(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var plus = version?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        if (version is null || plus < 0 || plus == version.Length - 1)
            return null;
        var commit = version[(plus + 1)..];
        return commit.Length > 7 ? commit[..7] : commit;
    }

    private static string? FirstLine(string file)
    {
        try
        {
            var line = File.Exists(file) ? File.ReadLines(file).FirstOrDefault()?.Trim() : null;
            return string.IsNullOrEmpty(line) ? null : line;
        }
        catch (IOException)
        {
            return null;   // being rewritten by the deploy script right now
        }
    }
}
