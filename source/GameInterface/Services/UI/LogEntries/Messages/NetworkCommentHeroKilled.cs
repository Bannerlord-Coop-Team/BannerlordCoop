using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.UI.LogEntries.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommentHeroKilled : ICommand
{
    [ProtoMember(1)]
    public readonly string VictimId;

    [ProtoMember(2)]
    public readonly string KillerId;

    [ProtoMember(3)]
    public readonly KillCharacterAction.KillCharacterActionDetail Detail;

    public NetworkCommentHeroKilled(
        string victimId,
        string killerId,
        KillCharacterAction.KillCharacterActionDetail detail)
    {
        VictimId = victimId;
        KillerId = killerId;
        Detail = detail;
    }
}
