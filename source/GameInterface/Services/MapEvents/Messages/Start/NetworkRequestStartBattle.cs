using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestStartBattle : ICommand
{
    public readonly string AttackerId;
    public readonly string DefenderId;
    public readonly string SettlementId;
    public readonly MapEvent.BattleTypes BattleType;

    public NetworkRequestStartBattle(string attackerId, string defenderId, string settlementId, MapEvent.BattleTypes battleType)
    {
        AttackerId = attackerId;
        DefenderId = defenderId;
        SettlementId = settlementId;
        BattleType = battleType;
    }
}
