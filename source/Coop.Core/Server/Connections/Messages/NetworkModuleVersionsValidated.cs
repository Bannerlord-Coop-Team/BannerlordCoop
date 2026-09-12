using Common;
using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Response to <see cref="NetworkModuleVersionsValidate"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkModuleVersionsValidated : IEvent
{
    [ProtoMember(1)]
    public bool Matches { get; }
    [ProtoMember(2)]
    public string? Reason { get; }
    [ProtoMember(3)]
    public string? CoopBuildVersion { get; }

    public NetworkModuleVersionsValidated(bool matches, string? reason)
        : this(matches, reason, ModInformation.BuildVersion)
    {
    }

    public NetworkModuleVersionsValidated(bool matches, string? reason, string? coopBuildVersion)
    {
        Matches = matches;
        Reason = reason;
        CoopBuildVersion = coopBuildVersion;
    }
}
