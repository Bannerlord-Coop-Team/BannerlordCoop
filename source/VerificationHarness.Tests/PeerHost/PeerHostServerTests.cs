using System.Text.Json;
using VerificationHarness.PeerHost;

namespace VerificationHarness.Tests.PeerHost;

public sealed class PeerHostServerTests
{
    [Fact]
    public async Task EndOfInputStopsCleanlyWithoutOutput()
    {
        var output = new StringWriter();
        var server = new PeerHostServer();

        int exitCode = await server.RunAsync(
            new StringReader(string.Empty),
            output,
            "client-a",
            42,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task SilentInputHonorsCancellation()
    {
        var input = new PrefixThenSilentTextReader(string.Empty);
        var output = new StringWriter();
        var server = new PeerHostServer();
        using var cancellation = new CancellationTokenSource();

        Task<int> runTask = server.RunAsync(
            input,
            output,
            "client-a",
            42,
            cancellation.Token);
        await input.WaitingForMoreInput.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task OversizedInputWithoutNewlineFailsBeforeWaitingForMoreInput()
    {
        var input = new PrefixThenSilentTextReader(
            new string('x', PeerHostServer.MaximumLineLength + 1));
        var output = new StringWriter();
        var server = new PeerHostServer();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        int exitCode = await server.RunAsync(
                input,
                output,
                "client-a",
                42,
                cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(PeerHostServer.InvalidRequestExitCode, exitCode);
        Assert.False(input.WaitingForMoreInput.Task.IsCompleted);
        using JsonDocument response = JsonDocument.Parse(output.ToString());
        Assert.Equal("invalid-frame", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            "Request line exceeds 65536 characters.",
            response.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvalidJsonEmitsErrorAndStopsWithFailure()
    {
        var output = new StringWriter();
        var server = new PeerHostServer();

        int exitCode = await server.RunAsync(
            new StringReader("not-json\n"),
            output,
            "client-a",
            42,
            CancellationToken.None);

        Assert.Equal(PeerHostServer.InvalidRequestExitCode, exitCode);
        using JsonDocument response = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, response.RootElement.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("client-a", response.RootElement.GetProperty("instanceId").GetString());
        Assert.Equal(42, response.RootElement.GetProperty("processId").GetInt32());
        Assert.Equal(1, response.RootElement.GetProperty("sequence").GetInt64());
        Assert.Equal("error", response.RootElement.GetProperty("status").GetString());
        Assert.Equal("invalid-json", response.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SequenceGapEmitsExpectedSequenceAndStopsWithFailure()
    {
        string input = JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            sequence = 2,
            command = "hello"
        });
        var output = new StringWriter();
        var server = new PeerHostServer();

        int exitCode = await server.RunAsync(
            new StringReader(input + "\n"),
            output,
            "client-a",
            42,
            CancellationToken.None);

        Assert.Equal(PeerHostServer.InvalidRequestExitCode, exitCode);
        using JsonDocument response = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, response.RootElement.GetProperty("sequence").GetInt64());
        Assert.Equal("invalid-sequence", response.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private sealed class PrefixThenSilentTextReader : TextReader
    {
        private readonly string prefix;
        private readonly TaskCompletionSource<string?> parameterlessRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int offset;

        public TaskCompletionSource WaitingForMoreInput { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PrefixThenSilentTextReader(string prefix)
        {
            this.prefix = prefix;
        }

        public override Task<string?> ReadLineAsync()
        {
            return parameterlessRead.Task;
        }

        public override ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
            {
                return ValueTask.FromResult(0);
            }

            if (offset < prefix.Length)
            {
                buffer.Span[0] = prefix[offset];
                offset++;
                return ValueTask.FromResult(1);
            }

            WaitingForMoreInput.TrySetResult();
            return new ValueTask<int>(WaitForCancellation(cancellationToken));
        }

        private static async Task<int> WaitForCancellation(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
