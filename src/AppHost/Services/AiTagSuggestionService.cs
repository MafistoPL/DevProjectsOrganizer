using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppHost.Persistence;

namespace AppHost.Services;

public sealed class AiTagSuggestionService
{
    private static readonly Regex TokenRegex = new(
        "[a-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<DetectedTagSuggestion> Detect(
        ProjectEntity project,
        IReadOnlyList<TagEntity> customTags)
    {
        if (customTags.Count == 0)
        {
            return [];
        }

        var context = BuildProjectContext(project);
        var output = new List<DetectedTagSuggestion>();

        foreach (var tag in customTags)
        {
            var match = TryMatchTag(tag, context);
            if (match is null)
            {
                continue;
            }

            output.Add(new DetectedTagSuggestion(
                tag.Id,
                tag.Name,
                TagSuggestionType.AssignExisting.ToString(),
                TagSuggestionSource.Ai.ToString(),
                match.Confidence,
                match.Reason,
                BuildFingerprint(project.Id, tag.Id, match.FingerprintSeed),
                DateTimeOffset.UtcNow));
        }

        return output
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.TagName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProjectContext BuildProjectContext(ProjectEntity project)
    {
        var markers = DeserializeStringList(project.MarkersJson);
        var hints = DeserializeStringList(project.TechHintsJson);

        var metadataText = string.Join(
            ' ',
            new[]
            {
                project.Name,
                project.Path,
                project.Description,
                project.Reason,
                project.ExtensionsSummary,
                string.Join(' ', markers),
                string.Join(' ', hints)
            });

        var canonicalMetadata = NormalizeForPhrase(metadataText);
        var canonicalNamePath = NormalizeForPhrase($"{project.Name} {project.Path}");
        var tokenSet = ExtractTokens(canonicalMetadata)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ProjectContext(canonicalMetadata, canonicalNamePath, tokenSet);
    }

    private static TagMatch? TryMatchTag(TagEntity tag, ProjectContext context)
    {
        var canonicalTag = NormalizeForPhrase(tag.Name);
        if (string.IsNullOrWhiteSpace(canonicalTag))
        {
            return null;
        }

        var tokens = ExtractTokens(canonicalTag)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0)
        {
            return null;
        }

        if (ContainsPhrase(context.CanonicalNamePath, canonicalTag))
        {
            return new TagMatch(
                0.93,
                $"ai:name-path phrase `{canonicalTag}`",
                $"name-path:{canonicalTag}");
        }

        if (ContainsPhrase(context.CanonicalMetadata, canonicalTag))
        {
            return new TagMatch(
                0.88,
                $"ai:metadata phrase `{canonicalTag}`",
                $"metadata:{canonicalTag}");
        }

        if (tokens.Count > 1 && tokens.All(context.Tokens.Contains))
        {
            var evidence = string.Join(',', tokens);
            return new TagMatch(
                0.79,
                $"ai:all tokens matched `{evidence}`",
                $"tokens:{evidence}");
        }

        if (tokens.Count == 1)
        {
            var token = tokens[0];
            if (token.Length >= 4 && context.Tokens.Contains(token))
            {
                return new TagMatch(
                    0.74,
                    $"ai:token matched `{token}`",
                    $"token:{token}");
            }
        }

        return null;
    }

    private static string BuildFingerprint(Guid projectId, Guid tagId, string fingerprintSeed)
    {
        using var sha = SHA256.Create();
        var payload = $"{projectId:D}::{tagId:D}::{fingerprintSeed}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var haystack = $" {text} ";
        var needle = $" {phrase} ";
        return haystack.Contains(needle, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ExtractTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return TokenRegex
            .Matches(value)
            .Select(match => match.Value);
    }

    private static string NormalizeForPhrase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
                continue;
            }

            if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            if (parsed is null)
            {
                return [];
            }

            return parsed
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private sealed record ProjectContext(
        string CanonicalMetadata,
        string CanonicalNamePath,
        HashSet<string> Tokens);

    private sealed record TagMatch(
        double Confidence,
        string Reason,
        string FingerprintSeed);
}
