using VerificationHarness.Planning;

namespace VerificationHarness.Tests.Planning;

public sealed class VerificationPlanBuilderTests
{
    private static readonly string[] AllTiers =
    {
        "unit",
        "deterministic-peer",
        "process-peer",
        "dedicated-server-synthetic",
        "rendered-smoke",
        "full-live"
    };

    private readonly VerificationPlanBuilder builder = new();
    private readonly VerificationSourceIdentity source = new(
        "1111111111111111111111111111111111111111",
        "2222222222222222222222222222222222222222");

    [Fact]
    public void UnitPathRequiresOnlyUnitProfile()
    {
        VerificationPlan plan = Build("source/Common.Tests/Utils/PollerTests.cs");

        Assert.True(plan.InputValid);
        Assert.Equal("unit", plan.HighestRequiredTier);
        Assert.Equal(new[] { "unit" }, plan.RequiredTiers);
    }

    [Fact]
    public void CoopCorePathRequiresDeterministicPeers()
    {
        VerificationPlan plan = Build("source/Coop.Core/Services/Time/TimeControl.cs");

        Assert.Equal("deterministic-peer", plan.HighestRequiredTier);
        Assert.Equal(new[] { "unit", "deterministic-peer" }, plan.RequiredTiers);
    }

    [Fact]
    public void ProductionNetworkPathRequiresDedicatedServerSyntheticProfile()
    {
        VerificationPlan plan = Build("source/Common/Network/MessagePacket.cs");

        Assert.Equal("dedicated-server-synthetic", plan.HighestRequiredTier);
        Assert.Equal(AllTiers.Take(4), plan.RequiredTiers);
    }

    [Theory]
    [InlineData("source/VerificationHarness/Transport/ProcessPeerController.cs")]
    [InlineData("source/VerificationHarness/PeerHost/PeerHostServer.cs")]
    [InlineData("source/VerificationHarness.Tests/Transport/ProcessPeerLabTests.cs")]
    public void ProcessHarnessChangesRequireTheirOwnProcessProfile(string path)
    {
        VerificationPlan plan = Build(path);

        Assert.Equal("process-peer", plan.HighestRequiredTier);
        Assert.Equal(new[] { "unit", "deterministic-peer", "process-peer" }, plan.RequiredTiers);
    }

    [Fact]
    public void DedicatedSyntheticHarnessChangesRequireDedicatedServerProfile()
    {
        VerificationPlan plan = Build(
            "source/VerificationHarness/DedicatedServerSynthetic/DedicatedServerSyntheticController.cs");

        Assert.Equal("dedicated-server-synthetic", plan.HighestRequiredTier);
        Assert.Equal(AllTiers.Take(4), plan.RequiredTiers);
    }

    [Theory]
    [InlineData("source/ServerConsole/Program.cs")]
    [InlineData("source/Coop.Core/Common/Network/Packets/CampaignTimePacket.cs")]
    [InlineData("source/Coop.Core/Common/Network/Packets/GameSaveDataChunkPacket.cs")]
    [InlineData("source/Coop.Core/Common/Network/CoopNetworkBase.cs")]
    [InlineData("source/Common/Network/ConnectionPassword.cs")]
    [InlineData("source/Common/Network/ReliableMessageBatcher.cs")]
    [InlineData("source/Common/PacketHandlers/AggregateMessagePacketHandler.cs")]
    [InlineData("source/VerificationHarness/Program.cs")]
    public void DedicatedServerContractPathRequiresDedicatedServerSyntheticProfile(string path)
    {
        VerificationPlan plan = Build(path);

        Assert.Equal("dedicated-server-synthetic", plan.HighestRequiredTier);
        Assert.Equal(AllTiers.Take(4), plan.RequiredTiers);
    }

    [Theory]
    [InlineData("source/Coop.Core/Client/CoopClient.cs")]
    [InlineData("source/Coop.Core/Client/ClientModule.cs")]
    [InlineData("source/Coop.Core/Common/Configuration/NetworkConfig.cs")]
    [InlineData("source/Coop.Core/Common/Configuration/SessionAdvertisementConfig.cs")]
    [InlineData("source/Coop.Core/CoopartiveMultiplayerExperience.cs")]
    public void ProductionClientTransportRequiresFullLive(string path)
    {
        VerificationPlan plan = Build(path);

        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
        Assert.Contains(plan.Reasons, reason => reason.RuleId == "production-client-transport");
    }

    [Theory]
    [InlineData("source/Coop.Steam/SteamBoot.cs", "steam-integration")]
    [InlineData("source/Common/Native/Bridge.cs", "native-boundary")]
    [InlineData("UIMovies/CoopMenu.xml", "ui-or-rendering")]
    [InlineData("source/Coop.Core/Services/HarmonyPatches.cs", "reflection-or-runtime-patching")]
    [InlineData("source/GameInterface/Services/Entity/EntityInterface.cs", "game-runtime")]
    [InlineData("source/Common/Common.csproj", "build-contract")]
    public void RuntimeBoundaryEscalatesToFullLive(string path, string expectedRule)
    {
        VerificationPlan plan = Build(path);

        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
        Assert.Contains(plan.Reasons, reason => reason.RuleId == expectedRule);
        Assert.True(plan.Profiles.Single(profile => profile.Id == "rendered-smoke").Required);
        Assert.True(plan.Profiles.Single(profile => profile.Id == "full-live").Required);
    }

    [Fact]
    public void KnownRuntimeReflectionHandlerRequiresFullLive()
    {
        VerificationPlan plan = Build(
            "source/Coop.Core/Server/Services/Players/Handlers/PlayerPartyVisibilityHandler.cs");

        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
        Assert.Contains(plan.Reasons, reason => reason.RuleId == "reflection-or-runtime-patching");
    }

    [Theory]
    [InlineData("source/Common.Tests/Common.Tests.csproj", "unit")]
    [InlineData("source/Common.Tests/TestOnly.props", "unit")]
    [InlineData("source/Coop.Tests/Coop.Tests.csproj", "unit")]
    [InlineData("source/GameInterface.Tests/GameInterface.Tests.csproj", "unit")]
    [InlineData("source/Coop.CrashReporter.Tests/Coop.CrashReporter.Tests.csproj", "unit")]
    [InlineData("source/E2E.Tests/E2E.Tests.csproj", "deterministic-peer")]
    [InlineData("source/E2E.Tests/TestOnly.targets", "deterministic-peer")]
    [InlineData("source/Coop.IntegrationTests/Coop.IntegrationTests.csproj", "deterministic-peer")]
    [InlineData("source/VerificationHarness.Tests/VerificationHarness.Tests.csproj", "process-peer")]
    [InlineData("source/VerificationHarness.Tests/TestOnly.props", "process-peer")]
    public void TestOnlyBuildContractsUseTheirOwningTestTier(string path, string expectedTier)
    {
        VerificationPlan plan = Build(path);

        Assert.Equal(expectedTier, plan.HighestRequiredTier);
        Assert.DoesNotContain(plan.Reasons, reason => reason.RuleId == "build-contract");
    }

    [Theory]
    [InlineData("source/Common/Common.csproj")]
    [InlineData("source/Coop.Core/Coop.Core.csproj")]
    [InlineData("source/VerificationHarness/VerificationHarness.csproj")]
    [InlineData("source/Directory.Build.props")]
    [InlineData("Directory.Build.targets")]
    public void NonTestBuildContractsStillRequireFullLive(string path)
    {
        VerificationPlan plan = Build(path);

        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Contains(plan.Reasons, reason => reason.RuleId == "build-contract");
    }

    [Fact]
    public void UnknownPathFailsClosedToFullLive()
    {
        VerificationPlan plan = Build("new-root/surprise.bin");

        Assert.True(plan.InputValid);
        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
        Assert.Equal("unknown-path", Assert.Single(plan.Reasons).RuleId);
    }

    [Fact]
    public void HighRiskPathCannotBeDowngradedByLowRiskPath()
    {
        VerificationPlan plan = Build(
            "source/Common.Tests/Utils/PollerTests.cs",
            "source/Coop.Steam/SteamBoot.cs");

        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
    }

    [Fact]
    public void EmptyInputIsBlockedAtFullLive()
    {
        VerificationPlan plan = Build();

        Assert.False(plan.InputValid);
        Assert.Equal("blocked-invalid-input", plan.Decision);
        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
        Assert.Equal("invalid-input", Assert.Single(plan.Reasons).RuleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../source/Common/Util/Poller.cs")]
    [InlineData("/source/Common/Util/Poller.cs")]
    [InlineData("C:\\source\\Common\\Util\\Poller.cs")]
    [InlineData("C:source\\Common\\Util\\Poller.cs")]
    public void MalformedPathIsBlockedAtFullLive(string path)
    {
        VerificationPlan plan = Build(path);

        Assert.False(plan.InputValid);
        Assert.Equal("full-live", plan.HighestRequiredTier);
        Assert.Equal(AllTiers, plan.RequiredTiers);
        Assert.Single(plan.RejectedPaths);
        Assert.Contains(plan.Reasons, reason => reason.RuleId == "invalid-input");
    }

    [Fact]
    public void OutputIsDeterministicAcrossInputOrderingAndSeparators()
    {
        var writer = new VerificationPlanWriter();
        string first = writer.Serialize(Build(
            @"source\Common\Network\MessagePacket.cs",
            "./source/Common.Tests/Utils/PollerTests.cs"));
        string second = writer.Serialize(Build(
            "source/Common.Tests/Utils/PollerTests.cs",
            "source/Common/Network/MessagePacket.cs",
            "source/common/network/messagepacket.cs"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void CatalogAlwaysContainsAllBlockingProfilesInOrder()
    {
        VerificationPlan plan = Build("README.md");

        Assert.Equal(AllTiers, plan.Profiles.Select(profile => profile.Id));
        Assert.Equal(Enumerable.Range(0, AllTiers.Length), plan.Profiles.Select(profile => profile.Ordinal));
        Assert.All(plan.Profiles, profile => Assert.True(profile.Blocking));

        VerificationProfile processPeer = plan.Profiles.Single(profile => profile.Id == "process-peer");
        Assert.Equal("repository-dotnet-run", processPeer.Executor);
        Assert.Equal("process-peer-suite", processPeer.Action);
        Assert.Equal(new[]
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
        }, processPeer.Arguments);
        Assert.Contains("loopback UDP", processPeer.Scope);
        Assert.Contains("Synthetic transport lab", processPeer.Scope);
        Assert.Contains("does not claim Bannerlord handlers", processPeer.Scope);

        foreach (string profileId in new[] { "dedicated-server-synthetic", "rendered-smoke", "full-live" })
        {
            VerificationProfile profile = plan.Profiles.Single(item => item.Id == profileId);
            Assert.NotEmpty(profile.Arguments);
            Assert.Contains("{source.head}", profile.Arguments);
            Assert.Contains("{source.syntheticTree}", profile.Arguments);
            Assert.Contains("{evidence.output}", profile.Arguments);
        }
    }

    [Fact]
    public void PlanCarriesStableSourceChecksAndPendingRuntimeFields()
    {
        VerificationPlan plan = Build("UIMovies/CoopMenu.xml");

        Assert.Equal("1111111111111111111111111111111111111111", plan.Source.Head);
        Assert.Equal("2222222222222222222222222222222222222222", plan.Source.SyntheticTree);
        Assert.Equal("0x2222222222222222", plan.Seed);
        Assert.Equal(64, plan.PlanDigest.Length);
        Assert.Equal(new[]
        {
            "unit",
            "wire-copy-e2e",
            "poller-game-thread",
            "deterministic-peer",
            "process-peer",
            "dedicated-server-synthetic",
            "rendered-smoke",
            "full-live"
        }, plan.RequiredChecks);

        VerificationCheck rendered = plan.Checks.Single(check => check.Id == "rendered-smoke");
        Assert.Equal("visual", rendered.EvidenceProfile);
        Assert.Equal(2, rendered.Topology.ClientCount);
        Assert.Null(rendered.StateDigest);
        Assert.Null(rendered.StartedAtUtc);
        Assert.Empty(rendered.ArtifactHashes);
        Assert.Empty(rendered.ProcessExits);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void SourceIdentityRejectsNonGitObjectIds(string objectId)
    {
        Assert.Throws<ArgumentException>(() => new VerificationSourceIdentity(
            objectId,
            "2222222222222222222222222222222222222222"));
    }

    private VerificationPlan Build(params string?[] paths) => builder.Build(source, paths);
}
