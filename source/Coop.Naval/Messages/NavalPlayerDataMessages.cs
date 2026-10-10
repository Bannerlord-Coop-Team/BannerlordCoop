using Common.Messaging;
using ProtoBuf;

namespace Coop.Naval.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkInitializeServerNavalDataKeys : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    public NetworkInitializeServerNavalDataKeys(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}
