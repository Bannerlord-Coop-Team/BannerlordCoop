using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Save.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Save.Handlers;

/// <summary>
/// Handles Coop specific saving and loading
/// </summary>
internal class SaveGameHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SaveGameHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly ICoopSaveManager saveManager;
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IPlayerManager playerRegistry;
    private readonly IPlayerPartyRestorer playerPartyRestorer;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly HashSet<object> activeSaveSources = new HashSet<object>();

    public SaveGameHandler(
        IMessageBroker messageBroker,
        ICoopSaveManager saveManager,
        ICoopSessionProvider coopSessionProvider,
        IPlayerManager playerRegistry,
        IPlayerPartyRestorer playerPartyRestorer,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.saveManager = saveManager;
        this.coopSessionProvider = coopSessionProvider;
        this.playerRegistry = playerRegistry;
        this.playerPartyRestorer = playerPartyRestorer;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);
        messageBroker.Subscribe<GameSaveStateChanged>(Handle_GameSaveStateChanged);

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameSaved>(Handle_GameSaved);
        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);
        messageBroker.Unsubscribe<GameSaveStateChanged>(Handle_GameSaveStateChanged);

        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    private void Handle_GameSaveStateChanged(MessagePayload<GameSaveStateChanged> payload)
    {
        lock (activeSaveSources)
        {
            if (payload.What.IsSaving)
            {
                if (!activeSaveSources.Add(payload.Who) || activeSaveSources.Count != 1)
                    return;
            }
            else if (!activeSaveSources.Remove(payload.Who) || activeSaveSources.Count != 0)
            {
                return;
            }

            network.SendAll(new NetworkGameSaveStateChanged(payload.What.IsSaving));
        }
    }

    private void Handle_GameSaved(MessagePayload<GameSaved> obj)
    {
        var saveName = obj.What.SaveName;
        try
        {
            WriteCoopSession(saveName);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to write the co-op session for save {SaveName}", saveName);
            messageBroker.Publish(this, new CoopSessionWritten(saveName, false));
            return;
        }

        messageBroker.Publish(this, new CoopSessionWritten(saveName, true));
    }

    private void WriteCoopSession(string saveName)
    {
        var current = coopSessionProvider.CoopSession;
        var empty = CoopSession.Empty;

        CoopSession session = new CoopSession(
            saveName,
            playerRegistry.Players.ToArray(),
            current?.CraftingPlayerData ?? empty.CraftingPlayerData,
            current?.WorkshopPlayerData ?? empty.WorkshopPlayerData,
            current?.CaravansPlayerData ?? empty.CaravansPlayerData,
            current?.AlleyPlayerData ?? empty.AlleyPlayerData,
            current?.InteractionsPlayerData ?? empty.InteractionsPlayerData,
            current?.TradePlayerData ?? empty.TradePlayerData,
            current?.InventoryPlayerData ?? empty.InventoryPlayerData,
            current?.HeroMeetingData ?? empty.HeroMeetingData,
            current?.AgingPlayerData ?? empty.AgingPlayerData,
            current?.NavalPlayerData ?? empty.NavalPlayerData,
            current?.ClanFinance ?? empty.ClanFinance,
            current?.PlayerTraitData ?? empty.PlayerTraitData,
            current?.OrderOfBattlePlayerData ?? empty.OrderOfBattlePlayerData,
            current?.StatisticsPlayerData ?? empty.StatisticsPlayerData,
            current?.IncidentsPlayerData ?? empty.IncidentsPlayerData,
            current?.ViewDataPlayerData ?? empty.ViewDataPlayerData);

        coopSessionProvider.CoopSession = session;

        saveManager.SaveCoopSession(saveName, session);
    }

    private ICoopSession savedSession;
    private void Handle_GameLoaded(MessagePayload<GameLoaded> obj)
    {
        var loaded = saveManager.LoadCoopSession(obj.What.SaveName);
        var empty = CoopSession.Empty;

        savedSession = new CoopSession(
            loaded?.UniqueGameId ?? obj.What.SaveName,
            loaded?.Players ?? empty.Players,
            loaded?.CraftingPlayerData ?? empty.CraftingPlayerData,
            loaded?.WorkshopPlayerData ?? empty.WorkshopPlayerData,
            loaded?.CaravansPlayerData ?? empty.CaravansPlayerData,
            loaded?.AlleyPlayerData ?? empty.AlleyPlayerData,
            loaded?.InteractionsPlayerData ?? empty.InteractionsPlayerData,
            loaded?.TradePlayerData ?? empty.TradePlayerData,
            loaded?.InventoryPlayerData ?? empty.InventoryPlayerData,
            loaded?.HeroMeetingData ?? empty.HeroMeetingData,
            loaded?.AgingPlayerData ?? empty.AgingPlayerData,
            loaded?.NavalPlayerData ?? empty.NavalPlayerData,
            loaded?.ClanFinance ?? empty.ClanFinance,
            loaded?.PlayerTraitData ?? empty.PlayerTraitData,
            loaded?.OrderOfBattlePlayerData ?? empty.OrderOfBattlePlayerData,
            loaded?.StatisticsPlayerData ?? empty.StatisticsPlayerData,
            loaded?.IncidentsPlayerData ?? empty.IncidentsPlayerData,
            loaded?.ViewDataPlayerData ?? empty.ViewDataPlayerData);

        coopSessionProvider.CoopSession = savedSession;
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        // savedSession is only set by GameLoaded; hosting a NEW campaign never loads a save, so
        // there is no previous session (and no players) to restore.
        if (savedSession?.Players == null) return;

        var savedPlayers = savedSession.Players.Where(player => player != null).ToArray();

        // Every row whose hero is in the campaign, with that hero. A row can name it by its full or its
        // short id, so claims are compared by the hero they resolve to, not by the id string.
        var claims = savedPlayers
            .Select(player => (Player: player, Hero: objectManager.TryGetObject<Hero>(player.HeroId, out var hero) ? hero : null))
            .Where(claim => claim.Hero != null)
            .ToArray();

        // A file saved after an old double join can name one hero under two controllers. Neither the
        // file nor the campaign says which one owns it, so restore neither. This runs over every row
        // before one row is picked per controller, so the row order can't decide which of a
        // controller's heroes survive, and a dropped row can't hide a claim.
        var sharedClaims = new HashSet<Player>();
        foreach (var claim in claims)
        {
            var sameHero = claims.Where(other => ReferenceEquals(other.Hero, claim.Hero)).Select(other => other.Player).ToArray();
            var controllerCount = sameHero.Select(player => player.ControllerId).Distinct().Count();
            if (controllerCount < 2 || sharedClaims.Contains(claim.Player)) continue;

            Logger.Error(
                "Saved session gives hero {HeroId} to {Count} controllers; restoring none of them. " +
                "The next save writes the session file without them, so copy the session .json now " +
                "and fix the copy",
                claim.Player.HeroId,
                controllerCount);
            sharedClaims.UnionWith(sameHero);
        }

        // Every hero a saved row names, so a dead hero's claim can tell when its party belongs to
        // another player now
        var playerHeroes = claims.Select(claim => claim.Hero).ToArray();

        // Two controllers' dead heroes can name one party, by either of its ids, and neither owns it. Checked
        // over every row before row selection too, so the row order can't decide which controller keeps it
        var partyClaims = savedPlayers
            .Select(player => (Player: player, Party: TryGetOwnedParty(player, playerHeroes, out var party) ? party : null))
            .Where(claim => claim.Party != null)
            .ToArray();
        foreach (var claim in partyClaims)
        {
            var sameParty = partyClaims.Where(other => ReferenceEquals(other.Party, claim.Party)).Select(other => other.Player).ToArray();
            if (sameParty.Select(player => player.ControllerId).Distinct().Count() < 2) continue;

            if (sharedClaims.Add(claim.Player))
                Logger.Error(
                    "Skipped saved registration for hero {HeroId}: another saved controller names the same party",
                    claim.Player.HeroId);
        }

        var registrations = SelectOneRegistrationPerController(
            savedPlayers.Where(player => !sharedClaims.Contains(player)),
            playerHeroes);

        var restored = new List<Player>();
        foreach (var player in registrations)
        {
            // The restorer calls vanilla party code, and one bad row must not cost every other player
            // their registration
            Player registration;
            bool restoredGraph;
            try
            {
                restoredGraph = playerPartyRestorer.TryRestore(player, out registration);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Skipped saved registration for hero {HeroId}: restoring its party threw", player.HeroId);
                continue;
            }

            if (!restoredGraph)
            {
                Logger.Error(
                    "Skipped saved registration for controller {ControllerId} (hero {HeroId}): " +
                    "the player graph could not be restored",
                    player.ControllerId,
                    player.HeroId);
                continue;
            }

            restored.Add(registration);
        }

        // A dead hero keeps its saved party for heir selection unchecked, and the row naming the party's
        // real owner may not have been restored, so every row keeps its party only if the loaded campaign
        // still gives that party to it
        var owned = new List<(Player Registration, MobileParty Party)>();
        foreach (var registration in restored)
        {
            if (TryGetOwnedParty(registration, playerHeroes, out var party))
                owned.Add((registration, party));
            else
                Logger.Error(
                    "Skipped saved registration for hero {HeroId}: its saved party {PartyId} belongs to another hero. " +
                    "The next save writes the session file without it, so copy the session .json now and fix the copy",
                    registration?.HeroId,
                    registration?.MobilePartyId);
        }

        foreach (var (registration, party) in owned)
        {
            // Checked again after restore, since a repair can change the party a row names. Two controllers
            // naming the same party, by either of its ids, means neither is restored
            if (owned.Count(claim => ReferenceEquals(claim.Party, party)) > 1)
            {
                Logger.Error(
                    "Skipped saved registration for hero {HeroId}: another saved controller names the same party",
                    registration.HeroId);
                continue;
            }

            if (!playerRegistry.AddPlayer(registration))
                Logger.Warning(
                    "Skipped saved registration for hero {HeroId}: the player registry refused it",
                    registration?.HeroId);
        }

        messageBroker.Publish(this, new SavedPlayerRegistrationsRestored());
    }

    /// <summary>
    /// Picks the single registration to restore per controller. A save written before controller
    /// ids were unique can carry more than one, in either order, so keep the one whose hero and
    /// party still exist rather than the one that happens to come first: the campaign decides
    /// which registration is real. Keeping the first can retain an incomplete player graph, which
    /// leaves the live party unregistered and sends its owner off to build another character. A row
    /// whose party the campaign gives to it comes first, since a row that names another hero's party is
    /// dropped later and would leave the controller with nothing.
    /// </summary>
    private IEnumerable<Player> SelectOneRegistrationPerController(IEnumerable<Player> players, Hero[] playerHeroes)
    {
        // AllGameObjectsRegistered means the campaign's objects are resolvable, so these lookups
        // are authoritative about which registration still has a complete player graph.
        foreach (var group in players.Where(player => player != null).GroupBy(player => player.ControllerId))
        {
            var registrations = group.ToArray();
            if (registrations.Length == 1)
            {
                yield return registrations[0];
                continue;
            }

            var live =
                registrations.FirstOrDefault(registration => TryGetOwnedParty(registration, playerHeroes, out _)) ??
                registrations.FirstOrDefault(PlayerGraphExists) ??
                registrations.FirstOrDefault(PlayerHeroExists) ??
                registrations[0];

            Logger.Warning(
                "Save carries {Count} registrations for controller {ControllerId} (heroes {HeroIds}); " +
                "restoring {KeptHeroId} and dropping the rest",
                registrations.Length,
                group.Key,
                string.Join(", ", registrations.Select(registration => registration.HeroId)),
                live.HeroId);

            yield return live;
        }
    }

    private bool PlayerGraphExists(Player player) =>
        !string.IsNullOrEmpty(player.HeroId) &&
        !string.IsNullOrEmpty(player.MobilePartyId) &&
        objectManager.TryGetObject<Hero>(player.HeroId, out _) &&
        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out _);

    private bool PlayerHeroExists(Player player) =>
        !string.IsNullOrEmpty(player.HeroId) &&
        objectManager.TryGetObject<Hero>(player.HeroId, out _);

    // A living hero's restored party is always its own. When a player's hero dies, vanilla hands its party
    // to another hero in it, which then owns it, so a dead hero waiting for heir selection keeps its party
    // unless another saved player's hero owns it now. This goes by Owner, not by who leads the party, and
    // compares the resolved objects, since a row can name either one by its full or its short id.
    private bool TryGetOwnedParty(Player player, Hero[] playerHeroes, out MobileParty party)
    {
        party = null;
        if (player == null ||
            !objectManager.TryGetObject(player.MobilePartyId, out party) || party == null ||
            !objectManager.TryGetObject(player.HeroId, out Hero hero) || hero == null)
            return false;

        var owner = party.Owner;
        return ReferenceEquals(owner, hero) ||
            hero.IsDead && !playerHeroes.Any(playerHero => ReferenceEquals(playerHero, owner));
    }
}
