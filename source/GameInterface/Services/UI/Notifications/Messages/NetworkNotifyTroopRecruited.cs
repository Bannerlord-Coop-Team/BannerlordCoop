using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNotifyTroopRecruited : ICommand
{
    [ProtoMember(1)]
    public readonly string RecruiterHeroId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    [ProtoMember(3)]
    public readonly string TroopSourceHeroId;

    [ProtoMember(4)]
    public readonly string TroopId;

    [ProtoMember(5)]
    public readonly int Amount;

    public NetworkNotifyTroopRecruited(string recruiterHeroId, string settlementId, string troopSourceHeroId, string troopId, int amount)
    {
        RecruiterHeroId = recruiterHeroId;
        SettlementId = settlementId;
        TroopSourceHeroId = troopSourceHeroId;
        TroopId = troopId;
        Amount = amount;
    }
}
