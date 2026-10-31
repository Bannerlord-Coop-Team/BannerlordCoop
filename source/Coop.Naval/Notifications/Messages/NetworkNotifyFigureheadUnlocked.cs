using Common.Messaging;
using ProtoBuf;

namespace Coop.Naval.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyFigureheadUnlocked : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string FigureheadId;

    public NetworkNotifyFigureheadUnlocked(
        string playerHeroId,
        string figureheadId)
    {
        PlayerHeroId = playerHeroId;
        FigureheadId = figureheadId;
    }
}
