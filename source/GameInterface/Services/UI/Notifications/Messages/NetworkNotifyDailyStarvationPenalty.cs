using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyDailyStarvationPenalty : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly int DailyStarvationMoralePenalty;

    public NetworkNotifyDailyStarvationPenalty(
        string mobilePartyId,
        int dailyStarvationMoralePenalty)
    {
        MobilePartyId = mobilePartyId;
        DailyStarvationMoralePenalty = dailyStarvationMoralePenalty;
    }
}
