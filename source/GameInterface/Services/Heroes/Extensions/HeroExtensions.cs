using Common;
using Common.Logging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.Players;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Extensions;

public static class HeroExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    /// <summary>
    /// Checks to see if the Hero is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayerHero(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        return PlayerManager.TryGetControlledObjectInfo(hero, out var _);
    }

    /// <summary>A player governor counts as present in the town they govern, wherever they are.</summary>
    public static Settlement GetGovernorSettlement(this Hero hero)
    {
        if (hero.IsPlayerHero() && hero.GovernorOf != null) return hero.GovernorOf.Settlement;

        return hero.CurrentSettlement;
    }

    /// <summary>
    /// The most mercenaries <paramref name="hero"/> may hire from a town stock of
    /// <paramref name="availableMercenaries"/>. A generous hero may hire beyond the stock, as the vanilla menu and tavern dialog allow.
    /// </summary>
    public static int GetMercenaryHireLimit(this Hero hero, int availableMercenaries)
    {
        var limit = new ExplainedNumber(availableMercenaries);
        TraitEffectHelper.ApplyTraitEffect(hero, DefaultPersonalityTraitEffects.GenerosityMercenaryRecruitmentEffect, ref limit);
        return limit.RoundedResultNumber;
    }

    /// <summary>
    /// Check to see if the Hero is controlled by this client or server.
    /// </summary>
    /// <param name="hero">Hero to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsControlledByThisInstance(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        if (ModInformation.IsServer)
        {
            // Server controls all non-player objects
            return !PlayerManager.TryGetControlledObjectInfo(hero, out var _);
        }

        if (!PlayerManager.TryGetControlledObjectInfo(hero, out var controlledObjectInfo))
            return false;

        return controlledObjectInfo.IsControlled;
    }

    /// <summary>
    /// Determine Whether this client owns a hero's health.
    /// Use BattleSpawnGate.HeroAgentAuthorityProbe to determine if client has authority.
    /// </summary>
    public static bool IsHealthControlledByThisInstance(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        // Determine if this client has control of the hero from the mission layer
        var agentAuthority = BattleSpawnGate.HeroAgentAuthorityProbe?.Invoke(hero);
        if (agentAuthority.HasValue) return agentAuthority.Value;

        // Fallback: Hero is this player's character
        if (hero.IsControlledByThisInstance()) return true;

        // Location fights (such as alleys) use the local LocationEncounter instead of PlayerEncounter.Current
        if (HasLocationEncounterHealthAuthority(
            PlayerEncounter.LocationEncounter != null,
            hero.PartyBelongedTo == MobileParty.MainParty)) return true;

        // Allow control for solo encounters where agent authority doesn't exist
        // A solo encounter is where this client is the only involved player
        if (PlayerEncounter.Current != null && PlayerEncounter.Current.IsSoloEncounter()) return true;

        return false;
    }

    private static bool HasLocationEncounterHealthAuthority(bool hasLocationEncounter, bool belongsToMainParty)
        => hasLocationEncounter && belongsToMainParty;
}
