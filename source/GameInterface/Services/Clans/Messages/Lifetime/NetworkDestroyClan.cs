using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages.Lifetime;

/// <summary>
/// Network message to command the destruction of a clan
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyClan : ICommand
{
    [ProtoMember(1)]
    public string ClanId { get; }
    public int Details { get; }

    public NetworkDestroyClan(string clanId, int details)
    {
        ClanId = clanId;
        Details = details;
    }
}
