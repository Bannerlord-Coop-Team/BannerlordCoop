using System.Text.Json.Nodes;
using VerificationHarness.Planning;
using VerificationHarness.Serialization;

namespace VerificationHarness.Tests.Planning;

public sealed class VerificationPlanValidatorTests
{
    private const string Head = "1111111111111111111111111111111111111111";
    private const string Tree = "2222222222222222222222222222222222222222";
    private const string Base = "3333333333333333333333333333333333333333";
    private readonly VerificationSourceIdentity source = new(Head, Tree);

    [Fact]
    public void ProcessLabPlanHasNoExternalRuntimeClaim()
    {
        VerificationPlan plan = new VerificationPlanBuilder().Build(
            source,
            new[] { "source/VerificationHarness/Transport/TransportCodec.cs" });
        string json = new VerificationPlanWriter().Serialize(plan);

        VerificationPlanReceipt receipt = new VerificationPlanValidator().Validate(
            json,
            Head,
            Tree,
            Base,
            plan.ChangedPaths);

        Assert.Equal("validated-pending-ci", receipt.Verdict);
        Assert.Equal("selection-and-local-harness-handoff", receipt.Scope);
        Assert.False(receipt.IncludesTestEvidence);
        Assert.Equal(Base, receipt.AuthoritativeBase);
        Assert.Equal(new CanonicalJsonHasher().ComputeSha256(plan.ChangedPaths), receipt.ChangedPathsDigest);
        Assert.Equal(new[] { "process-peer" }, receipt.HarnessOwnedProfiles);
        Assert.Empty(receipt.ExternalRuntimeProfiles);
    }

    [Fact]
    public void RuntimePlanIsExplicitlyBlockedForExternalExecution()
    {
        VerificationPlan plan = new VerificationPlanBuilder().Build(
            source,
            new[] { "source/Coop/LiveTesting/LiveTestControlServer.cs" });
        string json = new VerificationPlanWriter().Serialize(plan);

        VerificationPlanReceipt receipt = new VerificationPlanValidator().Validate(
            json,
            Head,
            Tree,
            Base,
            plan.ChangedPaths);

        Assert.Equal("blocked-external-runtime", receipt.Verdict);
        Assert.Equal(
            new[] { "dedicated-server-synthetic", "rendered-smoke", "full-live" },
            receipt.ExternalRuntimeProfiles);
    }

    [Fact]
    public void TamperedPlanFailsValidation()
    {
        VerificationPlan plan = new VerificationPlanBuilder().Build(
            source,
            new[] { "source/VerificationHarness/Transport/TransportCodec.cs" });
        JsonNode root = JsonNode.Parse(new VerificationPlanWriter().Serialize(plan))!;
        root["profiles"]![2]!["verdict"] = "passed";

        Assert.Throws<InvalidDataException>(() =>
            new VerificationPlanValidator().Validate(
                root.ToJsonString(),
                Head,
                Tree,
                Base,
                plan.ChangedPaths));
    }

    [Fact]
    public void OmittedHighTierPathFailsAgainstAuthoritativeGitDiff()
    {
        VerificationPlan incompletePlan = new VerificationPlanBuilder().Build(
            source,
            new[] { "source/VerificationHarness/Transport/TransportCodec.cs" });
        string json = new VerificationPlanWriter().Serialize(incompletePlan);
        string[] authoritativePaths =
        {
            "source/VerificationHarness/Transport/TransportCodec.cs",
            "source/Coop/LiveTesting/LiveTestControlServer.cs"
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new VerificationPlanValidator().Validate(
                json,
                Head,
                Tree,
                Base,
                authoritativePaths));

        Assert.Contains("authoritative Git diff", exception.Message);
    }

    [Fact]
    public void InvalidAuthoritativeBaseFailsValidation()
    {
        VerificationPlan plan = new VerificationPlanBuilder().Build(
            source,
            new[] { "source/VerificationHarness/Transport/TransportCodec.cs" });

        Assert.Throws<ArgumentException>(() =>
            new VerificationPlanValidator().Validate(
                new VerificationPlanWriter().Serialize(plan),
                Head,
                Tree,
                "not-a-git-object",
                plan.ChangedPaths));
    }
}
