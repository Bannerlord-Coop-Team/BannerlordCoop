using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyCaughtIllness : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    public NetworkNotifyCaughtIllness(string playerHeroId)
    {
        PlayerHeroId = playerHeroId;
    }
}
