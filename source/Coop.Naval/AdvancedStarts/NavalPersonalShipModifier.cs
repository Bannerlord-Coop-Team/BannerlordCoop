using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart;
using NavalDLC.CampaignBehaviors;
using SandBox.AdvancedStartOptions;
using System;
using TaleWorlds.CampaignSystem;
using StartOptionIds = TaleWorlds.CampaignSystem.AdvancedStartOptions.DefaultAdvancedStartOptions;

namespace Coop.Naval.AdvancedStarts;

/// <summary>War Sails' personal ship, a trade ship a joining player can tick on top of any start.</summary>
internal sealed class NavalPersonalShipModifier : IAdvancedStartModifier
{
    public string OptionId => StartOptionIds.PersonalShipModifierId;

    // An on/off option, which a missing key offers.
    public int GetLimit(AdvancedStartConfigData config) => config?.PersonalShip != false ? 1 : 0;

    // The window shows War Sails' own option.
    public AdvancedStartOption CreateOption(int limit) => null;

    public void Run(Hero hero, int value)
    {
        var behavior = Campaign.Current.GetCampaignBehavior<NavalAdvancedStartingPlayerOptionsCampaignBehavior>();
        if (behavior == null)
            throw new InvalidOperationException($"No {nameof(NavalAdvancedStartingPlayerOptionsCampaignBehavior)} to give the personal ship");

        behavior.GivePersonalShip();
    }
}
