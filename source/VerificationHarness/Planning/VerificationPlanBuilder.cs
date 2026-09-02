using VerificationHarness.Serialization;

namespace VerificationHarness.Planning;

public interface IVerificationPlanBuilder
{
    VerificationPlan Build(VerificationSourceIdentity source, IEnumerable<string?> changedPaths);
}

public sealed class VerificationPlanBuilder : IVerificationPlanBuilder
{
    private static readonly HashSet<string> KnownRuntimeReflectionFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "source/Coop.Core/Server/Services/Players/Handlers/PlayerPartyVisibilityHandler.cs"
    };

    private static readonly HashSet<string> ProductionTransportRuntimeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "source/Coop.Core/Client/ClientModule.cs",
        "source/Coop.Core/Client/CoopClient.cs",
        "source/Coop.Core/Common/Configuration/NetworkConfig.cs",
        "source/Coop.Core/Common/Configuration/SessionAdvertisementConfig.cs",
        "source/Coop.Core/CoopartiveMultiplayerExperience.cs"
    };

    private static readonly ProfileDefinition[] ProfileDefinitions =
    {
        new(
            VerificationTier.Unit,
            "repository-dotnet-test",
            "dotnet-test",
            new[] { "source/CoopUnitTests.slnf", "-c", "Release" },
            "Fast unit and in-process integration tests."),
        new(
            VerificationTier.DeterministicPeer,
            "repository-script",
            "run-e2e",
            new[] { ".github/scripts/run-e2e-local-docker.sh" },
            "Deterministic serialized peer simulation."),
        new(
            VerificationTier.ProcessPeer,
            "repository-dotnet-run",
            "process-peer-suite",
            new[]
            {
                "source/VerificationHarness/VerificationHarness.csproj",
                "--head",
                "{source.head}",
                "--tree",
                "{source.syntheticTree}",
                "--seed",
                "{seed}",
                "--artifact-manifest",
                "{artifact.manifest}"
            },
            "Synthetic transport lab over real loopback UDP with one LiteNetLib server process, two isolated client processes, and Common serialization; this does not claim Bannerlord handlers, campaign state, native code, rendering, or save loading."),
        new(
            VerificationTier.DedicatedServerSynthetic,
            "issue-to-pr-orchestrator",
            "dedicated-server-synthetic",
            new[]
            {
                "--head", "{source.head}",
                "--tree", "{source.syntheticTree}",
                "--seed", "{seed}",
                "--server-head", "{dedicatedServer.head}",
                "--server-tree", "{dedicatedServer.tree}",
                "--server-pid", "{dedicatedServer.pid}",
                "--run-token", "{run.token}",
                "--request-id", "{request.id}",
                "--join-port", "{dedicatedServer.joinPort}",
                "--output", "{evidence.output}"
            },
            "Standalone dedicated server with synthetic clients."),
        new(
            VerificationTier.RenderedSmoke,
            "issue-to-pr-orchestrator",
            "rendered-smoke",
            new[]
            {
                "--head", "{source.head}",
                "--tree", "{source.syntheticTree}",
                "--seed", "{seed}",
                "--evidence-profile", "{evidence.profile}",
                "--repro-contract", "{repro.contract}",
                "--output", "{evidence.output}"
            },
            "Rendered startup and screenshot assertions."),
        new(
            VerificationTier.FullLive,
            "issue-to-pr-live-operator",
            "full-live",
            new[]
            {
                "--head", "{source.head}",
                "--tree", "{source.syntheticTree}",
                "--seed", "{seed}",
                "--evidence-profile", "{evidence.profile}",
                "--repro-contract", "{repro.contract}",
                "--output", "{evidence.output}"
            },
            "Complete Windows Bannerlord live verification.")
    };

    private static readonly CheckDefinition[] CheckDefinitions =
    {
        new("unit", VerificationTier.Unit, new VerificationTopology(0, 0, 1, false)),
        new("wire-copy-e2e", VerificationTier.DeterministicPeer, new VerificationTopology(1, 2, 1, false)),
        new("poller-game-thread", VerificationTier.DeterministicPeer, new VerificationTopology(1, 2, 1, false)),
        new("deterministic-peer", VerificationTier.DeterministicPeer, new VerificationTopology(1, 2, 1, false)),
        new("process-peer", VerificationTier.ProcessPeer, new VerificationTopology(1, 2, 3, true)),
        new("dedicated-server-synthetic", VerificationTier.DedicatedServerSynthetic, new VerificationTopology(1, 2, 3, true)),
        new("rendered-smoke", VerificationTier.RenderedSmoke, new VerificationTopology(1, 2, 3, true)),
        new("full-live", VerificationTier.FullLive, new VerificationTopology(1, 2, 3, true))
    };

    private static readonly ClassificationRule[] ClassificationRules =
    {
        new(
            "steam-integration",
            VerificationTier.FullLive,
            "Steam integration requires the complete live profile.",
            IsSteamPath),
        new(
            "native-boundary",
            VerificationTier.FullLive,
            "Native boundaries and native artifacts require the complete live profile.",
            IsNativePath),
        new(
            "ui-or-rendering",
            VerificationTier.FullLive,
            "UI, scene, and rendering changes require rendered smoke and the complete live profile.",
            IsUiOrRenderingPath),
        new(
            "reflection-or-runtime-patching",
            VerificationTier.FullLive,
            "Reflection, publicizing, weaving, and runtime patches require the complete live profile.",
            IsReflectionOrPatchingPath),
        new(
            "game-runtime",
            VerificationTier.FullLive,
            "Game-facing module code requires the complete live profile.",
            IsGameRuntimePath),
        new(
            "deployment-or-ci",
            VerificationTier.FullLive,
            "Deployment and CI control-plane changes require the complete live profile.",
            IsDeploymentOrCiPath),
        new(
            "production-client-transport",
            VerificationTier.FullLive,
            "Production client transport and bootstrap changes require the complete live profile.",
            path => ProductionTransportRuntimeFiles.Contains(path)),
        new(
            "process-test-build-contract",
            VerificationTier.ProcessPeer,
            "Verification-harness test project contracts require the synthetic process profile.",
            IsProcessTestBuildContractPath),
        new(
            "build-contract",
            VerificationTier.FullLive,
            "Production project and dependency contracts require the complete live profile.",
            IsProductionBuildContractPath),
        new(
            "dedicated-server",
            VerificationTier.DedicatedServerSynthetic,
            "Dedicated-server, join, save, and session changes require synthetic clients.",
            IsDedicatedServerPath),
        new(
            "network-process-boundary",
            VerificationTier.ProcessPeer,
            "Transport, polling, messaging, and serialization changes require process isolation.",
            IsProcessBoundaryPath),
        new(
            "deterministic-peer",
            VerificationTier.DeterministicPeer,
            "Co-op state and deterministic peer-harness changes require deterministic peer verification.",
            IsDeterministicPeerPath),
        new(
            "shared-runtime",
            VerificationTier.FullLive,
            "Unclassified production Common runtime changes stay on the complete live profile until a narrower oracle is proven.",
            path => StartsWith(path, "source/Common/")),
        new(
            "unit",
            VerificationTier.Unit,
            "Pure managed utilities, tests, tools, and documentation require the unit profile.",
            IsUnitPath),
        new(
            "unknown-path",
            VerificationTier.FullLive,
            "Unrecognized repository paths fail closed to the complete live profile.",
            _ => true)
    };

    private readonly ICanonicalJsonHasher canonicalJsonHasher;

    public VerificationPlanBuilder()
        : this(new CanonicalJsonHasher())
    {
    }

    public VerificationPlanBuilder(ICanonicalJsonHasher canonicalJsonHasher)
    {
        if (canonicalJsonHasher == null) throw new ArgumentNullException(nameof(canonicalJsonHasher));
        this.canonicalJsonHasher = canonicalJsonHasher;
    }

    public VerificationPlan Build(VerificationSourceIdentity source, IEnumerable<string?> changedPaths)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (changedPaths == null) throw new ArgumentNullException(nameof(changedPaths));

        var normalizedPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rejectedPaths = new List<RejectedPath>();

        foreach (string? suppliedPath in changedPaths)
        {
            if (TryNormalize(suppliedPath, out string? normalizedPath, out string? rejectionReason))
            {
                if (!normalizedPathMap.TryGetValue(normalizedPath!, out string? existingPath) ||
                    StringComparer.Ordinal.Compare(normalizedPath, existingPath) < 0)
                {
                    normalizedPathMap[normalizedPath!] = normalizedPath!;
                }
            }
            else
            {
                rejectedPaths.Add(new RejectedPath(suppliedPath, rejectionReason!));
            }
        }

        rejectedPaths.Sort(CompareRejectedPaths);
        string[] normalizedPaths = normalizedPathMap.Values
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var groupedReasons = new Dictionary<ClassificationRule, List<string>>();
        VerificationTier highestTier = VerificationTier.Unit;

        foreach (string path in normalizedPaths)
        {
            ClassificationRule rule = Classify(path);
            if (!groupedReasons.TryGetValue(rule, out List<string>? paths))
            {
                paths = new List<string>();
                groupedReasons.Add(rule, paths);
            }

            paths.Add(path);
            if (rule.Tier > highestTier)
            {
                highestTier = rule.Tier;
            }
        }

        bool inputValid = normalizedPaths.Length > 0 && rejectedPaths.Count == 0;
        var reasons = groupedReasons
            .OrderByDescending(x => x.Key.Tier)
            .ThenBy(x => x.Key.Id, StringComparer.Ordinal)
            .Select(x => new VerificationReason(
                x.Key.Id,
                GetTierId(x.Key.Tier),
                x.Key.Description,
                x.Value.OrderBy(path => path, StringComparer.Ordinal).ToArray()))
            .ToList();

        if (!inputValid)
        {
            highestTier = VerificationTier.FullLive;
            string description = normalizedPaths.Length == 0 && rejectedPaths.Count == 0
                ? "No changed repository paths were supplied."
                : "One or more repository paths were empty, rooted, traversed outside the repository, or otherwise malformed.";
            reasons.Insert(0, new VerificationReason(
                "invalid-input",
                GetTierId(VerificationTier.FullLive),
                description,
                Array.Empty<string>()));
        }

        string decision = inputValid ? "run" : "blocked-invalid-input";
        string verdict = inputValid ? "pending" : "blocked";
        string seed = $"0x{source.SyntheticTree.Substring(0, 16)}";
        string evidenceProfile = reasons.Any(reason => reason.RuleId == "ui-or-rendering")
            ? "visual"
            : "functional";

        string[] requiredTiers = ProfileDefinitions
            .Where(x => x.Tier <= highestTier)
            .Select(x => GetTierId(x.Tier))
            .ToArray();

        VerificationProfile[] profiles = ProfileDefinitions
            .Select(x =>
            {
                bool required = x.Tier <= highestTier;
                return new VerificationProfile(
                    GetTierId(x.Tier),
                    (int)x.Tier,
                    required,
                    GetRuntimeVerdict(required, inputValid),
                    x.Executor,
                    x.Action,
                    x.Arguments,
                    x.Scope);
            })
            .ToArray();

        VerificationCheck[] checks = CheckDefinitions
            .Select(x =>
            {
                bool required = x.Tier <= highestTier;
                string checkId = x.Id;
                return new VerificationCheck(
                    checkId,
                    GetTierId(x.Tier),
                    required,
                    GetRuntimeVerdict(required, inputValid),
                    x.Topology,
                    seed,
                    $"verification-v1:{source.SyntheticTree}:{seed}:{checkId}",
                    checkId == "rendered-smoke" ? evidenceProfile : null);
            })
            .ToArray();

        string[] requiredChecks = checks
            .Where(check => check.Required)
            .Select(check => check.Id)
            .ToArray();

        string planDigest = canonicalJsonHasher.ComputeSha256(new
        {
            schemaVersion = VerificationPlan.CurrentSchemaVersion,
            source,
            decision,
            verdict,
            highestRequiredTier = GetTierId(highestTier),
            seed,
            changedPaths = normalizedPaths,
            rejectedPaths,
            requiredTiers,
            requiredChecks,
            evidenceProfile,
            reasons
        });

        return new VerificationPlan(
            source,
            decision,
            verdict,
            inputValid,
            GetTierId(highestTier),
            seed,
            planDigest,
            $"verification-v1:{source.SyntheticTree}:{seed}:{planDigest}",
            normalizedPaths,
            rejectedPaths,
            requiredTiers,
            requiredChecks,
            profiles,
            checks,
            reasons);
    }

    private static string GetRuntimeVerdict(bool required, bool inputValid)
    {
        if (!required) return "not-required";
        return inputValid ? "pending" : "blocked";
    }

    private static ClassificationRule Classify(string path)
    {
        foreach (ClassificationRule rule in ClassificationRules)
        {
            if (rule.Matches(path))
            {
                return rule;
            }
        }

        throw new InvalidOperationException("The final classification rule must match every path.");
    }

    private static bool TryNormalize(string? suppliedPath, out string? normalizedPath, out string? rejectionReason)
    {
        normalizedPath = null;
        rejectionReason = null;

        if (string.IsNullOrWhiteSpace(suppliedPath))
        {
            rejectionReason = "path is empty";
            return false;
        }

        string candidate = suppliedPath.Trim().Replace('\\', '/');
        if (candidate.StartsWith("/", StringComparison.Ordinal) ||
            (candidate.Length >= 2 && char.IsLetter(candidate[0]) && candidate[1] == ':'))
        {
            rejectionReason = "path must be repository-relative";
            return false;
        }

        if (candidate.Any(char.IsControl))
        {
            rejectionReason = "path contains a control character";
            return false;
        }

        var segments = new List<string>();
        foreach (string segment in candidate.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                rejectionReason = "path traverses outside the repository";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            rejectionReason = "path is empty";
            return false;
        }

        normalizedPath = string.Join('/', segments);
        return true;
    }

    private static int CompareRejectedPaths(RejectedPath left, RejectedPath right)
    {
        int pathComparison = StringComparer.OrdinalIgnoreCase.Compare(left.SuppliedPath, right.SuppliedPath);
        return pathComparison != 0
            ? pathComparison
            : StringComparer.Ordinal.Compare(left.Reason, right.Reason);
    }

    private static bool IsSteamPath(string path) =>
        SplitSegments(path).Any(segment => segment.Contains("steam", StringComparison.OrdinalIgnoreCase));

    private static bool IsNativePath(string path)
    {
        string extension = Path.GetExtension(path);
        return SplitSegments(path).Any(segment => segment.Contains("native", StringComparison.OrdinalIgnoreCase)) ||
               extension.Equals(".c", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".h", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".hpp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".asm", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".def", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiOrRenderingPath(string path)
    {
        string extension = Path.GetExtension(path);
        string[] segments = SplitSegments(path);
        return StartsWith(path, "UIMovies/") ||
               StartsWith(path, "Images/") ||
               StartsWith(path, "Workshop/") ||
               segments.Any(segment =>
                   segment.Equals("UI", StringComparison.OrdinalIgnoreCase) ||
                   segment.Equals("View", StringComparison.OrdinalIgnoreCase) ||
                   segment.Equals("Views", StringComparison.OrdinalIgnoreCase) ||
                   segment.Contains("Gauntlet", StringComparison.OrdinalIgnoreCase) ||
                   segment.Contains("Render", StringComparison.OrdinalIgnoreCase) ||
                   segment.Contains("Shader", StringComparison.OrdinalIgnoreCase) ||
                   segment.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
                   segment.Contains("Scene", StringComparison.OrdinalIgnoreCase)) ||
               extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mesh", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".material", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReflectionOrPatchingPath(string path) =>
        KnownRuntimeReflectionFiles.Contains(path) ||
        SplitSegments(path).Any(segment =>
            segment.Contains("Reflection", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("Harmony", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("AccessTools", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("Publicizer", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("Weaver", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("Patches", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(segment).EndsWith("Patch", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(segment).EndsWith("Patches", StringComparison.OrdinalIgnoreCase));

    private static bool IsGameRuntimePath(string path) =>
        StartsWith(path, "source/Coop/") ||
        StartsWith(path, "source/Missions/") ||
        StartsWith(path, "source/GameInterface/") ||
        StartsWith(path, "source/MissionTestMod/") ||
        StartsWith(path, "source/ClientDebug") ||
        StartsWith(path, "source/pdb/");

    private static bool IsDeploymentOrCiPath(string path) =>
        StartsWith(path, "deploy/") ||
        StartsWith(path, ".github/workflows/") ||
        path.Equals("source/SubModule.xml", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("SubModule.xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsProcessTestBuildContractPath(string path) =>
        StartsWith(path, "source/VerificationHarness.Tests/") && IsBuildContractFile(path);

    private static bool IsProductionBuildContractPath(string path) =>
        (StartsWith(path, "source/") &&
         IsBuildContractFile(path) &&
         !IsTestOnlyPath(path)) ||
        path.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildContractFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestOnlyPath(string path) =>
        StartsWith(path, "source/Common.Tests/") ||
        StartsWith(path, "source/Coop.Tests/") ||
        StartsWith(path, "source/GameInterface.Tests/") ||
        StartsWith(path, "source/Coop.CrashReporter.Tests/") ||
        StartsWith(path, "source/Coop.IntegrationTests/") ||
        StartsWith(path, "source/E2E.Tests/") ||
        StartsWith(path, "source/VerificationHarness.Tests/");

    private static bool IsDedicatedServerPath(string path) =>
        StartsWith(path, "source/ServerConsole/") ||
        StartsWith(path, "source/IntroServer/") ||
        StartsWith(path, "source/VerificationHarness/DedicatedServerSynthetic/") ||
        StartsWith(path, "source/VerificationHarness.Tests/DedicatedServerSynthetic/") ||
        path.Equals("source/VerificationHarness/Program.cs", StringComparison.OrdinalIgnoreCase) ||
        StartsWith(path, "source/Coop.Core/Server/") ||
        StartsWith(path, "source/Coop.Core/Common/Network/") ||
        StartsWith(path, "source/Common/Network/") ||
        StartsWith(path, "source/Common/Messaging/") ||
        StartsWith(path, "source/Common/PacketHandlers/") ||
        StartsWith(path, "source/Common/Serialization/") ||
        path.Equals("source/Common/Util/Poller.cs", StringComparison.OrdinalIgnoreCase) ||
        ContainsPath(path, "/Services/Save/") ||
        ContainsPath(path, "/Common/Session/") ||
        ContainsPath(path, "/Connections/");

    private static bool IsProcessBoundaryPath(string path) =>
        StartsWith(path, "source/VerificationHarness/Transport/") ||
        StartsWith(path, "source/VerificationHarness/PeerHost/") ||
        StartsWith(path, "source/VerificationHarness.Tests/Transport/") ||
        StartsWith(path, "source/VerificationHarness.Tests/PeerHost/");

    private static bool IsDeterministicPeerPath(string path) =>
        StartsWith(path, "source/Coop.Core/") ||
        StartsWith(path, "source/E2E.Tests/") ||
        StartsWith(path, "source/Coop.IntegrationTests/") ||
        StartsWith(path, ".github/scripts/run-e2e-");

    private static bool IsUnitPath(string path) =>
        StartsWith(path, "source/Common.Tests/") ||
        StartsWith(path, "source/Coop.Tests/") ||
        StartsWith(path, "source/GameInterface.Tests/") ||
        StartsWith(path, "source/Coop.CrashReporter/") ||
        StartsWith(path, "source/Coop.CrashReporter.Tests/") ||
        StartsWith(path, "source/VerificationHarness/") ||
        StartsWith(path, "source/VerificationHarness.Tests/") ||
        StartsWith(path, "tools/") ||
        StartsWith(path, "doc/") ||
        Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase) ||
        path.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase) ||
        path.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("LICENSE", StringComparison.OrdinalIgnoreCase);

    private static string[] SplitSegments(string path) => path.Split('/');

    private static bool StartsWith(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPath(string path, string part) =>
        path.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static string GetTierId(VerificationTier tier) => tier switch
    {
        VerificationTier.Unit => "unit",
        VerificationTier.DeterministicPeer => "deterministic-peer",
        VerificationTier.ProcessPeer => "process-peer",
        VerificationTier.DedicatedServerSynthetic => "dedicated-server-synthetic",
        VerificationTier.RenderedSmoke => "rendered-smoke",
        VerificationTier.FullLive => "full-live",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
    };

    private enum VerificationTier
    {
        Unit = 0,
        DeterministicPeer = 1,
        ProcessPeer = 2,
        DedicatedServerSynthetic = 3,
        RenderedSmoke = 4,
        FullLive = 5
    }

    private sealed class ProfileDefinition
    {
        public VerificationTier Tier { get; }
        public string Executor { get; }
        public string Action { get; }
        public IReadOnlyList<string> Arguments { get; }
        public string Scope { get; }

        public ProfileDefinition(
            VerificationTier tier,
            string executor,
            string action,
            IReadOnlyList<string> arguments,
            string scope)
        {
            Tier = tier;
            Executor = executor;
            Action = action;
            Arguments = arguments;
            Scope = scope;
        }
    }

    private sealed class CheckDefinition
    {
        public string Id { get; }
        public VerificationTier Tier { get; }
        public VerificationTopology Topology { get; }

        public CheckDefinition(string id, VerificationTier tier, VerificationTopology topology)
        {
            Id = id;
            Tier = tier;
            Topology = topology;
        }
    }

    private sealed class ClassificationRule
    {
        public string Id { get; }
        public VerificationTier Tier { get; }
        public string Description { get; }
        public Func<string, bool> Matches { get; }

        public ClassificationRule(
            string id,
            VerificationTier tier,
            string description,
            Func<string, bool> matches)
        {
            Id = id;
            Tier = tier;
            Description = description;
            Matches = matches;
        }
    }
}
