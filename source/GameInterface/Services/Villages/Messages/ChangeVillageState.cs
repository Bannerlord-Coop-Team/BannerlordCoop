using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Villages.Messages;


/// <summary>
/// TODO
/// </summary>
public record ChangeVillageState : ICommand
{
    public string SettlementId { get; }
    public int State { get; }

    public ChangeVillageState(string settlementId, int state)
    {
        SettlementId = settlementId;
        State = state;
    }
}