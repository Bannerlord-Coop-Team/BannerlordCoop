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
}
