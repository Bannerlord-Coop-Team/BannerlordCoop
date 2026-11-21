using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

/// <summary>
/// Sent to the player whose join the server refused because the kingdom has maxClansPerKingdom clans.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkKingdomFull : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    public NetworkKingdomFull(string kingdomId)
    {
        KingdomId = kingdomId;
    }
}
