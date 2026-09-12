using System.Runtime.InteropServices;
using System.Security.Cryptography;
using VerificationHarness.Serialization;

namespace VerificationHarness.Transport;

public sealed class ProcessRuntimeIdentity
{
    public string FrameworkDescription { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string HostFileName { get; set; } = string.Empty;
    public string HostSha256 { get; set; } = string.Empty;
    public string CoreLibraryFileName { get; set; } = string.Empty;
    public string CoreLibrarySha256 { get; set; } = string.Empty;
    public string SharedRuntimeDigest { get; set; } = string.Empty;
    public string IdentityDigest { get; set; } = string.Empty;

    public static ProcessRuntimeIdentity CaptureCurrent()
    {
        string hostPath = Environment.ProcessPath ?? string.Empty;
        string coreLibraryPath = typeof(object).Assembly.Location;
        var identity = new ProcessRuntimeIdentity
        {
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            RuntimeVersion = Environment.Version.ToString(),
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            HostFileName = Path.GetFileName(hostPath),
            HostSha256 = Sha256File(hostPath),
            CoreLibraryFileName = Path.GetFileName(coreLibraryPath),
            CoreLibrarySha256 = Sha256File(coreLibraryPath),
        };
        identity.RefreshDigests();
        return identity;
    }

    internal void RefreshDigests()
    {
        SharedRuntimeDigest = ComputeSharedRuntimeDigest(this);
        IdentityDigest = ComputeIdentityDigest(this);
    }

    public bool HasValidShape()
    {
        return !string.IsNullOrWhiteSpace(FrameworkDescription) &&
            !string.IsNullOrWhiteSpace(RuntimeVersion) &&
            !string.IsNullOrWhiteSpace(RuntimeIdentifier) &&
            !string.IsNullOrWhiteSpace(OsDescription) &&
            !string.IsNullOrWhiteSpace(OsArchitecture) &&
            !string.IsNullOrWhiteSpace(ProcessArchitecture) &&
            !string.IsNullOrWhiteSpace(HostFileName) &&
            IsSha256(HostSha256) &&
            !string.IsNullOrWhiteSpace(CoreLibraryFileName) &&
            IsSha256(CoreLibrarySha256) &&
            IsSha256(SharedRuntimeDigest) &&
            IsSha256(IdentityDigest) &&
            string.Equals(SharedRuntimeDigest, ComputeSharedRuntimeDigest(this), StringComparison.Ordinal) &&
            string.Equals(IdentityDigest, ComputeIdentityDigest(this), StringComparison.Ordinal);
    }

    private static string ComputeSharedRuntimeDigest(ProcessRuntimeIdentity identity)
    {
        return new CanonicalJsonHasher().ComputeSha256(new
        {
            identity.FrameworkDescription,
            identity.RuntimeVersion,
            identity.RuntimeIdentifier,
            identity.OsDescription,
            identity.OsArchitecture,
            identity.ProcessArchitecture,
            identity.CoreLibraryFileName,
            identity.CoreLibrarySha256,
        });
    }

    private static string ComputeIdentityDigest(ProcessRuntimeIdentity identity)
    {
        return new CanonicalJsonHasher().ComputeSha256(new
        {
            identity.SharedRuntimeDigest,
            identity.HostFileName,
            identity.HostSha256,
        });
    }

    private static string Sha256File(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool IsSha256(string value)
    {
        return value != null &&
            value.Length == 64 &&
            value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
