using Common.LiveTesting;
using System.Text.Json;
using VerificationHarness.DedicatedServerSynthetic;
using VerificationHarness.Serialization;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerSyntheticArtifactManifestTests
{
    private static readonly string ArtifactRoot = Path.Combine(
        Path.GetTempPath(),
        "dedicated-server-synthetic-test-stage");

    [Fact]
    public void LoadAndVerify_RejectsSourceRelabeling()
    {
        string path = WriteManifest(CreateManifest());
        try
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                DedicatedServerSyntheticArtifactManifestFile.LoadAndVerify(
                    path,
                    new string('f', 40),
                    new string('b', 40),
                    new string('c', 40),
                    new string('d', 40),
                    DedicatedServerSyntheticArtifactManifestFile.Sha256File(path)));

            Assert.Contains("source identity", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAndVerify_RejectsContentTampering()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            string frozenManifestSha256 =
                DedicatedServerSyntheticArtifactManifestFile.Sha256File(path);
            string json = File.ReadAllText(path).Replace(
                manifest.LoadedAssemblies["Coop"].Sha256,
                new string('f', 64),
                StringComparison.Ordinal);
            File.WriteAllText(path, json);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                DedicatedServerSyntheticArtifactManifestFile.LoadAndVerify(
                    path,
                    new string('a', 40),
                    new string('b', 40),
                    new string('c', 40),
                    new string('d', 40),
                    frozenManifestSha256));

            Assert.Contains("file hash", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_ReportsManifestTamperingBeforeRuntimeVerification()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            File.AppendAllText(path, " ");
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                new StubHostArtifactReader(manifest),
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Equal(new[] { "artifact-manifest-invalid" }, result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_BindsProcessAndEveryLoadedArtifact()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var reader = new StubHostArtifactReader(manifest);
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(manifest.ManifestDigest, result.Manifest?.ManifestDigest);
            Assert.Equal(
                manifest.LoadedAssemblies.Count + manifest.DedicatedServerAssemblies.Count + 1,
                reader.HashedPaths.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadAndVerify_AcceptsTheLinuxStarterIdentity()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest(
            "DedicatedServer.Linux",
            "TaleWorlds.Starter.DotNetCore.Linux");
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticArtifactManifest loaded =
                DedicatedServerSyntheticArtifactManifestFile.LoadAndVerify(
                    path,
                    new string('a', 40),
                    new string('b', 40),
                    new string('c', 40),
                    new string('d', 40),
                    DedicatedServerSyntheticArtifactManifestFile.Sha256File(path));

            Assert.True(loaded.DedicatedServerAssemblies.ContainsKey(
                "TaleWorlds.Starter.DotNetCore.Linux"));
            Assert.Equal(
                DedicatedServerSyntheticExecutableArtifact.SystemDotnetKind,
                loaded.ServerExecutable.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_AcceptsLinuxSystemDotnetAndHashesTheStagedStarter()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest(
            "DedicatedServer.Linux",
            "TaleWorlds.Starter.DotNetCore.Linux");
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var reader = new StubHostArtifactReader(manifest);
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Contains(
                StagedPath(manifest.DedicatedServerAssemblies["TaleWorlds.Starter.DotNetCore.Linux"].RelativePath),
                reader.HashedPaths);
            Assert.DoesNotContain(reader.ProcessExecutablePath, reader.HashedPaths);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_RejectsLoadedAssemblyHashMismatch()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var reader = new StubHostArtifactReader(manifest);
            reader.HashOverrides[StagedPath("runtime/Coop.dll")] = new string('0', 64);
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-loaded-assembly-hash-mismatch", result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_HashesTheProcessReportedLoadedAssemblyPath()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            DedicatedServerSyntheticAssemblyArtifact coop = manifest.LoadedAssemblies["Coop"];
            string expectedPath = StagedPath(coop.RelativePath);
            string observedPath = Path.Combine(
                Path.GetDirectoryName(expectedPath)!,
                ".",
                Path.GetFileName(expectedPath));
            var reader = new StubHostArtifactReader(manifest);
            reader.RegisterObservedPath(observedPath, coop, new string('0', 64));
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(
                    manifest,
                    mutateLocation: (name, location) => name == "Coop" ? observedPath : location),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-loaded-assembly-hash-mismatch", result.FailureCodes);
            Assert.Contains(observedPath, reader.HashedPaths);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_RejectsCaseMismatchedLoadedAssemblyPathOnCaseSensitiveHost()
    {
        if (OperatingSystem.IsWindows()) return;

        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            string mismatchedPath = StagedPath(
                manifest.LoadedAssemblies["Coop"].RelativePath.Replace("Coop.dll", "coop.dll"));
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(
                    manifest,
                    mutateLocation: (name, location) => name == "Coop" ? mismatchedPath : location),
                new StubHostArtifactReader(manifest),
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-loaded-assembly-path-mismatch", result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("mvid")]
    public async Task VerifyAsync_RejectsLoadedAssemblyMetadataMismatch(string field)
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest, (name, version, mvid) =>
                    name == "DedicatedServer.Core"
                        ? field == "version"
                            ? ("0.0.0.0", mvid)
                            : (version, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
                        : (version, mvid)),
                new StubHostArtifactReader(manifest),
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-loaded-assembly-metadata-mismatch", result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_RejectsGenericHostExecutableMismatch()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var reader = new StubHostArtifactReader(manifest)
            {
                ProcessExecutablePath = "other-dotnet.exe"
            };
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-server-executable-mismatch", result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_RejectsLinuxHostThatIsNotDotnet()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest(
            "DedicatedServer.Linux",
            "TaleWorlds.Starter.DotNetCore.Linux");
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var reader = new StubHostArtifactReader(manifest)
            {
                ProcessExecutablePath = Path.Combine(Path.GetTempPath(), "unexpected-host")
            };
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-server-executable-mismatch", result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifyAsync_RejectsDiskMvidMismatch()
    {
        DedicatedServerSyntheticArtifactManifest manifest = CreateManifest();
        string path = WriteManifest(manifest);
        try
        {
            DedicatedServerSyntheticOptions options = CreateOptions(path);
            var reader = new StubHostArtifactReader(manifest);
            string coopPath = StagedPath(manifest.LoadedAssemblies["Coop"].RelativePath);
            reader.IdentityOverrides[coopPath] = new DedicatedServerHostAssemblyIdentity(
                manifest.LoadedAssemblies["Coop"].Version,
                "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var verifier = new DedicatedServerSyntheticArtifactVerifier(
                new StatusControlClient(manifest),
                reader,
                new CanonicalJsonHasher());

            DedicatedServerSyntheticArtifactVerification result = await verifier.VerifyAsync(
                options,
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("artifact-loaded-assembly-disk-mvid-mismatch", result.FailureCodes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DedicatedServerSyntheticArtifactManifest CreateManifest(
        string platformAssemblyName = "DedicatedServer.Windows",
        string starterAssemblyName = "TaleWorlds.Starter.DotNetCore")
    {
        bool isLinux = platformAssemblyName == "DedicatedServer.Linux";
        var manifest = new DedicatedServerSyntheticArtifactManifest
        {
            CoopSource = new DedicatedServerSyntheticSourceIdentity
            {
                Head = new string('a', 40),
                Tree = new string('b', 40)
            },
            DedicatedServerSource = new DedicatedServerSyntheticSourceIdentity
            {
                Head = new string('c', 40),
                Tree = new string('d', 40)
            },
            BuildVersion = "1.2.3+source-bound",
            ServerExecutable = new DedicatedServerSyntheticExecutableArtifact
            {
                Kind = isLinux
                    ? DedicatedServerSyntheticExecutableArtifact.SystemDotnetKind
                    : DedicatedServerSyntheticExecutableArtifact.StagedExecutableKind,
                FileName = isLinux ? "dotnet" : "dotnet.exe",
                RelativePath = isLinux ? string.Empty : "runtime/dotnet.exe",
                Sha256 = isLinux ? string.Empty : HashFor("runtime/dotnet.exe")
            }
        };

        AddAssembly(manifest.DedicatedServerAssemblies, "DedicatedServer.Core", 1);
        AddAssembly(manifest.DedicatedServerAssemblies, platformAssemblyName, 2);
        AddAssembly(manifest.DedicatedServerAssemblies, starterAssemblyName, 3);
        for (int index = 0; index < DedicatedServerSyntheticArtifactManifestFile.RequiredAssemblyNames.Length; index++)
        {
            AddAssembly(
                manifest.LoadedAssemblies,
                DedicatedServerSyntheticArtifactManifestFile.RequiredAssemblyNames[index],
                index + 4);
        }

        DedicatedServerSyntheticArtifactManifestFile.RefreshDigests(manifest);
        return manifest;
    }

    private static void AddAssembly(
        SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> assemblies,
        string name,
        int identity)
    {
        assemblies[name] = new DedicatedServerSyntheticAssemblyArtifact
        {
            RelativePath = "runtime/" + name + ".dll",
            Version = $"1.0.0.{identity}",
            Mvid = $"00000000-0000-0000-0000-{identity:D12}",
            Sha256 = HashFor("runtime/" + name + ".dll")
        };
    }

    private static string HashFor(string path) =>
        new CanonicalJsonHasher().ComputeSha256(path);

    private static string WriteManifest(DedicatedServerSyntheticArtifactManifest manifest)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "dedicated-server-synthetic-artifacts-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, DedicatedServerSyntheticJson.Options));
        return path;
    }

    private static DedicatedServerSyntheticOptions CreateOptions(string manifestPath)
    {
        string passwordVariable = "DS_SYNTHETIC_ARTIFACT_TEST_PASSWORD_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(passwordVariable, "artifact-test-password");
        return DedicatedServerSyntheticOptions.Parse(new[]
        {
            "--head", new string('a', 40),
            "--tree", new string('b', 40),
            "--server-head", new string('c', 40),
            "--server-tree", new string('d', 40),
            "--server-pid", "1234",
            "--run-token", "run-token",
            "--request-id", "request-id",
            "--join-port", "4201",
            "--timeout-ms", "1000",
            "--seed", "17",
            "--artifact-manifest", manifestPath,
            "--artifact-manifest-sha256",
            DedicatedServerSyntheticArtifactManifestFile.Sha256File(manifestPath),
            "--artifact-root", ArtifactRoot,
            "--password-env", passwordVariable
        });
    }

    private sealed class StatusControlClient : IDedicatedServerControlClient
    {
        private readonly DedicatedServerSyntheticArtifactManifest manifest;
        private readonly Func<string, string, string, (string Version, string Mvid)> mutate;
        private readonly Func<string, string, string> mutateLocation;

        public StatusControlClient(
            DedicatedServerSyntheticArtifactManifest manifest,
            Func<string, string, string, (string Version, string Mvid)>? mutate = null,
            Func<string, string, string>? mutateLocation = null)
        {
            this.manifest = manifest;
            this.mutate = mutate ?? ((_, version, mvid) => (version, mvid));
            this.mutateLocation = mutateLocation ?? ((_, location) => location);
        }

        public Task<string> GetStatusAsync(
            int processId,
            string requestId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            object[] Assemblies(
                SortedDictionary<string, DedicatedServerSyntheticAssemblyArtifact> artifacts) =>
                artifacts.Select(pair =>
                {
                    (string version, string mvid) = mutate(
                        pair.Key,
                        pair.Value.Version,
                        pair.Value.Mvid);
                    return new
                    {
                        name = pair.Key,
                        version,
                        mvid,
                        location = mutateLocation(pair.Key, StagedPath(pair.Value.RelativePath))
                    };
                }).Cast<object>().ToArray();

            string json = LiveTestProtocol.SerializeResponse(new LiveTestResponse
            {
                Id = requestId,
                Ok = true,
                Process = new LiveTestProcessInfo
                {
                    Pid = processId,
                    Role = "server",
                    RunToken = "run-token"
                },
                Result = new
                {
                    processStartedUtc = StubHostArtifactReader.ExpectedProcessStartedUtc,
                    buildVersion = manifest.BuildVersion,
                    assemblyMvid = manifest.LoadedAssemblies["Coop"].Mvid,
                    loadedAssemblies = Assemblies(manifest.LoadedAssemblies),
                    dedicatedServerAssemblies = Assemblies(manifest.DedicatedServerAssemblies)
                }
            });
            return Task.FromResult(json);
        }
    }

    private sealed class StubHostArtifactReader : IDedicatedServerHostArtifactReader
    {
        public static readonly DateTime ExpectedProcessStartedUtc =
            new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DedicatedServerHostAssemblyIdentity> identities =
            new(StringComparer.Ordinal);

        public StubHostArtifactReader(DedicatedServerSyntheticArtifactManifest manifest)
        {
            if (manifest.ServerExecutable.Kind == DedicatedServerSyntheticExecutableArtifact.SystemDotnetKind)
            {
                ProcessExecutablePath = Path.Combine(Path.GetTempPath(), "system-dotnet", "dotnet");
            }
            else
            {
                ProcessExecutablePath = StagedPath(manifest.ServerExecutable.RelativePath);
                hashes[ProcessExecutablePath] = manifest.ServerExecutable.Sha256;
            }
            foreach ((string name, DedicatedServerSyntheticAssemblyArtifact artifact) in
                     manifest.LoadedAssemblies.Concat(manifest.DedicatedServerAssemblies))
            {
                string path = StagedPath(artifact.RelativePath);
                hashes[path] = artifact.Sha256;
                identities[path] = new DedicatedServerHostAssemblyIdentity(
                    artifact.Version,
                    artifact.Mvid);
            }
        }

        public string ProcessExecutablePath { get; set; }
        public Dictionary<string, string> HashOverrides { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, DedicatedServerHostAssemblyIdentity> IdentityOverrides { get; } =
            new(StringComparer.Ordinal);
        public HashSet<string> HashedPaths { get; } = new(StringComparer.Ordinal);

        public string GetProcessExecutablePath(int processId) => ProcessExecutablePath;

        public DateTime GetProcessStartedUtc(int processId) => ExpectedProcessStartedUtc;

        public void RegisterObservedPath(
            string path,
            DedicatedServerSyntheticAssemblyArtifact artifact,
            string hash)
        {
            hashes[path] = hash;
            identities[path] = new DedicatedServerHostAssemblyIdentity(
                artifact.Version,
                artifact.Mvid);
        }

        public string ComputeSha256(string path)
        {
            HashedPaths.Add(path);
            return HashOverrides.GetValueOrDefault(path, hashes[path]);
        }

        public DedicatedServerHostAssemblyIdentity ReadAssemblyIdentity(string path) =>
            IdentityOverrides.GetValueOrDefault(path, identities[path]);
    }

    private static string StagedPath(string relativePath) => Path.GetFullPath(Path.Combine(
        ArtifactRoot,
        relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
