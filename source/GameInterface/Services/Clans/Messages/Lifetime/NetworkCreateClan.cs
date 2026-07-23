using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Network message to command the creation of a new clan
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateClan : ICommand
{
    [ProtoMember(1)]
    public string ClanId { get; }

    public NetworkCreateClan(string clanId)
    {
        ClanId = clanId;
    }
}
