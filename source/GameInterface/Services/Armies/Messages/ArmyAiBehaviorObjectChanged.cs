using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a Army.AiBehaviorObject changed
/// </summary>
public readonly struct ArmyAiBehaviorObjectChanged : IEvent
{
    public readonly Army Army;
    public readonly IMapPoint AiBehaviorObject;

    public ArmyAiBehaviorObjectChanged(Army army, IMapPoint aiBehaviorObject)
    {
        Army = army;
        AiBehaviorObject = aiBehaviorObject;
    }
}
