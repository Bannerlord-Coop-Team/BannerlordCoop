using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Common.LiveTesting;
using VerificationHarness.Serialization;

namespace VerificationHarness.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticArtifactManifest
{
    public const string CurrentSchemaVersion = "dedicated-server-synthetic-artifacts.v1";

    public string SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DedicatedServerSyntheticSourceIdentity CoopSource { get; set; } = new();
    public DedicatedServerSyntheticSourceIdentity DedicatedServerSource { get; set; } = new();
    public string BuildVersion { get; set; } = string.Empty;
    public DedicatedServerSyntheticExecutableArtifact ServerExecutable { get; set; } = new();
    public SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> DedicatedServerAssemblies { get; set; } =
        new(StringComparer.Ordinal);
    public SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> LoadedAssemblies { get; set; } =
        new(StringComparer.Ordinal);
    public string ArtifactSetDigest { get; set; } = string.Empty;
    public string ManifestDigest { get; set; } = string.Empty;
}

public sealed class DedicatedServerSyntheticSourceIdentity
{
    public string Head { get; set; } = string.Empty;
    public string Tree { get; set; } = string.Empty;
}

public sealed class DedicatedServerSyntheticExecutableArtifact
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class DedicatedServerSyntheticAssemblyArtifact
{
    public string RelativePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Mvid { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

public static class DedicatedServerSyntheticArtifactManifestFile
{
    internal static readonly string[] RequiredAssemblyNames =
    {
        "Common",
        "Coop",
        "Coop.Core",
        "Coop.Steam",
        "GameInterface",
        "Missions"
    };

    public static DedicatedServerSyntheticArtifactManifest LoadAndVerify(
        string path,
        string expectedCoopHead,
        string expectedCoopTree,
        string expectedDedicatedServerHead,
        string expectedDedicatedServerTree,
        string expectedManifestFileSha256)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidDataException("The dedicated-server synthetic artifact manifest does not exist.");
        byte[] manifestBytes = File.ReadAllBytes(path);
        string manifestFileSha256 =
            Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        if (!IsSha256(expectedManifestFileSha256) ||
            !string.Equals(manifestFileSha256, expectedManifestFileSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic artifact manifest file hash does not match the frozen receipt.");
        }

        DedicatedServerSyntheticArtifactManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<DedicatedServerSyntheticArtifactManifest>(
                manifestBytes,
                DedicatedServerSyntheticJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic artifact manifest is malformed.",
                ex);
        }

        if (manifest == null || manifest.SchemaVersion != DedicatedServerSyntheticArtifactManifest.CurrentSchemaVersion)
            throw new InvalidDataException("The dedicated-server synthetic artifact manifest schema is unsupported.");

        ValidateShape(manifest);
        if (!SourceMatches(manifest.CoopSource, expectedCoopHead, expectedCoopTree) ||
            !SourceMatches(
                manifest.DedicatedServerSource,
                expectedDedicatedServerHead,
                expectedDedicatedServerTree))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic artifact manifest source identity does not match the requested run.");
        }

        if (!string.Equals(manifest.ArtifactSetDigest, ComputeArtifactSetDigest(manifest), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic artifact-set digest is invalid.");
        }

        if (!string.Equals(manifest.ManifestDigest, ComputeManifestDigest(manifest), StringComparison.Ordinal))
            throw new InvalidDataException("The dedicated-server synthetic artifact manifest digest is invalid.");

        return manifest;
    }

    internal static void RefreshDigests(DedicatedServerSyntheticArtifactManifest manifest)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        manifest.ArtifactSetDigest = ComputeArtifactSetDigest(manifest);
        manifest.ManifestDigest = ComputeManifestDigest(manifest);
    }

    private static string ComputeArtifactSetDigest(DedicatedServerSyntheticArtifactManifest manifest)
    {
        return new CanonicalJsonHasher().ComputeSha256(new
        {
            manifest.BuildVersion,
            manifest.ServerExecutable,
            manifest.DedicatedServerAssemblies,
            manifest.LoadedAssemblies
        });
    }

    private static string ComputeManifestDigest(DedicatedServerSyntheticArtifactManifest manifest)
    {
        return new CanonicalJsonHasher().ComputeSha256(new
        {
            manifest.SchemaVersion,
            manifest.CoopSource,
            manifest.DedicatedServerSource,
            manifest.ArtifactSetDigest
        });
    }

    private static void ValidateShape(DedicatedServerSyntheticArtifactManifest manifest)
    {
        ValidateSource(manifest.CoopSource);
        ValidateSource(manifest.DedicatedServerSource);
        if (string.IsNullOrWhiteSpace(manifest.BuildVersion) ||
            Encoding.UTF8.GetByteCount(manifest.BuildVersion) > 256)
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic artifact manifest build version is invalid.");
        }

        if (manifest.ServerExecutable == null ||
            string.IsNullOrWhiteSpace(manifest.ServerExecutable.FileName) ||
            !string.Equals(
                Path.GetFileName(manifest.ServerExecutable.FileName),
                manifest.ServerExecutable.FileName,
                StringComparison.Ordinal) ||
            Encoding.UTF8.GetByteCount(manifest.ServerExecutable.FileName) > 260 ||
            !IsSafeRelativePath(manifest.ServerExecutable.RelativePath) ||
            !string.Equals(
                Path.GetFileName(manifest.ServerExecutable.RelativePath),
                manifest.ServerExecutable.FileName,
                StringComparison.Ordinal) ||
            !IsSha256(manifest.ServerExecutable.Sha256))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic server executable artifact is invalid.");
        }

        if (manifest.LoadedAssemblies == null ||
            !manifest.LoadedAssemblies.Keys.SequenceEqual(RequiredAssemblyNames, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic loaded-assembly set is invalid.");
        }

        ValidateDedicatedServerAssemblySet(manifest.DedicatedServerAssemblies);
        ValidateAssemblyArtifacts(manifest.DedicatedServerAssemblies);
        ValidateAssemblyArtifacts(manifest.LoadedAssemblies);

        if (!IsSha256(manifest.ArtifactSetDigest) || !IsSha256(manifest.ManifestDigest))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic artifact manifest contains an invalid digest.");
        }
    }

    private static void ValidateDedicatedServerAssemblySet(
        SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> assemblies)
    {
        if (assemblies == null)
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic dedicated-server assembly set is invalid.");
        }

        bool hasWindows = assemblies.ContainsKey("DedicatedServer.Windows");
        bool hasLinux = assemblies.ContainsKey("DedicatedServer.Linux");
        string starterName = hasLinux
            ? "TaleWorlds.Starter.DotNetCore.Linux"
            : "TaleWorlds.Starter.DotNetCore";
        if (assemblies.Count != 3 ||
            !assemblies.ContainsKey("DedicatedServer.Core") ||
            hasWindows == hasLinux ||
            !assemblies.ContainsKey(starterName))
        {
            throw new InvalidDataException(
                "The dedicated-server synthetic dedicated-server assembly set is invalid.");
        }
    }

    private static void ValidateAssemblyArtifacts(
        SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> assemblies)
    {
        foreach ((string name, DedicatedServerSyntheticAssemblyArtifact artifact) in assemblies)
        {
            if (artifact == null ||
                string.IsNullOrWhiteSpace(name) ||
                !IsSafeRelativePath(artifact.RelativePath) ||
                string.IsNullOrWhiteSpace(artifact.Version) ||
                Encoding.UTF8.GetByteCount(artifact.Version) > 128 ||
                !TryNormalizeGuid(artifact.Mvid, out string normalizedMvid) ||
                !string.Equals(artifact.Mvid, normalizedMvid, StringComparison.Ordinal) ||
                !IsSha256(artifact.Sha256))
            {
                throw new InvalidDataException(
                    "The dedicated-server synthetic loaded-assembly artifact is invalid.");
            }
        }
    }

    private static void ValidateSource(DedicatedServerSyntheticSourceIdentity source)
    {
        if (source == null || !IsGitObjectId(source.Head) || !IsGitObjectId(source.Tree))
            throw new InvalidDataException("The dedicated-server synthetic source identity is invalid.");
    }

    private static bool SourceMatches(
        DedicatedServerSyntheticSourceIdentity source,
        string expectedHead,
        string expectedTree)
    {
        return string.Equals(source.Head, expectedHead, StringComparison.Ordinal) &&
            string.Equals(source.Tree, expectedTree, StringComparison.Ordinal);
    }

    private static bool IsGitObjectId(string value)
    {
        return value != null &&
            value.Length == 40 &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    internal static bool TryNormalizeGuid(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!Guid.TryParseExact(value, "D", out Guid parsed)) return false;
        normalized = parsed.ToString("D");
        return true;
    }

    internal static bool IsSha256(string value)
    {
        return value != null &&
            value.Length == 64 &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    internal static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool IsSafeRelativePath(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !Path.IsPathRooted(value) &&
            !value.Contains('\\') &&
            value.Split('/').All(segment =>
                !string.IsNullOrWhiteSpace(segment) &&
                !string.Equals(segment, ".", StringComparison.Ordinal) &&
                !string.Equals(segment, "..", StringComparison.Ordinal));
    }
}

public sealed class DedicatedServerSyntheticArtifactVerification
{
    public DedicatedServerSyntheticArtifactManifest? Manifest { get; init; }
    public string ManifestFileSha256 { get; init; } = string.Empty;
    public DateTime ProcessStartedUtc { get; init; }
    public bool RuntimeArtifactsMatch { get; init; }
    public List<string> FailureCodes { get; init; } = new();

    public bool IsValid =>
        Manifest != null &&
        RuntimeArtifactsMatch &&
        FailureCodes.Count == 0;
}

public interface IDedicatedServerSyntheticArtifactVerifier
{
    Task<DedicatedServerSyntheticArtifactVerification> VerifyAsync(
        DedicatedServerSyntheticOptions options,
        CancellationToken cancellationToken);
}

public interface IDedicatedServerHostArtifactReader
{
    string GetProcessExecutablePath(int processId);
    DateTime GetProcessStartedUtc(int processId);
    string ComputeSha256(string path);
    DedicatedServerHostAssemblyIdentity ReadAssemblyIdentity(string path);
}

public sealed record DedicatedServerHostAssemblyIdentity(string Version, string Mvid);

public sealed class DedicatedServerHostArtifactReader : IDedicatedServerHostArtifactReader
{
    public string GetProcessExecutablePath(int processId)
    {
        using Process process = Process.GetProcessById(processId);
        if (process.HasExited)
            throw new InvalidDataException("The dedicated-server process has exited.");

        return process.MainModule?.FileName ??
            throw new InvalidDataException("The dedicated-server process executable is unavailable.");
    }

    public DateTime GetProcessStartedUtc(int processId)
    {
        using Process process = Process.GetProcessById(processId);
        if (process.HasExited)
            throw new InvalidDataException("The dedicated-server process has exited.");

        return process.StartTime.ToUniversalTime();
    }

    public string ComputeSha256(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidDataException("A required dedicated-server runtime artifact is missing.");

        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public DedicatedServerHostAssemblyIdentity ReadAssemblyIdentity(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidDataException("A required dedicated-server runtime assembly is missing.");

        string version = AssemblyName.GetAssemblyName(path).Version?.ToString() ??
            throw new InvalidDataException("A required dedicated-server runtime assembly has no version.");
        using FileStream stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        MetadataReader metadata = peReader.GetMetadataReader();
        Guid mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
        return new DedicatedServerHostAssemblyIdentity(version, mvid.ToString("D"));
    }
}

public sealed class DedicatedServerSyntheticArtifactVerifier : IDedicatedServerSyntheticArtifactVerifier
{
    private readonly IDedicatedServerControlClient controlClient;
    private readonly IDedicatedServerHostArtifactReader hostArtifactReader;
    private readonly ICanonicalJsonHasher hasher;

    public DedicatedServerSyntheticArtifactVerifier(IDedicatedServerControlClient controlClient)
        : this(controlClient, new DedicatedServerHostArtifactReader(), new CanonicalJsonHasher())
    {
    }

    public DedicatedServerSyntheticArtifactVerifier(
        IDedicatedServerControlClient controlClient,
        IDedicatedServerHostArtifactReader hostArtifactReader,
        ICanonicalJsonHasher hasher)
    {
        if (controlClient == null) throw new ArgumentNullException(nameof(controlClient));
        if (hostArtifactReader == null) throw new ArgumentNullException(nameof(hostArtifactReader));
        if (hasher == null) throw new ArgumentNullException(nameof(hasher));
        this.controlClient = controlClient;
        this.hostArtifactReader = hostArtifactReader;
        this.hasher = hasher;
    }

    public async Task<DedicatedServerSyntheticArtifactVerification> VerifyAsync(
        DedicatedServerSyntheticOptions options,
        CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        DedicatedServerSyntheticArtifactManifest manifest;
        try
        {
            manifest = DedicatedServerSyntheticArtifactManifestFile.LoadAndVerify(
                options.ArtifactManifestPath,
                options.Head,
                options.Tree,
                options.ServerHead,
                options.ServerTree,
                options.ArtifactManifestSha256);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return Failed("artifact-manifest-invalid");
        }

        try
        {
            string requestId = hasher.ComputeSha256(new
            {
                purpose = "dedicated-server-artifact-verification",
                options.RequestId,
                manifest.ManifestDigest
            });
            string responseJson = await controlClient.GetStatusAsync(
                options.ServerProcessId,
                requestId,
                TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),
                cancellationToken);
            return VerifyStatusAndHostArtifacts(responseJson, requestId, options, manifest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Failed("runtime-artifact-verification-failed", manifest);
        }
    }

    private DedicatedServerSyntheticArtifactVerification VerifyStatusAndHostArtifacts(
        string responseJson,
        string requestId,
        DedicatedServerSyntheticOptions options,
        DedicatedServerSyntheticArtifactManifest manifest)
    {
        if (!LiveTestProtocol.TryDeserializeResponse(responseJson, out LiveTestResponse response, out _) ||
            !response.Ok ||
            !string.Equals(response.Id, requestId, StringComparison.Ordinal))
        {
            return Failed("artifact-control-envelope-invalid", manifest);
        }

        if (response.Process.Pid != options.ServerProcessId ||
            !string.Equals(response.Process.Role, "server", StringComparison.Ordinal) ||
            !string.Equals(response.Process.RunToken, options.RunToken, StringComparison.Ordinal))
        {
            return Failed("artifact-process-identity-mismatch", manifest);
        }

        JsonElement result = response.Result is JsonElement element
            ? element
            : JsonSerializer.SerializeToElement(response.Result);
        if (result.ValueKind != JsonValueKind.Object)
            return Failed("artifact-status-result-invalid", manifest);

        if (!result.TryGetProperty("processStartedUtc", out JsonElement processStartedElement) ||
            !processStartedElement.TryGetDateTime(out DateTime reportedProcessStartedUtc) ||
            reportedProcessStartedUtc.Kind != DateTimeKind.Utc ||
            reportedProcessStartedUtc != hostArtifactReader.GetProcessStartedUtc(options.ServerProcessId))
        {
            return Failed("artifact-process-start-identity-mismatch", manifest);
        }
        DateTime processStartedUtc = reportedProcessStartedUtc;

        if (!TryReadObservedAssemblies(result, "loadedAssemblies", out Dictionary<string, ObservedAssembly> observed))
        {
            return Failed("artifact-loaded-assembly-set-mismatch", manifest);
        }

        if (!observed.Keys.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(
                DedicatedServerSyntheticArtifactManifestFile.RequiredAssemblyNames,
                StringComparer.Ordinal))
        {
            return Failed("artifact-loaded-assembly-set-mismatch", manifest);
        }

        if (!VerifyAssemblyArtifacts(
                manifest.LoadedAssemblies,
                observed,
                options.ArtifactRootPath,
                out string loadedAssemblyFailureCode))
        {
            return Failed(loadedAssemblyFailureCode, manifest);
        }

        if (!TryReadBoundedString(result, "buildVersion", 256, out string buildVersion) ||
            !string.Equals(buildVersion, manifest.BuildVersion, StringComparison.Ordinal))
        {
            return Failed("artifact-build-version-mismatch", manifest);
        }

        if (!TryReadBoundedString(result, "assemblyMvid", 128, out string coopMvid) ||
            !DedicatedServerSyntheticArtifactManifestFile.TryNormalizeGuid(coopMvid, out string normalizedCoopMvid) ||
            !string.Equals(
                normalizedCoopMvid,
                manifest.LoadedAssemblies["Coop"].Mvid,
                StringComparison.Ordinal))
        {
            return Failed("artifact-coop-mvid-mismatch", manifest);
        }

        if (!TryReadObservedAssemblies(
                result,
                "dedicatedServerAssemblies",
                out Dictionary<string, ObservedAssembly> observedDedicatedServerAssemblies) ||
            !observedDedicatedServerAssemblies.Keys
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(manifest.DedicatedServerAssemblies.Keys, StringComparer.Ordinal))
        {
            return Failed("artifact-dedicated-server-assembly-set-mismatch", manifest);
        }

        if (!VerifyAssemblyArtifacts(
                manifest.DedicatedServerAssemblies,
                observedDedicatedServerAssemblies,
                options.ArtifactRootPath,
                out string dedicatedServerAssemblyFailureCode))
        {
            return Failed(dedicatedServerAssemblyFailureCode, manifest);
        }

        string executablePath = hostArtifactReader.GetProcessExecutablePath(options.ServerProcessId);
        string expectedExecutablePath = ResolveStagedPath(
            options.ArtifactRootPath,
            manifest.ServerExecutable.RelativePath);
        if (!PathsEqual(executablePath, expectedExecutablePath) ||
            !string.Equals(
                Path.GetFileName(executablePath),
                manifest.ServerExecutable.FileName,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                hostArtifactReader.ComputeSha256(executablePath),
                manifest.ServerExecutable.Sha256,
                StringComparison.Ordinal))
        {
            return Failed("artifact-server-executable-mismatch", manifest);
        }

        return new DedicatedServerSyntheticArtifactVerification
        {
            Manifest = manifest,
            ManifestFileSha256 = options.ArtifactManifestSha256,
            ProcessStartedUtc = processStartedUtc,
            RuntimeArtifactsMatch = true
        };
    }

    private bool VerifyAssemblyArtifacts(
        SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> expectedAssemblies,
        IReadOnlyDictionary<string, ObservedAssembly> observedAssemblies,
        string artifactRootPath,
        out string failureCode)
    {
        foreach ((string name, DedicatedServerSyntheticAssemblyArtifact expected) in expectedAssemblies)
        {
            ObservedAssembly actual = observedAssemblies[name];
            string expectedPath = ResolveStagedPath(artifactRootPath, expected.RelativePath);
            if (!PathsEqual(actual.Location, expectedPath))
            {
                failureCode = "artifact-loaded-assembly-path-mismatch";
                return false;
            }

            DedicatedServerHostAssemblyIdentity diskIdentity =
                hostArtifactReader.ReadAssemblyIdentity(actual.Location);
            if (!string.Equals(actual.Version, expected.Version, StringComparison.Ordinal) ||
                !string.Equals(diskIdentity.Version, expected.Version, StringComparison.Ordinal) ||
                !string.Equals(actual.Mvid, expected.Mvid, StringComparison.Ordinal))
            {
                failureCode = "artifact-loaded-assembly-metadata-mismatch";
                return false;
            }
            if (!string.Equals(diskIdentity.Mvid, expected.Mvid, StringComparison.Ordinal))
            {
                failureCode = "artifact-loaded-assembly-disk-mvid-mismatch";
                return false;
            }

            string actualHash = hostArtifactReader.ComputeSha256(actual.Location);
            if (!string.Equals(actualHash, expected.Sha256, StringComparison.Ordinal))
            {
                failureCode = "artifact-loaded-assembly-hash-mismatch";
                return false;
            }
        }

        failureCode = string.Empty;
        return true;
    }

    private static string ResolveStagedPath(string artifactRootPath, string relativePath)
    {
        string root = Path.GetFullPath(artifactRootPath);
        string resolved = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, PathComparison))
            throw new InvalidDataException("A dedicated-server artifact path escapes the staged root.");

        return resolved;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static bool TryReadObservedAssemblies(
        JsonElement result,
        string propertyName,
        out Dictionary<string, ObservedAssembly> observed)
    {
        observed = new Dictionary<string, ObservedAssembly>(StringComparer.Ordinal);
        if (!result.TryGetProperty(propertyName, out JsonElement assemblyElements) ||
            assemblyElements.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement item in assemblyElements.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryReadBoundedString(item, "name", 128, out string name) ||
                !TryReadBoundedString(item, "version", 128, out string version) ||
                !TryReadBoundedString(item, "mvid", 128, out string mvid) ||
                !DedicatedServerSyntheticArtifactManifestFile.TryNormalizeGuid(mvid, out string normalizedMvid) ||
                !TryReadBoundedString(item, "location", 32768, out string location) ||
                !observed.TryAdd(name, new ObservedAssembly(version, normalizedMvid, location)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadBoundedString(
        JsonElement parent,
        string propertyName,
        int maximumBytes,
        out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? candidate = element.GetString();
        if (string.IsNullOrWhiteSpace(candidate) || Encoding.UTF8.GetByteCount(candidate) > maximumBytes)
            return false;

        value = candidate;
        return true;
    }

    private static DedicatedServerSyntheticArtifactVerification Failed(
        string failureCode,
        DedicatedServerSyntheticArtifactManifest? manifest = null)
    {
        return new DedicatedServerSyntheticArtifactVerification
        {
            Manifest = manifest,
            FailureCodes = new List<string> { failureCode }
        };
    }

    private sealed record ObservedAssembly(string Version, string Mvid, string Location);
}
