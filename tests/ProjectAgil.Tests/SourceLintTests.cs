using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Wpf.Ui.Controls;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class SourceLintTests
{
    private static readonly char[] BannedDashes = ['\u2014', '\u2013', '\u2012', '\u2015', '\u2212'];

    private static readonly string[] LintedExtensions = [".cs", ".xaml", ".md", ".iss", ".bat", ".csproj"];

    [Fact]
    public void NoSourceFileContainsAFancyDash()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            var index = text.IndexOfAny(BannedDashes);

            if (index < 0)
            {
                continue;
            }

            var line = text[..index].Count(c => c == '\n') + 1;
            offenders.Add($"{Path.GetRelativePath(RepoRoot(), file)}:{line}");
        }

        Assert.True(
            offenders.Count == 0,
            "Only the plain hyphen is allowed. Found a fancy dash in:\n" + string.Join("\n", offenders)
        );
    }

    [Fact]
    public void EverySymbolIconNameExists()
    {
        var offenders = new List<string>();

        foreach (var (file, name) in ReferencedSymbolNames())
        {
            if (!Enum.TryParse<SymbolRegular>(name, out _))
            {
                offenders.Add($"{file}: {name} is not a SymbolRegular value");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void EverySymbolIconRendersInsideTheBasicPlane()
    {
        var offenders = new List<string>();

        foreach (var (file, name) in ReferencedSymbolNames())
        {
            if (!Enum.TryParse<SymbolRegular>(name, out var symbol))
            {
                continue;
            }

            var codePoint = (int)symbol;

            if (codePoint > 0xFFFF)
            {
                offenders.Add($"{file}: {name} is U+{codePoint:X} and renders as a random letter");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void NoCSharpFileCarriesLeftoverComments()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles().Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].TrimStart();

                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("/*", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(RepoRoot(), file)}:{index + 1}  {trimmed}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Comments are not allowed in code:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<(string File, string Name)> ReferencedSymbolNames()
    {
        var patterns = new[]
        {
            new Regex(@"SymbolIcon\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled),
            new Regex(@"Symbol\s*=\s*""([A-Za-z0-9_]+)""", RegexOptions.Compiled),
            new Regex(@"SymbolRegular\.([A-Za-z0-9_]+)", RegexOptions.Compiled),
        };

        foreach (var file in SourceFiles())
        {
            if (!file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                && !file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepoRoot(), file);

            foreach (var pattern in patterns)
            {
                foreach (Match match in pattern.Matches(text))
                {
                    yield return (relative, match.Groups[1].Value);
                }
            }
        }
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = RepoRoot();

        foreach (var directory in new[] { "src", "build", "tests" })
        {
            var path = Path.Combine(root, directory);

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                if (LintedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }
    }

    private static string RepoRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
