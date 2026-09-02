using Common.LiveTesting;
using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerControlResponseValidatorTests
{
    private static readonly DedicatedServerControlExpectation Expectation = new(
        1234,
        "run-token",
        "request-id",
        4201,
        DedicatedServerSyntheticOptions.ExpectedControllerIds);

    private readonly DedicatedServerControlResponseValidator validator = new();

    [Fact]
    public void FutureRosterSurface_ValidatesExactTwoClients()
    {
        string json = ResponseJson(
            1234,
            "run-token",
            "request-id",
            new
            {
                serving = true,
                joinPort = 4201,
                connectionRoster = new[]
                {
                    new { controllerId = "ds-synthetic-client-a", connectionInstanceId = "connection-a-1", connected = true, joinState = "ResolveCharacterState" },
                    new { controllerId = "ds-synthetic-client-b", connectionInstanceId = "connection-b-1", connected = true, joinState = "ResolveCharacterState" }
                }
            });

        DedicatedServerControlValidation result = validator.Validate(json, Expectation);

        Assert.True(result.IsValid);
        Assert.Empty(result.FailureCodes);
    }

    [Fact]
    public void ReusedConnectionInstanceIdentityFailsClosed()
    {
        string json = ResponseJson(
            1234,
            "run-token",
            "request-id",
            new
            {
                serving = true,
                joinPort = 4201,
                connectionRoster = new[]
                {
                    new { controllerId = "ds-synthetic-client-a", connectionInstanceId = "reused", connected = true, joinState = "ResolveCharacterState" },
                    new { controllerId = "ds-synthetic-client-b", connectionInstanceId = "reused", connected = true, joinState = "ResolveCharacterState" }
                }
            });

        DedicatedServerControlValidation result = validator.Validate(json, Expectation);

        Assert.False(result.IsValid);
        Assert.Contains("expected-connection-roster-mismatch", result.FailureCodes);
    }

    [Fact]
    public void RegisteredPlayerCount_DoesNotMasqueradeAsConnectionRoster()
    {
        string json = ResponseJson(
            1234,
            "run-token",
            "request-id",
            new
            {
                serving = true,
                joinPort = 4201,
                registeredPlayers = 2
            });

        DedicatedServerControlValidation result = validator.Validate(json, Expectation);

        Assert.False(result.IsValid);
        Assert.False(result.RosterSurfaceValid);
        Assert.Contains("first-class-connection-roster-missing", result.FailureCodes);
    }

    [Theory]
    [InlineData(9999, "run-token", "request-id", "process-id-mismatch")]
    [InlineData(1234, "other-run", "request-id", "run-token-mismatch")]
    [InlineData(1234, "run-token", "other-request", "request-id-mismatch")]
    public void ResponseIdentityMismatch_FailsClosed(
        int processId,
        string runToken,
        string requestId,
        string failureCode)
    {
        string json = ResponseJson(
            processId,
            runToken,
            requestId,
            new
            {
                serving = true,
                joinPort = 4201,
                connectionRoster = Array.Empty<object>()
            });

        DedicatedServerControlValidation result = validator.Validate(json, Expectation);

        Assert.False(result.IsValid);
        Assert.Contains(failureCode, result.FailureCodes);
    }

    private static string ResponseJson(int processId, string runToken, string requestId, object result)
    {
        return LiveTestProtocol.SerializeResponse(new LiveTestResponse
        {
            Id = requestId,
            Ok = true,
            Process = new LiveTestProcessInfo
            {
                Pid = processId,
                Role = "server",
                RunToken = runToken
            },
            Result = result
        });
    }
}
