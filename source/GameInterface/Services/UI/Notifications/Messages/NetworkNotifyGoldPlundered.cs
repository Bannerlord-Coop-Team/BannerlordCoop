using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyGoldPlundered : ICommand
{
    [ProtoMember(1)]
    public readonly string LeaderHeroId;

    [ProtoMember(2)]
    public readonly int PlunderedGold;

    public NetworkNotifyGoldPlundered(string leaderHeroId, int plunderedGold)
    {
        LeaderHeroId = leaderHeroId;
        PlunderedGold = plunderedGold;
    }
}
