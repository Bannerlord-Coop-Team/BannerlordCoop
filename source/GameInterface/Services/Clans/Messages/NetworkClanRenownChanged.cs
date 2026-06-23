using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkClanRenownChanged : ICommand
{
    [ProtoMember(1)]
    public string ClanId { get; }
    [ProtoMember(2)]
    public float Renown { get; }

    public NetworkClanRenownChanged(string clanId, float renown)
    {
        ClanId = clanId;
        Renown = renown;
    }
}
