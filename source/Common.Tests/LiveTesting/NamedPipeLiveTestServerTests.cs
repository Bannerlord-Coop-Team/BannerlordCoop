using Common.LiveTesting;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Common.Tests.LiveTesting;

public class NamedPipeLiveTestServerTests
{
    [Fact]
    public void Server_HandlesPersistentRequestsAndReconnects()
    {
        string pipeName = "BannerlordCoop.LiveTest.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(request.Id, Process(), new { method = request.Method }));
        server.Start();

        using (var firstClient = Connect(pipeName))
        {
            AssertResponse(firstClient, "status", "status");
            AssertResponse(firstClient, "command", "command");
        }

        using var secondClient = Connect(pipeName);
        AssertResponse(secondClient, "screenshot", "screenshot");
    }

    [Fact]
    public void Server_MalformedRequestDoesNotKillConnection()
    {
        string pipeName = "BannerlordCoop.LiveTest.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(request.Id, Process(), new { method = request.Method }));
        server.Start();

        using ConnectedPipe client = Connect(pipeName);
        client.Writer.WriteLine("not json");

        LiveTestResponse malformed = ReadResponse(client);
        Assert.False(malformed.Ok);
        Assert.Equal("invalid_json", malformed.Error.Code);

        AssertResponse(client, "status", "status");
    }

    [Fact]
    public void Server_OversizedRequestDoesNotKillConnection()
    {
        string pipeName = "BannerlordCoop.LiveTest.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(request.Id, Process(), new { method = request.Method }));
        server.Start();

        using ConnectedPipe client = Connect(pipeName);
        client.Writer.WriteLine(new string('x', LiveTestProtocol.MaximumMessageBytes + 1));

        LiveTestResponse oversized = ReadResponse(client);
        Assert.False(oversized.Ok);
        Assert.Equal("request_too_large", oversized.Error.Code);

        AssertResponse(client, "status", "status");
    }

    [Fact]
    public void Server_InvalidUtf8DoesNotKillListener()
    {
        string pipeName = "BannerlordCoop.LiveTest.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(request.Id, Process(), new { method = request.Method }));
        server.Start();

        using (var malformedClient = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous))
        {
            malformedClient.Connect(5000);
            malformedClient.Write(new byte[] { 0xff, (byte)'\n' });
            malformedClient.Flush();
        }

        using ConnectedPipe validClient = Connect(pipeName);
        AssertResponse(validClient, "status", "status");
    }

    [Fact]
    public void Server_OversizedResponseReportsUncertainOutcome()
    {
        string pipeName = "BannerlordCoop.LiveTest.Tests." + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(
                request.Id,
                Process(),
                new { output = new string('x', LiveTestProtocol.MaximumMessageBytes) }));
        server.Start();

        using ConnectedPipe client = Connect(pipeName);
        string id = Guid.NewGuid().ToString("N");
        using JsonDocument parameters = JsonDocument.Parse("{}");
        client.Writer.WriteLine(LiveTestProtocol.SerializeRequest(new LiveTestRequest
        {
            Version = LiveTestProtocol.Version,
            Id = id,
            Method = "command",
            Parameters = parameters.RootElement.Clone(),
        }));

        LiveTestResponse response = ReadResponse(client);
        Assert.False(response.Ok);
        Assert.Equal(id, response.Id);
        Assert.Equal("response_too_large", response.Error.Code);
        Assert.True(response.Error.OutcomeUncertain);
    }

    [Fact]
    public void Dispose_WhileWaitingForConnection_ReturnsPromptlyAndReleasesPipe()
    {
        string pipeName = "BannerlordCoop.LiveTest.Tests." + Guid.NewGuid().ToString("N");
        var server = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(request.Id, Process(), new { }));
        server.Start();
        Assert.True(server.WaitUntilListening(TimeSpan.FromSeconds(5)));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        server.Dispose();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));

        using var replacement = new NamedPipeLiveTestServer(pipeName, Process, request =>
            LiveTestResponse.Success(request.Id, Process(), new { method = request.Method }));
        replacement.Start();

        using ConnectedPipe client = Connect(pipeName);
        AssertResponse(client, "status", "status");
    }

    private static ConnectedPipe Connect(string pipeName)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        pipe.Connect(5000);
        return new ConnectedPipe(pipe);
    }

    private static void AssertResponse(ConnectedPipe client, string method, string expectedMethod)
    {
        string id = Guid.NewGuid().ToString("N");
        using JsonDocument parameters = JsonDocument.Parse("{}");
        var request = new LiveTestRequest
        {
            Version = LiveTestProtocol.Version,
            Id = id,
            Method = method,
            Parameters = parameters.RootElement.Clone(),
        };

        client.Writer.WriteLine(LiveTestProtocol.SerializeRequest(request));

        LiveTestResponse response = ReadResponse(client);
        Assert.True(response.Ok);
        Assert.Equal(id, response.Id);
        Assert.Equal(456, response.Process.Pid);
        JsonElement result = Assert.IsType<JsonElement>(response.Result);
        Assert.Equal(expectedMethod, result.GetProperty("method").GetString());
    }

    private static LiveTestResponse ReadResponse(ConnectedPipe client)
    {
        string? json = client.Reader.ReadLine();
        Assert.NotNull(json);
        Assert.True(LiveTestProtocol.TryDeserializeResponse(json, out var response, out var error), error?.Message);
        return response;
    }

    private static LiveTestProcessInfo Process()
    {
        return new LiveTestProcessInfo
        {
            Pid = 456,
            Role = "client",
            PlatformId = "testclient",
            RunToken = "pipe-test",
        };
    }

    private sealed class ConnectedPipe : IDisposable
    {
        public ConnectedPipe(NamedPipeClientStream pipe)
        {
            Pipe = pipe;
            var encoding = new UTF8Encoding(false);
            Reader = new StreamReader(pipe, encoding, false, 4096, true);
            Writer = new StreamWriter(pipe, encoding, 4096, true) { AutoFlush = true };
        }

        public NamedPipeClientStream Pipe { get; }

        public StreamReader Reader { get; }

        public StreamWriter Writer { get; }

        public void Dispose()
        {
            Writer.Dispose();
            Reader.Dispose();
            Pipe.Dispose();
        }
    }
}
