using System.Text.Json;

namespace VerificationHarness.PeerHost;

public sealed class PeerHostRequest
{
    public int ProtocolVersion { get; set; }
    public long Sequence { get; set; }
    public string? Command { get; set; }
    public JsonElement Payload { get; set; }
}

public sealed class PeerHostResponse
{
    public int ProtocolVersion { get; }
    public string InstanceId { get; }
    public int ProcessId { get; }
    public long Sequence { get; }
    public string Status { get; }
    public object? Result { get; }
    public PeerHostError? Error { get; }

    public PeerHostResponse(
        string instanceId,
        int processId,
        long sequence,
        string status,
        object? result,
        PeerHostError? error)
    {
        ProtocolVersion = PeerHostServer.CurrentProtocolVersion;
        InstanceId = instanceId;
        ProcessId = processId;
        Sequence = sequence;
        Status = status;
        Result = result;
        Error = error;
    }
}

public sealed class PeerHostError
{
    public string Code { get; }
    public string Message { get; }

    public PeerHostError(string code, string message)
    {
        Code = code;
        Message = message;
    }
}
