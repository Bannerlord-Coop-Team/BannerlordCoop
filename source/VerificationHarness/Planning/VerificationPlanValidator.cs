using System.Text.Json;
using VerificationHarness.Serialization;

namespace VerificationHarness.Planning;

public interface IVerificationPlanValidator
{
    VerificationPlanReceipt Validate(
        string json,
        string expectedHead,
        string expectedTree,
        string authoritativeBase,
        IEnumerable<string?> authoritativeChangedPaths);
}

public sealed class VerificationPlanValidator : IVerificationPlanValidator
{
    private static readonly HashSet<string> HarnessOwnedExecutors = new(StringComparer.Ordinal)
    {
        "repository-dotnet-run"
    };

    private readonly ICanonicalJsonHasher hasher;

    public VerificationPlanValidator()
        : this(new CanonicalJsonHasher())
    {
    }

    public VerificationPlanValidator(ICanonicalJsonHasher hasher)
    {
        if (hasher == null) throw new ArgumentNullException(nameof(hasher));
        this.hasher = hasher;
    }

    public VerificationPlanReceipt Validate(
        string json,
        string expectedHead,
        string expectedTree,
        string authoritativeBase,
        IEnumerable<string?> authoritativeChangedPaths)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("The verification plan is empty.");
        if (authoritativeChangedPaths == null) throw new ArgumentNullException(nameof(authoritativeChangedPaths));
        var expectedSource = new VerificationSourceIdentity(expectedHead, expectedTree);
        string normalizedBase = NormalizeGitObjectId(authoritativeBase, nameof(authoritativeBase));
        string?[] changedPathSnapshot = authoritativeChangedPaths.ToArray();

        using JsonDocument suppliedDocument = Parse(json);
        JsonElement supplied = suppliedDocument.RootElement;
        if (supplied.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The verification plan root must be an object.");

        JsonElement source = RequiredProperty(supplied, "source");
        string head = RequiredString(source, "head");
        string tree = RequiredString(source, "syntheticTree");
        if (!string.Equals(head, expectedSource.Head, StringComparison.Ordinal) ||
            !string.Equals(tree, expectedSource.SyntheticTree, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The verification plan source identity does not match the expected source.");
        }

        JsonElement changedPathsElement = RequiredProperty(supplied, "changedPaths");
        if (changedPathsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The verification plan changedPaths value must be an array.");
        string[] suppliedChangedPaths = changedPathsElement.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : throw new InvalidDataException("The verification plan changedPaths entries must be strings."))
            .ToArray();

        VerificationPlan rebuilt = new VerificationPlanBuilder(hasher).Build(expectedSource, changedPathSnapshot);
        if (!rebuilt.InputValid)
            throw new InvalidDataException("The authoritative Git diff contains blocked path input.");
        if (!suppliedChangedPaths.SequenceEqual(rebuilt.ChangedPaths, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The verification plan changedPaths do not match the authoritative Git diff.");
        }

        string rebuiltJson = new VerificationPlanWriter().Serialize(rebuilt);
        using JsonDocument rebuiltDocument = Parse(rebuiltJson);
        if (!string.Equals(
                hasher.ComputeSha256(supplied),
                hasher.ComputeSha256(rebuiltDocument.RootElement),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The verification plan does not match the repository classifier output.");
        }

        VerificationProfile[] requiredProfiles = rebuilt.Profiles
            .Where(profile => profile.Required)
            .ToArray();
        string[] harnessOwned = requiredProfiles
            .Where(profile => HarnessOwnedExecutors.Contains(profile.Executor))
            .Select(profile => profile.Id)
            .ToArray();
        string[] externalRuntime = requiredProfiles
            .Where(profile => profile.Executor is "issue-to-pr-orchestrator" or "issue-to-pr-live-operator")
            .Select(profile => profile.Id)
            .ToArray();

        return new VerificationPlanReceipt
        {
            Source = expectedSource,
            AuthoritativeBase = normalizedBase,
            ChangedPathsDigest = hasher.ComputeSha256(rebuilt.ChangedPaths),
            PlanDigest = rebuilt.PlanDigest,
            RequiredProfiles = requiredProfiles.Select(profile => profile.Id).ToArray(),
            HarnessOwnedProfiles = harnessOwned,
            ExternalRuntimeProfiles = externalRuntime,
            Verdict = externalRuntime.Length == 0
                ? "validated-pending-ci"
                : "blocked-external-runtime"
        };
    }

    private static string NormalizeGitObjectId(string value, string parameterName)
    {
        if (value == null) throw new ArgumentNullException(parameterName);
        if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Git object ids must contain exactly 40 hexadecimal characters.",
                parameterName);
        }

        return value.ToLowerInvariant();
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The verification plan is malformed.", ex);
        }
    }

    private static JsonElement RequiredProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            throw new InvalidDataException($"The verification plan is missing {name}.");
        return property;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        JsonElement property = RequiredProperty(element, name);
        string? value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"The verification plan {name} value is empty.");
        return value;
    }
}

public sealed class VerificationPlanReceipt
{
    public string SchemaVersion { get; set; } = "verification-plan-receipt.v1";
    public string Scope { get; set; } = "selection-and-local-harness-handoff";
    public bool IncludesTestEvidence { get; set; } = false;
    public VerificationSourceIdentity Source { get; set; } = null!;
    public string AuthoritativeBase { get; set; } = string.Empty;
    public string ChangedPathsDigest { get; set; } = string.Empty;
    public string PlanDigest { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty;
    public IReadOnlyList<string> RequiredProfiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> HarnessOwnedProfiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ExternalRuntimeProfiles { get; set; } = Array.Empty<string>();
}
