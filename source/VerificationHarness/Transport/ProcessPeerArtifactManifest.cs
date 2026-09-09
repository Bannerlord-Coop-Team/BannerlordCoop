using System.Security.Cryptography;
using System.Text.Json;
using Common.Serialization;
using LiteNetLib;
using VerificationHarness.Serialization;

namespace VerificationHarness.Transport;

public sealed class ProcessPeerArtifactManifest
{
    public string SchemaVersion { get; set; } = "process-peer-artifacts.v2";
    public string Head { get; set; } = string.Empty;
    public string Tree { get; set; } = string.Empty;
    public SortedDictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);
    public string ArtifactSetDigest { get; set; } = string.Empty;
    public ProcessRuntimeIdentity RuntimeIdentity { get; set; } = new();
    public string ManifestDigest { get; set; } = string.Empty;
}

public static class ProcessPeerArtifactManifestFile
{
    public static async Task<ProcessPeerArtifactManifest> CreateCurrentAsync(
        string head,
        string tree,
        string outputPath)
    {
        ValidateGitIdentity(head, nameof(head));
        ValidateGitIdentity(tree, nameof(tree));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("An artifact-manifest output path is required.", nameof(outputPath));

        var manifest = new ProcessPeerArtifactManifest
        {
            Head = head.ToLowerInvariant(),
            Tree = tree.ToLowerInvariant(),
            Artifacts = CurrentArtifactHashes(),
            RuntimeIdentity = ProcessRuntimeIdentity.CaptureCurrent(),
        };
        RefreshDigests(manifest);
        string json = JsonSerializer.Serialize(manifest, TransportJson.Options);
        await TransportEvidenceFileWriter.WriteAtomicallyAsync(outputPath, json);
        return manifest;
    }

    public static ProcessPeerArtifactManifest LoadAndVerify(
        string path,
        string expectedHead,
        string expectedTree)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidDataException("The process-peer artifact manifest does not exist.");

        ProcessPeerArtifactManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ProcessPeerArtifactManifest>(
                File.ReadAllText(path),
                TransportJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The process-peer artifact manifest is malformed.", ex);
        }

        if (manifest == null || manifest.SchemaVersion != "process-peer-artifacts.v2")
            throw new InvalidDataException("The process-peer artifact manifest schema is unsupported.");
        ValidateManifestShape(manifest);
        ProcessRuntimeIdentity currentRuntime = ProcessRuntimeIdentity.CaptureCurrent();
        if (!string.Equals(
                manifest.RuntimeIdentity.IdentityDigest,
                currentRuntime.IdentityDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The process-peer runtime identity does not match the artifact manifest.");
        }
        if (!string.Equals(manifest.Head, expectedHead, StringComparison.Ordinal) ||
            !string.Equals(manifest.Tree, expectedTree, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The process-peer artifact manifest source identity does not match the requested run.");
        }

        if (!string.Equals(manifest.ManifestDigest, ComputeDigest(manifest), StringComparison.Ordinal))
            throw new InvalidDataException("The process-peer artifact manifest digest is invalid.");

        SortedDictionary<string, string> current = CurrentArtifactHashes();
        string currentArtifactSetDigest = ComputeArtifactSetDigest(current);
        if (!string.Equals(manifest.ArtifactSetDigest, ComputeArtifactSetDigest(manifest.Artifacts), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The process-peer artifact manifest artifact-set digest is invalid.");
        }

        string? missing = manifest.Artifacts.Keys.Except(current.Keys, StringComparer.Ordinal).FirstOrDefault();
        string? unexpected = current.Keys.Except(manifest.Artifacts.Keys, StringComparer.Ordinal).FirstOrDefault();
        if (missing != null || unexpected != null)
        {
            throw new InvalidDataException(
                $"The process-peer runtime artifact set differs (missing={missing ?? "none"}, unexpected={unexpected ?? "none"}).");
        }

        if (!string.Equals(manifest.ArtifactSetDigest, currentArtifactSetDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The process-peer runtime artifact-set digest does not match the manifest.");
        }

        foreach (string name in current.Keys)
        {
            if (!string.Equals(manifest.Artifacts[name], current[name], StringComparison.Ordinal))
                throw new InvalidDataException($"The loaded {name} does not match the process-peer artifact manifest.");
        }

        return manifest;
    }

    internal static string CurrentArtifactSetDigest() =>
        ComputeArtifactSetDigest(CurrentArtifactHashes());

    internal static void RefreshDigests(ProcessPeerArtifactManifest manifest)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        manifest.ArtifactSetDigest = ComputeArtifactSetDigest(manifest.Artifacts);
        manifest.ManifestDigest = ComputeDigest(manifest);
    }

    internal static SortedDictionary<string, string> HashRuntimeArtifacts(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
            throw new InvalidDataException("The process-peer runtime directory does not exist.");

        var artifacts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string root = Path.GetFullPath(baseDirectory);
        foreach (string path in Directory
                     .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(IsRuntimeArtifact)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            artifacts["runtime/" + relativePath] = Sha256File(path);
        }

        if (artifacts.Count == 0)
            throw new InvalidDataException("The process-peer runtime directory contains no loadable artifacts.");

        return artifacts;
    }

    private static SortedDictionary<string, string> CurrentArtifactHashes() =>
        HashRuntimeArtifacts(AppContext.BaseDirectory);

    private static bool IsRuntimeArtifact(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);

    private static string ComputeArtifactSetDigest(SortedDictionary<string, string> artifacts) =>
        new CanonicalJsonHasher().ComputeSha256(artifacts);

    private static string ComputeDigest(ProcessPeerArtifactManifest manifest)
    {
        return new CanonicalJsonHasher().ComputeSha256(new
        {
            manifest.SchemaVersion,
            manifest.Head,
            manifest.Tree,
            manifest.Artifacts,
            manifest.ArtifactSetDigest,
            manifest.RuntimeIdentity
        });
    }

    private static string Sha256File(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidDataException($"Required process-peer artifact is missing: {Path.GetFileName(path)}.");

        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ValidateManifestShape(ProcessPeerArtifactManifest manifest)
    {
        if (manifest.Artifacts == null || manifest.Artifacts.Count == 0)
            throw new InvalidDataException("The process-peer artifact manifest has no runtime artifacts.");
        if (manifest.RuntimeIdentity == null || !manifest.RuntimeIdentity.HasValidShape())
            throw new InvalidDataException("The process-peer artifact manifest has an invalid runtime identity.");
        if (!IsSha256(manifest.ArtifactSetDigest) || !IsSha256(manifest.ManifestDigest))
            throw new InvalidDataException("The process-peer artifact manifest contains an invalid digest.");

        foreach ((string name, string hash) in manifest.Artifacts)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                !name.StartsWith("runtime/", StringComparison.Ordinal) ||
                !IsSha256(hash))
            {
                throw new InvalidDataException("The process-peer artifact manifest contains an invalid artifact entry.");
            }
        }
    }

    private static bool IsSha256(string value)
    {
        return value != null &&
            value.Length == 64 &&
            value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static void ValidateGitIdentity(string value, string name)
    {
        if (value == null || value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Git object ids must contain exactly 40 hexadecimal characters.", name);
    }
}
