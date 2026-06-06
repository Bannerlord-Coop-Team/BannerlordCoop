using Common.Logging;
using Common.Messaging;
using ProtoBuf;
using Serilog;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Response to <see cref="NetworkModuleVersionsValidate"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkModuleVersionsValidated : IResponse
{
    [ProtoMember(1)]
    public bool Matches { get; }
    [ProtoMember(2)]
    public string Reason { get; }

    public NetworkModuleVersionsValidated(bool matches, string reason)
    {
        Matches = matches;
        Reason = reason;
    }
}
