using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Messages;

public readonly struct UpdateOtherOptions : IEvent { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateOtherOptions : ICommand
{
    [ProtoMember(1)]
    public readonly int PlayerReceivedDamageDifficulty;

    public NetworkUpdateOtherOptions(
        int playerReceivedDamageDifficulty)
    {
        PlayerReceivedDamageDifficulty = playerReceivedDamageDifficulty;
    }
}