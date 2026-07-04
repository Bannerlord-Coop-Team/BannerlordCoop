using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNotifyTroopGivenToSettlement : ICommand
{
    [ProtoMember(1)]
    public readonly string GiverHeroId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    [ProtoMember(3)]
    public readonly TroopRosterData Troops;

    public NetworkNotifyTroopGivenToSettlement(string giverHeroId, string settlementId, TroopRosterData troops)
    {
        GiverHeroId = giverHeroId;
        SettlementId = settlementId;
        Troops = troops;
    }
}
