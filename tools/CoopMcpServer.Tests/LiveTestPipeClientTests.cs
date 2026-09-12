using Common.LiveTesting;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CoopMcpServer.Tests;

public sealed class LiveTestPipeClientTests
{
    [Fact]
    public async Task BoundedReaderRejectsOversizedAndIncompleteResponses()
    {
        var client = new LiveTestPipeClient();
        using var oversized = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', LiveTestProtocol.MaximumMessageBytes + 1) + "\n"));
        await Assert.ThrowsAsync<IOException>(() => client.ReadResponseAsync(oversized, default));
        using var incomplete = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        await Assert.ThrowsAsync<IOException>(() => client.ReadResponseAsync(incomplete, default));
    }

    [Fact]
    public async Task StaleRegistrationIsRejectedBeforeMutationIsSent()
    {
        using var endpoint = new Endpoint();
        endpoint.Register(endpoint.Identity with { StartedUtc = endpoint.Identity.StartedUtc.AddSeconds(-1) });
        var response = await new LiveTestPipeClient().SendAsync(endpoint.Identity, "command", new { }, true, default);
        Assert.False(response.Ok);
        Assert.False(response.Error.OutcomeUncertain);
    }

    [Theory]
    [InlineData("pid")]
    [InlineData("start")]
    [InlineData("token")]
    [InlineData("role")]
    [InlineData("platform")]
    [InlineData("id")]
    public async Task ResponseIdentityMismatchPreservesMutationUncertainty(string mismatch)
    {
        using var endpoint = new Endpoint();
        endpoint.Register(endpoint.Identity);
        var server = endpoint.ServeAsync(request =>
        {
            var identity = endpoint.Identity;
            var process = new LiveTestProcessInfo
            {
                Pid = identity.Pid + (mismatch == "pid" ? 1 : 0),
                ProcessStartedUtc = identity.StartedUtc.AddSeconds(mismatch == "start" ? 1 : 0),
                RunToken = mismatch == "token" ? "other" : identity.RunToken,
                Role = mismatch == "role" ? "client" : identity.Role,
                PlatformId = mismatch == "platform" ? "other" : identity.PlatformId,
            };
            return LiveTestResponse.Success(mismatch == "id" ? "other" : request.Id, process, new { });
        });
        var response = await new LiveTestPipeClient().SendAsync(endpoint.Identity, "command", new { }, true, default);
        await server;
        Assert.False(response.Ok);
        Assert.True(response.Error.OutcomeUncertain);
    }

    [Fact]
    public async Task BrokenPipeAfterWriteDoesNotClaimMutationWasNotApplied()
    {
        using var endpoint = new Endpoint();
        endpoint.Register(endpoint.Identity);
        var server = endpoint.ServeAsync(_ => null);
        var response = await new LiveTestPipeClient().SendAsync(endpoint.Identity, "join", new { }, true, default);
        await server;
        Assert.False(response.Ok);
        Assert.True(response.Error.OutcomeUncertain);
    }

    [Fact]
    public async Task ValidResponseAndArgumentArrayRoundTrip()
    {
        using var endpoint = new Endpoint();
        endpoint.Register(endpoint.Identity);
        var server = endpoint.ServeAsync(request =>
        {
            Assert.Equal("value with spaces", request.Parameters.GetProperty("arguments")[0].GetString());
            return LiveTestResponse.Success(request.Id, new LiveTestProcessInfo
            {
                Pid = endpoint.Identity.Pid, ProcessStartedUtc = endpoint.Identity.StartedUtc,
                RunToken = endpoint.Identity.RunToken, Role = endpoint.Identity.Role, PlatformId = endpoint.Identity.PlatformId,
            }, new { output = "success" });
        });
        var response = await new LiveTestPipeClient().SendAsync(endpoint.Identity, "command", new { arguments = new[] { "value with spaces" } }, true, default);
        await server;
        Assert.True(response.Ok);
    }

    private sealed class Endpoint : IDisposable
    {
        public InstanceIdentity Identity { get; } = new(Random.Shared.Next(100000000, int.MaxValue), DateTime.UtcNow,
            "server", "testserver", "test-" + Guid.NewGuid().ToString("N"));
        private string DirectoryPath => Path.Combine(Path.GetTempPath(), "BannerlordCoop.LiveTest.v1", Identity.RunToken);

        public void Register(InstanceIdentity identity)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, Identity.Pid + ".json"), JsonSerializer.Serialize(new
            {
                version = LiveTestProtocol.Version, pid = identity.Pid, processStartedUtc = identity.StartedUtc,
                role = identity.Role, platformId = identity.PlatformId, runToken = identity.RunToken,
                pipeName = LiveTestProtocol.GetPipeName(identity.Pid),
            }));
        }

        public async Task ServeAsync(Func<LiveTestRequest, LiveTestResponse> respond)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var pipe = new NamedPipeServerStream(LiveTestProtocol.GetPipeName(Identity.Pid), PipeDirection.InOut,
                1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(timeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            string line = await reader.ReadLineAsync(timeout.Token);
            Assert.True(LiveTestProtocol.TryDeserializeRequest(line, out var request, out _));
            var response = respond(request);
            if (response != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(LiveTestProtocol.SerializeResponse(response) + "\n");
                await pipe.WriteAsync(bytes, timeout.Token);
                await pipe.FlushAsync(timeout.Token);
            }
        }

        public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
    }
}
