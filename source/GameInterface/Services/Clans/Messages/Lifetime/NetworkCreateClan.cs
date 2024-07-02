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
    public ClanCreatedData Data { get; }

    public NetworkCreateClan(ClanCreatedData data)
    {
        Data = data;
    }
}
