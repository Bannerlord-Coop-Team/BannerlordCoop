using Common.Logging;
using Common.Network.Coalescing;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Players;

public interface IPlayerCreationRollback
{
    string[] CaptureRegistrationIds(Player player);
    void Rollback(Player player, string[] registrationIds);
}

internal class PlayerCreationRollback : IPlayerCreationRollback
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCreationRollback>();

    private readonly IObjectManager objectManager;
    private readonly ISendCoalescer coalescer;

    public PlayerCreationRollback(IObjectManager objectManager, ISendCoalescer coalescer = null)
    {
        this.objectManager = objectManager;
        this.coalescer = coalescer;
    }

    public string[] CaptureRegistrationIds(Player player)
    {
        if (player == null) return Array.Empty<string>();

        objectManager.TryGetObject(player.HeroId, out Hero hero);
        objectManager.TryGetObject(player.MobilePartyId, out MobileParty party);
        objectManager.TryGetObject(player.ClanId, out Clan clan);
        objectManager.TryGetObject(player.CharacterObjectId, out CharacterObject characterObject);

        return CaptureRegistrations(hero, party, clan, characterObject)
            .Select(registration =>
                objectManager.TryGetId(registration, out var id) ? id : null)
            .Where(id => id != null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public void Rollback(Player player, string[] registrationIds)
    {
        if (player == null) return;

        objectManager.TryGetObject(player.HeroId, out Hero hero);
        objectManager.TryGetObject(player.MobilePartyId, out MobileParty party);
        objectManager.TryGetObject(player.ClanId, out Clan clan);
        objectManager.TryGetObject(player.CharacterObjectId, out CharacterObject characterObject);

        var registrations = ResolveRegistrations(
            registrationIds,
            hero,
            party,
            clan,
            characterObject);
        DropPendingUpdates(registrationIds, registrations);

        if (party != null)
        {
            TryStep("party removal", party.RemoveParty);
            TryStep("party campaign fallback", () => RemovePartyFromCampaign(party));
        }

        if (hero != null)
        {
            TryStep("hero party detach", () => hero.PartyBelongedTo = null);
            TryStep("hero clan detach", () => hero.Clan = null);
            TryStep("hero campaign removal", () => RemoveHeroFromCampaign(hero));
        }

        if (clan != null)
            TryStep("clan campaign removal", () => RemoveClanFromCampaign(clan));

        if (characterObject != null)
            TryStep("character object removal", () => MBObjectManager.Instance?.UnregisterObject(characterObject));

        foreach (var registration in registrations)
            objectManager.Remove(registration);
    }

    private void DropPendingUpdates(IEnumerable<string> registrationIds, IEnumerable<object> registrations)
    {
        foreach (var id in registrationIds ?? Array.Empty<string>())
            coalescer?.DropInstance(id);

        foreach (var registration in registrations)
        {
            if (!objectManager.TryGetId(registration, out var id)) continue;

            coalescer?.DropInstance(
                global::GameInterface.Services.ObjectManager.ObjectManager.Compact(id, registration.GetType()));
        }
    }

    private IReadOnlyList<object> ResolveRegistrations(
        IEnumerable<string> registrationIds,
        Hero hero,
        MobileParty party,
        Clan clan,
        CharacterObject characterObject)
    {
        var registrations = CaptureRegistrations(hero, party, clan, characterObject).ToList();

        foreach (var id in registrationIds ?? Array.Empty<string>())
        {
            if (objectManager.TryGetObject(id, out object registration))
                AddIfPresent(registrations, registration);
        }

        return registrations;
    }

    private static IReadOnlyList<object> CaptureRegistrations(
        Hero hero,
        MobileParty party,
        Clan clan,
        CharacterObject characterObject)
    {
        var registrations = new List<object>();

        CaptureIfPresent(registrations, "item roster", () => party?.ItemRoster);
        CaptureIfPresent(registrations, "member roster", () => party?.MemberRoster);
        CaptureIfPresent(registrations, "prison roster", () => party?.PrisonRoster);
        CaptureIfPresent(registrations, "party base", () => party?.Party);
        CaptureIfPresent(registrations, "hero developer", () => hero?.HeroDeveloper);
        AddIfPresent(registrations, characterObject);
        AddIfPresent(registrations, party);
        AddIfPresent(registrations, hero);
        AddIfPresent(registrations, clan);

        return registrations;
    }

    private static void AddIfPresent(ICollection<object> registrations, object registration)
    {
        if (registration != null && !registrations.Contains(registration))
            registrations.Add(registration);
    }

    private static void CaptureIfPresent(
        ICollection<object> registrations,
        string registrationName,
        Func<object> resolve)
    {
        try
        {
            AddIfPresent(registrations, resolve());
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to capture {Registration} during player creation rollback", registrationName);
        }
    }

    private static void RemovePartyFromCampaign(MobileParty party)
    {
        var campaign = Campaign.Current;
        if (campaign?.MobileParties?.Contains(party) != true) return;

        campaign.MobilePartyLocator?.RemoveLocatable(party);
        campaign.VisualTrackerManager?.RemoveTrackedObject(party, true);
        campaign.CampaignObjectManager.RemoveMobileParty(party);
    }

    private static void RemoveHeroFromCampaign(Hero hero)
    {
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        if (campaignObjectManager == null) return;

        if (Hero.AllAliveHeroes.Contains(hero))
            hero.ChangeState(Hero.CharacterStates.Disabled);

        if (Hero.DeadOrDisabledHeroes.Contains(hero))
            campaignObjectManager.UnregisterDeadHero(hero);
    }

    private static void RemoveClanFromCampaign(Clan clan)
    {
        var campaign = Campaign.Current;
        var campaignObjectManager = campaign?.CampaignObjectManager;
        if (campaignObjectManager == null) return;

        var factionManager = campaign.FactionManager;
        var stances = factionManager?._stances?
            .GetStanceLinks()
            .Where(stance => stance.Faction1 == clan || stance.Faction2 == clan)
            .ToArray() ?? Array.Empty<StanceLink>();

        foreach (var stance in stances)
            factionManager._stances.RemoveStance(stance);

        var affectedFactions = stances
            .SelectMany(stance => new[] { stance.Faction1, stance.Faction2 })
            .Where(faction => faction != clan)
            .Distinct()
            .ToArray();

        if (campaignObjectManager.Clans.Contains(clan))
            campaignObjectManager.RemoveClan(clan);

        // Update after removing the failed clan from Clan.All so these cache rebuilds cannot recreate
        // a neutral stance against it through FactionManager.GetStanceLinkInternal.
        foreach (var faction in affectedFactions)
        {
            faction.UpdateFactionsAtWarWith();
        }
    }

    private static void TryStep(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Player creation rollback step {Step} failed", step);
        }
    }
}
