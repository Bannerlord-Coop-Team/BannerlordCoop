using Common.Util;
using GameInterface.Services.Clans.Data;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

internal interface IPartyConfigurationService
{
    bool Get(Hero hero, PartyConfigurationFlag flag);
    void Set(Hero hero, PartyConfigurationFlag flag, bool value);

    /// <summary>
    /// Server only: <see cref="Hero.ResetPartyConfiguration"/> nulls a field that is not synced,
    /// so the synced flags are reset first and clients get the defaults too.
    /// </summary>
    void Reset(Hero hero);
}

internal class PartyConfigurationService : IPartyConfigurationService
{
    public bool Get(Hero hero, PartyConfigurationFlag flag) => flag switch
    {
        PartyConfigurationFlag.CanJoinArmy => hero.CanJoinArmy,
        PartyConfigurationFlag.CanRaid => hero.CanRaid,
        PartyConfigurationFlag.CanDonateTroopsToGarrison => hero.CanDonateTroopsToGarrison,
        PartyConfigurationFlag.CanHaveFleet => hero.CanHaveFleet,
        _ => throw new ArgumentOutOfRangeException(nameof(flag), flag, null),
    };

    public void Set(Hero hero, PartyConfigurationFlag flag, bool value)
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

    public void Reset(Hero hero)
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
