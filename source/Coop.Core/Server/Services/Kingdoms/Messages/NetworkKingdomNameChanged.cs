using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages;

/// <summary>
/// Notifies the requesting client that its kingdom rename was applied
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkKingdomNameChanged : ICommand
{
    [ProtoMember(1)]
    public string KingdomId { get; }

    public NetworkKingdomNameChanged(string kingdomId)
    {
        KingdomId = kingdomId;
    }
}