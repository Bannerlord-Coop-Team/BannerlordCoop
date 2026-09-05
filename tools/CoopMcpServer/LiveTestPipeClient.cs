using Common.LiveTesting;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CoopMcpServer;

public interface ILiveTestPipeClient
{
    Task<LiveTestResponse> SendAsync(InstanceIdentity identity, string method, object parameters,
        bool mutation, CancellationToken cancellationToken);
}

public sealed record InstanceIdentity(int Pid, DateTime StartedUtc, string Role, string PlatformId, string RunToken);

public sealed class LiveTestPipeClient : ILiveTestPipeClient
{
    public async Task<LiveTestResponse> SendAsync(InstanceIdentity identity, string method, object parameters,
        bool mutation, CancellationToken cancellationToken)
    {
        string id = Guid.NewGuid().ToString("N");
        bool writeStarted = false;
        var process = new LiveTestProcessInfo
        {
            Pid = identity.Pid, ProcessStartedUtc = identity.StartedUtc, Role = identity.Role,
            PlatformId = identity.PlatformId, RunToken = identity.RunToken,
        };
        try
        {
            var request = new LiveTestRequest
            {
                Version = LiveTestProtocol.Version, Id = id, Method = method,
                Parameters = JsonSerializer.SerializeToElement(parameters),
            };
            byte[] payload = Encoding.UTF8.GetBytes(LiveTestProtocol.SerializeRequest(request) + "\n");
            ValidateRegistration(identity);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(35));
            using var pipe = new NamedPipeClientStream(".", LiveTestProtocol.GetPipeName(identity.Pid),
                PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500, timeout.Token);
            writeStarted = true;
            await pipe.WriteAsync(payload, timeout.Token);
            await pipe.FlushAsync(timeout.Token);
            string json = await ReadResponseAsync(pipe, timeout.Token);
            if (!LiveTestProtocol.TryDeserializeResponse(json, out var response, out var error))
                throw new IOException(error.Message);
            if (response.Id != id || response.Process.Pid != identity.Pid ||
                response.Process.ProcessStartedUtc != identity.StartedUtc ||
                response.Process.Role != identity.Role || response.Process.PlatformId != identity.PlatformId ||
                response.Process.RunToken != identity.RunToken)
                throw new IOException("Response identity or request id mismatch.");
            return response;
        }
        catch (Exception exception) when (exception is IOException || exception is TimeoutException ||
            exception is OperationCanceledException || exception is JsonException ||
            exception is UnauthorizedAccessException || exception is InvalidOperationException ||
            exception is KeyNotFoundException || exception is FormatException || exception is ArgumentException)
        {
            return LiveTestResponse.Failure(id, process,
                new LiveTestError("transport_failed", exception.Message, mutation && writeStarted));
        }
    }

    private void ValidateRegistration(InstanceIdentity identity)
    {
        string path = Path.Combine(Path.GetTempPath(), "BannerlordCoop.LiveTest.v1", identity.RunToken,
            identity.Pid + ".json");
        using var file = File.OpenRead(path);
        if (file.Length > 16384) throw new IOException("Endpoint registration is too large.");
        using var document = JsonDocument.Parse(file);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != LiveTestProtocol.Version ||
            root.GetProperty("pid").GetInt32() != identity.Pid ||
            root.GetProperty("processStartedUtc").GetDateTime() != identity.StartedUtc ||
            root.GetProperty("role").GetString() != identity.Role ||
            root.GetProperty("platformId").GetString() != identity.PlatformId ||
            root.GetProperty("runToken").GetString() != identity.RunToken ||
            root.GetProperty("pipeName").GetString() != LiveTestProtocol.GetPipeName(identity.Pid))
            throw new IOException("Endpoint registration does not match the owned process.");
    }

    public async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) throw new IOException("Pipe closed before a complete response.");
            int newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
            int length = newline >= 0 ? newline : count;
            if (bytes.Length + length > LiveTestProtocol.MaximumMessageBytes)
                throw new IOException("Live-test response exceeds the protocol limit.");
            bytes.Write(buffer, 0, length);
            if (newline >= 0) return new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimEnd('\r');
        }
    }
}
