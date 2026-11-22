using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyCleanHideoutRelations : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly int RelationValue;

    public NetworkNotifyCleanHideoutRelations(string playerHeroId, int relationValue)
    {
        PlayerHeroId = playerHeroId;
        RelationValue = relationValue;
    }
}
