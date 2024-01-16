using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// TODO update summary
/// A command changes the state of something
/// </summary>
public record VillageStateChange : ICommand
{
    public Village VillageChanged { get; }

    public VillageStateChange(Village villageChanged)
    {
        VillageChanged = villageChanged;
    }
}
