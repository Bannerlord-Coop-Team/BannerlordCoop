using Common.Util;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Data;

/// <summary>
/// The per-leader party commands v1.5 added to the clan screen (they replace the party objective).
/// Each one is a <see cref="Hero"/> property backed by its party configuration.
/// </summary>
public enum PartyConfigurationFlag
{
    CanJoinArmy,
    CanRaid,
    CanDonateTroopsToGarrison,
    CanHaveFleet,
}

public static class PartyConfigurationFlags
{
    public static bool Get(Hero hero, PartyConfigurationFlag flag) => flag switch
    {
        PartyConfigurationFlag.CanJoinArmy => hero.CanJoinArmy,
        PartyConfigurationFlag.CanRaid => hero.CanRaid,
        PartyConfigurationFlag.CanDonateTroopsToGarrison => hero.CanDonateTroopsToGarrison,
        PartyConfigurationFlag.CanHaveFleet => hero.CanHaveFleet,
        _ => throw new ArgumentOutOfRangeException(nameof(flag), flag, null),
    };

    public static void Set(Hero hero, PartyConfigurationFlag flag, bool value)
    {
        switch (flag)
        {
            case PartyConfigurationFlag.CanJoinArmy: hero.CanJoinArmy = value; break;
            case PartyConfigurationFlag.CanRaid: hero.CanRaid = value; break;
            case PartyConfigurationFlag.CanDonateTroopsToGarrison: hero.CanDonateTroopsToGarrison = value; break;
            case PartyConfigurationFlag.CanHaveFleet: hero.CanHaveFleet = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(flag), flag, null);
        }
    }

    /// <summary>
    /// Server only: <see cref="Hero.ResetPartyConfiguration"/>, which nulls a field that is not synced,
    /// done through the synced flags first so that clients get the defaults too.
    /// </summary>
    public static void Reset(Hero hero)
    {
        // Clients never run the vanilla reset, so publish even inside a replicated action's scope.
        using (AllowedThread.Suspend())
        {
            foreach (PartyConfigurationFlag flag in Enum.GetValues(typeof(PartyConfigurationFlag)))
                Set(hero, flag, true);
        }
        hero.ResetPartyConfiguration();
    }
}
