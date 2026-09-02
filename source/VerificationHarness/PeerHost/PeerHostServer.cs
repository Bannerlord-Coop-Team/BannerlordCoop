using System.Text;
using System.Text.Json;
using VerificationHarness.Serialization;

namespace VerificationHarness.PeerHost;

public interface IPeerHostServer
{
    Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        string instanceId,
        int processId,
        CancellationToken cancellationToken);
}

public sealed class PeerHostServer : IPeerHostServer
{
    public const int CurrentProtocolVersion = 1;
    public const int InvalidRequestExitCode = 4;

    internal const int MaximumLineLength = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ICanonicalJsonHasher canonicalJsonHasher;

    public PeerHostServer()
        : this(new CanonicalJsonHasher())
    {
    }

    public PeerHostServer(ICanonicalJsonHasher canonicalJsonHasher)
    {
        if (canonicalJsonHasher == null) throw new ArgumentNullException(nameof(canonicalJsonHasher));
        this.canonicalJsonHasher = canonicalJsonHasher;
    }

    public async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        string instanceId,
        int processId,
        CancellationToken cancellationToken)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (output == null) throw new ArgumentNullException(nameof(output));
        ValidateInstanceId(instanceId);
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));

        long nextSequence = 1;
        bool handshakeComplete = false;
        var lineReader = new BoundedLineReader(input);
        var state = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var counters = new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["get"] = 0,
            ["hello"] = 0,
            ["ping"] = 0,
            ["put"] = 0,
            ["shutdown"] = 0,
            ["snapshot"] = 0
        };

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string? line, bool exceededMaximumLength) = await lineReader.ReadAsync(cancellationToken);
            if (exceededMaximumLength)
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "invalid-frame",
                    "Request line exceeds 65536 characters.");
            }

            if (line == null)
            {
                return 0;
            }

            if (line.Length == 0)
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "invalid-frame",
                    "Request lines cannot be empty.");
            }

            PeerHostRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<PeerHostRequest>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "invalid-json",
                    ex.Message);
            }

            if (request == null)
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "invalid-request",
                    "Request must be a JSON object.");
            }

            if (request.ProtocolVersion != CurrentProtocolVersion)
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "unsupported-protocol",
                    $"Expected protocolVersion {CurrentProtocolVersion}.");
            }

            if (request.Sequence != nextSequence)
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "invalid-sequence",
                    $"Expected sequence {nextSequence}.");
            }

            if (string.IsNullOrWhiteSpace(request.Command))
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "missing-command",
                    "Command is required.");
            }

            string command = request.Command;
            if (!handshakeComplete && !string.Equals(command, "hello", StringComparison.Ordinal))
            {
                return await WriteFailure(
                    output,
                    instanceId,
                    processId,
                    nextSequence,
                    "handshake-required",
                    "The first request must be hello.");
            }

            object? result;
            switch (command)
            {
                case "hello":
                    if (handshakeComplete)
                    {
                        return await WriteFailure(
                            output,
                            instanceId,
                            processId,
                            nextSequence,
                            "duplicate-handshake",
                            "The hello handshake can only be sent once.");
                    }

                    handshakeComplete = true;
                    counters["hello"]++;
                    result = new
                    {
                        capabilities = new[] { "get", "ping", "put", "shutdown", "snapshot" }
                    };
                    break;

                case "ping":
                    counters["ping"]++;
                    result = new
                    {
                        payload = request.Payload.ValueKind == JsonValueKind.Undefined
                            ? (JsonElement?)null
                            : request.Payload.Clone()
                    };
                    break;

                case "put":
                    if (!TryReadStatePayload(request.Payload, requireValue: true, out string? putKey, out JsonElement putValue, out string? putError))
                    {
                        return await WriteFailure(
                            output,
                            instanceId,
                            processId,
                            nextSequence,
                            "invalid-payload",
                            putError!);
                    }

                    state[putKey!] = putValue;
                    counters["put"]++;
                    result = new { key = putKey };
                    break;

                case "get":
                    if (!TryReadStatePayload(request.Payload, requireValue: false, out string? getKey, out _, out string? getError))
                    {
                        return await WriteFailure(
                            output,
                            instanceId,
                            processId,
                            nextSequence,
                            "invalid-payload",
                            getError!);
                    }

                    bool found = state.TryGetValue(getKey!, out JsonElement storedValue);
                    counters["get"]++;
                    result = new
                    {
                        key = getKey,
                        found,
                        value = found ? storedValue : (JsonElement?)null
                    };
                    break;

                case "snapshot":
                    counters["snapshot"]++;
                    var snapshotFields = new PeerHostSnapshotFields(
                        instanceId,
                        nextSequence,
                        "ready",
                        new SortedDictionary<string, long>(counters, StringComparer.Ordinal),
                        new SortedDictionary<string, JsonElement>(state, StringComparer.Ordinal));
                    result = new PeerHostSnapshotResult(
                        canonicalJsonHasher.ComputeSha256(snapshotFields),
                        snapshotFields);
                    break;

                case "shutdown":
                    counters["shutdown"]++;
                    await WriteSuccess(output, instanceId, processId, nextSequence, new { shuttingDown = true });
                    return 0;

                default:
                    return await WriteFailure(
                        output,
                        instanceId,
                        processId,
                        nextSequence,
                        "unknown-command",
                        $"Unknown command: {command}");
            }

            await WriteSuccess(output, instanceId, processId, nextSequence, result);
            nextSequence++;
        }
    }

    private sealed class BoundedLineReader
    {
        private readonly TextReader input;
        private readonly char[] readBuffer = new char[1];
        private bool skipLeadingLineFeed;

        public BoundedLineReader(TextReader input)
        {
            this.input = input;
        }

        public async Task<(string? Line, bool ExceededMaximumLength)> ReadAsync(
            CancellationToken cancellationToken)
        {
            var line = new StringBuilder(256, MaximumLineLength);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int charactersRead = await input.ReadAsync(readBuffer.AsMemory(0, 1), cancellationToken);
                if (charactersRead == 0)
                {
                    return line.Length == 0
                        ? (null, false)
                        : (line.ToString(), false);
                }

                char character = readBuffer[0];
                if (skipLeadingLineFeed)
                {
                    skipLeadingLineFeed = false;
                    if (character == '\n')
                    {
                        continue;
                    }
                }

                if (character == '\n')
                {
                    return (line.ToString(), false);
                }

                if (character == '\r')
                {
                    skipLeadingLineFeed = true;
                    return (line.ToString(), false);
                }

                if (line.Length == MaximumLineLength)
                {
                    return (null, true);
                }

                line.Append(character);
            }
        }
    }

    private static bool TryReadStatePayload(
        JsonElement payload,
        bool requireValue,
        out string? key,
        out JsonElement value,
        out string? error)
    {
        key = null;
        value = default;
        error = null;

        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("key", out JsonElement keyElement) ||
            keyElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(keyElement.GetString()))
        {
            error = "Payload requires a non-empty string key.";
            return false;
        }

        key = keyElement.GetString();
        if (!requireValue)
        {
            return true;
        }

        if (!payload.TryGetProperty("value", out JsonElement valueElement))
        {
            error = "Put payload requires a value.";
            return false;
        }

        value = valueElement.Clone();
        return true;
    }

    private static async Task WriteSuccess(
        TextWriter output,
        string instanceId,
        int processId,
        long sequence,
        object? result)
    {
        var response = new PeerHostResponse(instanceId, processId, sequence, "ok", result, null);
        await WriteResponse(output, response);
    }

    private static async Task<int> WriteFailure(
        TextWriter output,
        string instanceId,
        int processId,
        long sequence,
        string code,
        string message)
    {
        var response = new PeerHostResponse(
            instanceId,
            processId,
            sequence,
            "error",
            null,
            new PeerHostError(code, message));
        await WriteResponse(output, response);
        return InvalidRequestExitCode;
    }

    private static async Task WriteResponse(TextWriter output, PeerHostResponse response)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
        await output.FlushAsync();
    }

    private static void ValidateInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || instanceId.Length > 64)
        {
            throw new ArgumentException("Instance id must contain between 1 and 64 characters.", nameof(instanceId));
        }

        if (instanceId.Any(character =>
                !char.IsLetterOrDigit(character) &&
                character != '.' &&
                character != '_' &&
                character != '-'))
        {
            throw new ArgumentException("Instance id may only contain letters, digits, period, underscore, and hyphen.", nameof(instanceId));
        }
    }
}
