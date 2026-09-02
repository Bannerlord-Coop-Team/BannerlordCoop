using Common.LiveTesting;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace VerificationHarness.DedicatedServerSynthetic;

public interface IDedicatedServerControlClient
{
    Task<string> GetStatusAsync(
        int processId,
        string requestId,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed class NamedPipeDedicatedServerControlClient : IDedicatedServerControlClient
{
    public async Task<string> GetStatusAsync(
        int processId,
        string requestId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("A request id is required.", nameof(requestId));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using var pipe = new NamedPipeClientStream(
            ".",
            LiveTestProtocol.GetPipeName(processId),
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeoutSource.Token);

        string requestJson = LiveTestProtocol.SerializeRequest(new LiveTestRequest
        {
            Version = LiveTestProtocol.Version,
            Id = requestId,
            Method = "status",
            Parameters = JsonSerializer.SerializeToElement(new { })
        });
        byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson + "\n");
        await pipe.WriteAsync(requestBytes, timeoutSource.Token);
        await pipe.FlushAsync(timeoutSource.Token);

        return await ReadBoundedUtf8LineAsync(
            pipe,
            LiveTestProtocol.MaximumMessageBytes,
            timeoutSource.Token);
    }

    internal static async Task<string> ReadBoundedUtf8LineAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        using var buffer = new MemoryStream();
        var oneByte = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(oneByte, cancellationToken);
            if (read == 0)
            {
                if (buffer.Length == 0) throw new EndOfStreamException("The control pipe closed before a response.");
                break;
            }

            if (oneByte[0] == (byte)'\n') break;
            if (buffer.Length >= maximumBytes)
            {
                throw new InvalidDataException(
                    $"The control response exceeds {maximumBytes} UTF-8 bytes.");
            }

            buffer.WriteByte(oneByte[0]);
        }

        byte[] bytes = buffer.ToArray();
        if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
        {
            Array.Resize(ref bytes, bytes.Length - 1);
        }

        return new UTF8Encoding(false, true).GetString(bytes);
    }
}

public sealed record DedicatedServerControlExpectation(
    int ProcessId,
    string RunToken,
    string RequestId,
    int JoinPort,
    IReadOnlyCollection<string> ExpectedControllerIds);

public sealed record DedicatedServerRosterEntry(
    string ControllerId,
    string ConnectionInstanceId,
    bool Connected,
    string JoinState);

public sealed record DedicatedServerControlSnapshot(
    bool Serving,
    int JoinPort,
    IReadOnlyList<DedicatedServerRosterEntry> ConnectionRoster);

public sealed class DedicatedServerControlValidation
{
    public bool EnvelopeValid { get; init; }
    public bool RequestIdentityValid { get; init; }
    public bool ProcessIdentityValid { get; init; }
    public bool ServingValid { get; init; }
    public bool JoinPortValid { get; init; }
    public bool RosterSurfaceValid { get; init; }
    public bool ExpectedRosterValid { get; init; }
    public DedicatedServerControlSnapshot? Snapshot { get; init; }
    public List<string> FailureCodes { get; init; } = new();

    public bool IsValid =>
        EnvelopeValid &&
        RequestIdentityValid &&
        ProcessIdentityValid &&
        ServingValid &&
        JoinPortValid &&
        RosterSurfaceValid &&
        ExpectedRosterValid &&
        FailureCodes.Count == 0;
}

public interface IDedicatedServerControlResponseValidator
{
    DedicatedServerControlValidation Validate(
        string responseJson,
        DedicatedServerControlExpectation expectation);
}

public sealed class DedicatedServerControlResponseValidator : IDedicatedServerControlResponseValidator
{
    public DedicatedServerControlValidation Validate(
        string responseJson,
        DedicatedServerControlExpectation expectation)
    {
        if (expectation == null) throw new ArgumentNullException(nameof(expectation));
        var failures = new List<string>();
        if (!LiveTestProtocol.TryDeserializeResponse(responseJson, out LiveTestResponse response, out _))
        {
            failures.Add("invalid-control-envelope");
            return new DedicatedServerControlValidation { FailureCodes = failures };
        }

        bool requestIdentityValid = string.Equals(
            response.Id,
            expectation.RequestId,
            StringComparison.Ordinal);
        if (!requestIdentityValid) failures.Add("request-id-mismatch");

        bool processIdentityValid =
            response.Process.Pid == expectation.ProcessId &&
            string.Equals(response.Process.Role, "server", StringComparison.Ordinal) &&
            string.Equals(response.Process.RunToken, expectation.RunToken, StringComparison.Ordinal);
        if (response.Process.Pid != expectation.ProcessId) failures.Add("process-id-mismatch");
        if (!string.Equals(response.Process.Role, "server", StringComparison.Ordinal))
        {
            failures.Add("process-role-mismatch");
        }
        if (!string.Equals(response.Process.RunToken, expectation.RunToken, StringComparison.Ordinal))
        {
            failures.Add("run-token-mismatch");
        }

        if (!response.Ok)
        {
            failures.Add("control-request-failed");
            return new DedicatedServerControlValidation
            {
                EnvelopeValid = true,
                RequestIdentityValid = requestIdentityValid,
                ProcessIdentityValid = processIdentityValid,
                FailureCodes = failures
            };
        }

        JsonElement result = response.Result is JsonElement element
            ? element
            : JsonSerializer.SerializeToElement(response.Result);
        if (result.ValueKind != JsonValueKind.Object)
        {
            failures.Add("invalid-status-result");
            return new DedicatedServerControlValidation
            {
                EnvelopeValid = true,
                RequestIdentityValid = requestIdentityValid,
                ProcessIdentityValid = processIdentityValid,
                FailureCodes = failures
            };
        }

        bool servingValid =
            result.TryGetProperty("serving", out JsonElement servingElement) &&
            servingElement.ValueKind is JsonValueKind.True;
        if (!servingValid) failures.Add("serving-status-missing-or-false");

        bool joinPortValid =
            result.TryGetProperty("joinPort", out JsonElement joinPortElement) &&
            joinPortElement.TryGetInt32(out int joinPort) &&
            joinPort == expectation.JoinPort;
        if (!joinPortValid) failures.Add("join-port-missing-or-mismatch");

        bool rosterSurfaceValid =
            result.TryGetProperty("connectionRoster", out JsonElement rosterElement) &&
            rosterElement.ValueKind == JsonValueKind.Array;
        var roster = new List<DedicatedServerRosterEntry>();
        if (rosterSurfaceValid)
        {
            foreach (JsonElement item in rosterElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !TryReadBoundedString(item, "controllerId", 1024, out string controllerId) ||
                    !TryReadBoundedString(
                        item,
                        "connectionInstanceId",
                        128,
                        out string connectionInstanceId) ||
                    !item.TryGetProperty("connected", out JsonElement connectedElement) ||
                    connectedElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False ||
                    !TryReadBoundedString(item, "joinState", 256, out string joinState))
                {
                    rosterSurfaceValid = false;
                    failures.Add("invalid-connection-roster-entry");
                    break;
                }

                roster.Add(new DedicatedServerRosterEntry(
                    controllerId,
                    connectionInstanceId,
                    connectedElement.GetBoolean(),
                    joinState));
            }
        }
        else
        {
            // registeredPlayers is only a count and must never be accepted as proof of two peers.
            failures.Add("first-class-connection-roster-missing");
        }

        bool expectedRosterValid = false;
        if (rosterSurfaceValid)
        {
            string[] expected = expectation.ExpectedControllerIds
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            string[] actual = roster
                .Where(x => x.Connected)
                .Select(x => x.ControllerId)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            expectedRosterValid =
                roster.Count == expected.Length &&
                roster.All(x => x.Connected) &&
                roster.Select(x => x.ControllerId).Distinct(StringComparer.Ordinal).Count() == roster.Count &&
                roster.Select(x => x.ConnectionInstanceId).Distinct(StringComparer.Ordinal).Count() == roster.Count &&
                expected.SequenceEqual(actual, StringComparer.Ordinal);
            if (!expectedRosterValid) failures.Add("expected-connection-roster-mismatch");
        }

        DedicatedServerControlSnapshot? snapshot = rosterSurfaceValid
            ? new DedicatedServerControlSnapshot(
                servingValid,
                joinPortValid ? expectation.JoinPort : 0,
                roster.AsReadOnly())
            : null;
        return new DedicatedServerControlValidation
        {
            EnvelopeValid = true,
            RequestIdentityValid = requestIdentityValid,
            ProcessIdentityValid = processIdentityValid,
            ServingValid = servingValid,
            JoinPortValid = joinPortValid,
            RosterSurfaceValid = rosterSurfaceValid,
            ExpectedRosterValid = expectedRosterValid,
            Snapshot = snapshot,
            FailureCodes = failures
        };
    }

    private static bool TryReadBoundedString(
        JsonElement parent,
        string propertyName,
        int maximumBytes,
        out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? candidate = element.GetString();
        if (string.IsNullOrWhiteSpace(candidate) || Encoding.UTF8.GetByteCount(candidate) > maximumBytes)
        {
            return false;
        }

        value = candidate;
        return true;
    }
}

public static class DedicatedServerSecretRedactor
{
    public const string Marker = "[redacted]";

    public static string Redact(string? value, params string?[] secrets)
    {
        string result = value ?? string.Empty;
        foreach (string secret in secrets
                     .Where(x => !string.IsNullOrEmpty(x))
                     .Cast<string>()
                     .Distinct(StringComparer.Ordinal)
                     .OrderByDescending(x => x.Length))
        {
            result = result.Replace(secret, Marker, StringComparison.Ordinal);
        }

        const int maximumEvidenceTextLength = 4096;
        return result.Length <= maximumEvidenceTextLength
            ? result
            : result.Substring(0, maximumEvidenceTextLength);
    }
}
