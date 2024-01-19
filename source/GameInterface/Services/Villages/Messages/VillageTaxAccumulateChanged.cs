using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Villages.Messages;

public record VillageTaxAccumulateChanged : ICommand
{
    public string VilageId { get; }

    public int TradeTaxAccumulated { get; }

    public VillageTaxAccumulateChanged(string vilageId, int tradeTaxAccumulated)
    {
        VilageId = vilageId;
        TradeTaxAccumulated = tradeTaxAccumulated;
    }
}
