using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Declare war approved by server
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkDeclareWarApproved : ICommand
{
    [ProtoMember(1)]
    public string Faction1Id { get; }
    [ProtoMember(2)]
    public string Faction2Id { get; }
    [ProtoMember(3)]
    public int Detail { get; }

    public NetworkDeclareWarApproved(string faction1Id, string faction2Id, int detail)
    {
        Faction1Id = faction1Id;
        Faction2Id = faction2Id;
        Detail = detail;
    }
}
