using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Village;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// TODO update summary
/// A command changes the state of something
/// </summary>
public record VillageChangeState : ICommand
{
    public Village VillageChange { get; }

    public VillageChangeState(Village villageChange)
    {
        VillageChange = villageChange;
    }
}
