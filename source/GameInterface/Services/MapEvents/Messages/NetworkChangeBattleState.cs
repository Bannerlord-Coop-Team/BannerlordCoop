using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract]
public readonly struct NetworkChangeBattleState : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly BattleState BattleState;

    public NetworkChangeBattleState(string mapEventId, BattleState battleState)
    {
        MapEventId = mapEventId;
        BattleState = battleState;
    }
}
