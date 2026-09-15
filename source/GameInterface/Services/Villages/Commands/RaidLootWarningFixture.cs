#if DEBUG
using Common;
using Common.Commands;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json;
using SandBox.GauntletUI;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Villages.Commands;

public interface IRaidLootWarningFixture : IGameAbstraction
{
    CoopCommandResult Prepare(string controllerId, string baseline);
    CoopCommandResult StartRaid(string controllerId);
    CoopCommandResult RequestRaid(string controllerId);
    CoopCommandResult CompleteSimulation(string controllerId);
    CoopCommandResult CompleteLootParty(string controllerId);
    CoopCommandResult ShowLootWarning(string controllerId);
    CoopCommandResult AcceptLootWarning(string controllerId);
    CoopCommandResult ReadState(string controllerId);
    void CaptureMapEvent(MapEvent mapEvent);
    void SeedLoot(MapEvent mapEvent, Dictionary<MapEventParty, ItemRoster> playerLootRosters);
}

// ServiceModule registers this stateful fixture once per session container.
public sealed class RaidLootWarningFixture : IRaidLootWarningFixture
{
    internal const string SettlementId = "village_ES1_2";
    internal const int PlayerTroops = 60;
    internal const int VillageTroops = 8;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private FixtureSession fixture;
    private ClientActionSession clientAction;

    public RaidLootWarningFixture(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        IVillageHostileActionInterface villageHostileActionInterface)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.villageHostileActionInterface = villageHostileActionInterface;
    }

    public CoopCommandResult Prepare(string controllerId, string baseline)
    {
        if (ModInformation.IsClient) return Failed("Run preparation on the authoritative server.");
        if (baseline != "disposable-baseline")
            return Failed("Reload a fresh copy of the baseline save, then pass disposable-baseline. Preparation changes campaign state permanently.");
        if (Campaign.Current == null) return Failed("No campaign is loaded.");
        if (!TryFindPlayer(controllerId, out var player, out var party, out var error)) return Failed(error);
        if (!playerManager.IsConnected(player) || !playerManager.Contains(party))
            return Failed("The target must be a registered, connected client party.");
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement))
            return Failed("Registered Polisia (village_ES1_2) is missing.");

        if (fixture?.Campaign != Campaign.Current) fixture = null;
        if (fixture != null)
        {
            if (fixture.CanRepeat(Campaign.Current, player.ControllerId, party, settlement) &&
                IsPreparedStateUnchanged(party, settlement))
                return ReadState(player.ControllerId);
            return Failed("This campaign already has a fixture. Reload a fresh copy of the baseline before any retry or other target.");
        }

        error = ValidatePreparation(party, settlement);
        if (error != null) return Failed(error);

        fixture = new FixtureSession(Campaign.Current, player.ControllerId, party, settlement);
        try
        {
            fixture.Prepare(() => Stage(party, settlement));
            return ReadState(player.ControllerId);
        }
        catch (Exception exception)
        {
            LogManager.GetLogger<RaidLootWarningFixture>().Error(exception, "Issue 3262 fixture preparation failed; reload baseline");
            return Failed("Preparation partially failed: " + exception.Message + ". Reload the baseline; do not retry in this campaign.");
        }
    }

    public CoopCommandResult StartRaid(string controllerId)
    {
        if (ModInformation.IsServer) return Failed("Run the raid start on the client.");
        if (Campaign.Current == null) return Failed("No campaign is loaded.");
        if (!TryFindPlayer(controllerId, out var player, out var party, out var error)) return Failed(error);
        if (party != MobileParty.MainParty)
            return Failed("Run the raid start on the client that owns the fixture controller.");
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement))
            return Failed("Registered Polisia (village_ES1_2) is missing.");
        if (clientAction?.Campaign != Campaign.Current) clientAction = null;
        if (clientAction != null)
            return Failed("This client already started a raid-loot-warning fixture. Reload a fresh baseline before another start.");
        if (party.MapEvent != null || PlayerEncounter.Current != null)
            return Failed("The client party already has an encounter or map event. Reload the fresh baseline before starting the fixture.");
        if (party.Position != settlement.GatePosition || party.CurrentSettlement != null)
            return Failed("The client party is not at the prepared Polisia gate. Confirm server preparation before starting the fixture.");
        if (settlement.Village?.VillageState != Village.VillageStates.Normal ||
            !FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
            return Failed("Polisia is not in the prepared raid state. Confirm the server fixture state before starting the fixture.");

        clientAction = new ClientActionSession(Campaign.Current, player.ControllerId, party, settlement);
        try
        {
            clientAction.RequestSettlementEntry();
            EncounterManager.StartSettlementEncounter(party, settlement);
            return ReadState(player.ControllerId);
        }
        catch (Exception exception)
        {
            clientAction.Fail();
            LogManager.GetLogger<RaidLootWarningFixture>().Error(exception, "Issue 3262 fixture settlement entry request failed; reload baseline");
            return Failed("Settlement entry request partially failed: " + exception.Message + ". Reload the baseline; do not retry in this campaign.");
        }
    }

    public CoopCommandResult RequestRaid(string controllerId)
    {
        if (!TryGetClientAction(controllerId, out var party, out var action, out var error)) return Failed(error);
        if (!action.IsAwaitingSettlementEntryApproval)
            return Failed("The fixture raid request is not awaiting the approved settlement encounter.");
        if (party.MapEvent != null || PlayerEncounter.Current == null ||
            Settlement.CurrentSettlement != action.Settlement || PlayerEncounter.EncounterSettlement != action.Settlement)
            return Failed("Wait for raid_loot_warning_state to show the approved Polisia settlement encounter before requesting the raid.");

        try
        {
            action.ObserveSettlementEntryApproval();
            action.RequestRaid();
            villageHostileActionInterface.RequestHostileAction(VillageHostileAction.Raid);
            return ReadState(action.ControllerId);
        }
        catch (Exception exception)
        {
            action.Fail();
            LogManager.GetLogger<RaidLootWarningFixture>().Error(exception, "Issue 3262 fixture raid request failed; reload baseline");
            return Failed("Raid request partially failed: " + exception.Message + ". Reload the baseline; do not retry in this campaign.");
        }
    }

    public CoopCommandResult CompleteSimulation(string controllerId)
    {
        if (!TryGetClientAction(controllerId, out var party, out var action, out var error)) return Failed(error);
        if (!action.CanCompleteSimulation()) return Failed("The fixture raid is not awaiting a simulation result.");
        if (!IsExpectedRaidMapEvent(party, action.Settlement)) return Failed("The active map event is not the fixture raid.");

        var battleSimulation = PlayerEncounter.Current?.BattleSimulation;
        if (battleSimulation == null || !battleSimulation.IsSimulationFinished)
            return Failed("The completed battle simulation is not ready.");

        var mapScreen = ScreenManager.TopScreen as MapScreen;
        var simulationVm = (mapScreen?._battleSimulationView as GauntletMapBattleSimulationView)?._dataSource;
        if (simulationVm?.IsSimulation != true || !simulationVm.IsOver)
            return Failed("The completed battle simulation result is not open.");

        action.ObserveMapEvent(party.MapEvent);
        simulationVm.ExecuteQuitAction();
        action.CompleteSimulation();
        return ReadState(action.ControllerId);
    }

    public CoopCommandResult CompleteLootParty(string controllerId)
    {
        if (!TryGetClientAction(controllerId, out var party, out var action, out var error)) return Failed(error);
        if (!action.CanCompleteLootParty()) return Failed("The fixture raid is not awaiting the loot Party screen.");
        if (!IsExpectedRaidMapEvent(party, action.Settlement)) return Failed("The active map event is not the fixture raid.");
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            partyState.PartyScreenMode != PartyScreenHelper.PartyScreenMode.Loot ||
            !(ScreenManager.TopScreen is GauntletPartyScreen))
            return Failed("The real raid loot Party screen is not active.");

        action.ObserveMapEvent(party.MapEvent);
        PartyScreenHelper.CloseScreen(isForced: false);
        action.CompleteLootParty();
        return ReadState(action.ControllerId);
    }

    public CoopCommandResult ShowLootWarning(string controllerId)
    {
        if (!TryGetClientAction(controllerId, out var party, out var action, out var error)) return Failed(error);
        if (!action.CanShowLootWarning()) return Failed("The fixture raid is not awaiting the leaving-loot warning.");
        if (!IsExpectedRaidMapEvent(party, action.Settlement)) return Failed("The active map event is not the fixture raid.");
        if (!(Game.Current?.GameStateManager?.ActiveState is InventoryState) ||
            !(ScreenManager.TopScreen is GauntletInventoryScreen inventoryScreen))
            return Failed("The real raid loot inventory is not active.");
        if (InformationManager.IsAnyInquiryActive()) return Failed("An inquiry is already active.");

        var inventory = inventoryScreen._dataSource?._inventoryLogic;
        if (inventory?.GetElementCountOnSide(InventoryLogic.InventorySide.OtherInventory) <= 0 ||
            GrainCount(inventory.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)) != 1)
            return Failed("The fixture inventory does not contain its authoritative one-grain loot seed.");

        InquiryData capturedInquiry = null;
        Action<InquiryData, bool, bool> captureInquiry = (inquiry, _, _) => capturedInquiry = inquiry;
        InformationManager.OnShowInquiry += captureInquiry;
        try
        {
            inventoryScreen.ExecuteConfirm();
        }
        finally
        {
            InformationManager.OnShowInquiry -= captureInquiry;
        }

        var expectedText = GameTexts.FindText("str_leaving_loot_behind").ToString();
        if (capturedInquiry?.AffirmativeAction == null ||
            !InformationManager.IsAnyInquiryActive() ||
            !string.Equals(capturedInquiry.Text, expectedText, StringComparison.Ordinal))
            return Failed("The real leaving-loot-behind warning did not open.");

        action.ObserveMapEvent(party.MapEvent);
        action.ShowLootWarning(capturedInquiry);
        return ReadState(action.ControllerId);
    }

    public CoopCommandResult AcceptLootWarning(string controllerId)
    {
        if (!TryGetClientAction(controllerId, out var party, out var action, out var error)) return Failed(error);
        if (!action.TryGetPendingLootWarning(out var inquiry) || !InformationManager.IsAnyInquiryActive())
            return Failed("The captured leaving-loot-behind warning is not active.");
        if (!IsExpectedRaidMapEvent(party, action.Settlement)) return Failed("The active map event is not the fixture raid.");

        InformationManager.HideInquiry();
        inquiry.AffirmativeAction();
        action.AcceptLootWarning();
        return ReadState(action.ControllerId);
    }

    private string ValidatePreparation(MobileParty party, Settlement settlement)
    {
        if (!MapEventConfig.AllowRaidAiIntervention)
            return "Enable coop.debug.mapevent.allow_raid_ai_intervention on before preparation so village resistance is exercised.";
        if (settlement?.Village == null || settlement.Village.StringId != "village_comp_ES1_2" ||
            settlement.Village.Bound?.StringId != "town_ES1" || settlement.Party == null)
            return "Polisia must be village_ES1_2 / village_comp_ES1_2, bound to Danustica (town_ES1).";
        if (!IsIdleLandParty(party) || settlement.Party.MapEvent != null || settlement.IsUnderSiege ||
            settlement.Village.Bound.IsUnderSiege || settlement.Village.VillageState != Village.VillageStates.Normal)
            return "Require an active holding land party outside settlements, armies, sieges and map events, and normal Polisia.";
        var hero = party.LeaderHero;
        if (hero == null || !hero.IsAlive || hero.IsPrisoner || hero.IsWounded || hero.Clan == null ||
            hero.CharacterObject == null || hero.Culture?.BasicTroop == null || settlement.Culture?.BasicTroop == null ||
            party.MemberRoster.GetTroopCount(hero.CharacterObject) != 1)
            return "Require one healthy living leader, its clan and both culture basic troops.";
        if (party.MapFaction == null || settlement.MapFaction == null || party.MapFaction == settlement.MapFaction)
            return "The player and Polisia must belong to different factions.";
        if (party.MemberRoster.GetTroopRoster().Any(x => x.Character.IsHero && x.Character != hero.CharacterObject) ||
            party.PrisonRoster.GetTroopRoster().Any(x => x.Character.IsHero) ||
            settlement.Party.MemberRoster.GetTroopRoster().Any(x => x.Character.IsHero) ||
            settlement.Party.PrisonRoster.GetTroopRoster().Any(x => x.Character.IsHero))
            return "Use a baseline without companions or hero prisoners in either fixture roster.";
        var militia = settlement.MilitiaPartyComponent?.MobileParty;
        if (!CanStageMilitia(militia, settlement))
            return "Polisia's native militia must be active, registered, inside the village and outside battles.";
        if (MobileParty.All.Any(x => IsUnexpectedOccupant(x, party, militia, settlement)))
            return "Polisia must contain no visiting mobile parties.";
        if (DefaultItems.Grain == null || !objectManager.TryGetId(DefaultItems.Grain, out _) ||
            !objectManager.TryGetId(hero.Culture.BasicTroop, out _) ||
            !objectManager.TryGetId(settlement.Culture.BasicTroop, out _) ||
            !objectManager.TryGetId(settlement.Party, out _) || !objectManager.TryGetId(hero, out _))
            return "The grain, troops, leader and village party must have registered ids before staging.";
        if (!settlement.GatePosition.IsValid() || !settlement.GatePosition.IsOnLand)
            return "Polisia's gate position must be valid land.";
        return null;
    }

    private bool CanStageMilitia(MobileParty militia, Settlement settlement) =>
        militia == null || (militia.IsMilitia && militia.IsActive && militia.Party.IsActive &&
            militia.CurrentSettlement == settlement && militia.MapEvent == null &&
            objectManager.TryGetId(militia, out _) &&
            !militia.MemberRoster.GetTroopRoster().Any(x => x.Character.IsHero) &&
            !militia.PrisonRoster.GetTroopRoster().Any(x => x.Character.IsHero));

    internal static bool IsUnexpectedOccupant(MobileParty occupant, MobileParty player, MobileParty militia, Settlement settlement) =>
        occupant != player && occupant != militia && occupant.CurrentSettlement == settlement;

    private static bool IsIdleLandParty(MobileParty party) =>
        party?.Party != null && party.IsActive && party.Party.IsActive &&
        party.CurrentSettlement == null && party.MapEvent == null && party.Army == null &&
        party.BesiegedSettlement == null && !party.IsCurrentlyAtSea &&
        party.PartyMoveMode == MoveModeType.Hold && !party.IsMoving;

    private static bool IsPreparedStateUnchanged(MobileParty party, Settlement settlement) =>
        MapEventConfig.AllowRaidAiIntervention && IsIdleLandParty(party) && party.LeaderHero?.Culture?.BasicTroop != null &&
        party.LeaderHero.IsAlive && !party.LeaderHero.IsWounded && !party.LeaderHero.IsPrisoner &&
        settlement?.Party != null && settlement.Culture?.BasicTroop != null && settlement.Party.MapEvent == null &&
        party.Position == settlement.GatePosition && settlement.Village.VillageState == Village.VillageStates.Normal &&
        party.MemberRoster.TotalManCount == PlayerTroops + 1 &&
        settlement.Party.MemberRoster.TotalManCount == VillageTroops &&
        party.MemberRoster.TotalWounded == 0 && settlement.Party.MemberRoster.TotalWounded == 0 &&
        (settlement.MilitiaPartyComponent?.MobileParty?.MemberRoster.TotalManCount ?? 0) == 0 &&
        party.MemberRoster.GetTroopCount(party.LeaderHero.Culture.BasicTroop) == PlayerTroops &&
        settlement.Party.MemberRoster.GetTroopCount(settlement.Culture.BasicTroop) == VillageTroops &&
        FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction);

    private void Stage(MobileParty party, Settlement settlement)
    {
        if (!FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
            DeclareWarAction.ApplyByDefault(party.MapFaction, settlement.MapFaction);

        foreach (var element in party.MemberRoster.GetTroopRoster().ToArray())
        {
            if (element.Character.IsHero) continue;
            party.MemberRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
        }
        party.MemberRoster.RemoveZeroCounts();
        party.MemberRoster.AddToCounts(party.LeaderHero.Culture.BasicTroop, PlayerTroops);
        party.PrisonRoster.Clear();
        settlement.Party.MemberRoster.Clear();
        settlement.Party.MemberRoster.AddToCounts(settlement.Culture.BasicTroop, VillageTroops);
        settlement.Party.PrisonRoster.Clear();
        settlement.Party.ItemRoster.Clear();
        var militia = settlement.MilitiaPartyComponent?.MobileParty;
        if (militia != null)
        {
            militia.MemberRoster.Clear();
            militia.PrisonRoster.Clear();
        }
        settlement.SettlementHitPoints = 1f;
        party.Position = settlement.GatePosition;
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(
            party, forcePosition: true, isCurrentlyAtSea: false, resetMovementToHold: true));
    }

    public void CaptureMapEvent(MapEvent mapEvent)
    {
        if (ModInformation.IsClient || fixture?.Campaign != Campaign.Current ||
            !mapEvent.IsRaidHostileAction() || fixture.Party.MapEvent != mapEvent ||
            mapEvent.MapEventSettlement != fixture.Settlement || mapEvent.IsFinalized ||
            mapEvent.AttackerSide?.LeaderParty != fixture.Party.Party ||
            mapEvent.DefenderSide?.LeaderParty != fixture.Settlement.Party ||
            !objectManager.TryGetId(mapEvent, out var eventId)) return;
        if (!HasExpectedParticipants(mapEvent))
        {
            fixture.Reject();
            return;
        }
        fixture.Capture(Campaign.Current, fixture.Party, fixture.Settlement, mapEvent, eventId);
    }

    public void SeedLoot(MapEvent mapEvent, Dictionary<MapEventParty, ItemRoster> playerLootRosters)
    {
        if (ModInformation.IsClient || fixture?.Campaign != Campaign.Current ||
            fixture.Party.MapEvent != mapEvent || mapEvent.BattleState != BattleState.AttackerVictory ||
            mapEvent.MapEventSettlement != fixture.Settlement || !mapEvent.IsRaidHostileAction()) return;
        if (!HasExpectedParticipants(mapEvent))
        {
            fixture.Reject();
            return;
        }
        var winner = mapEvent.AttackerSide.Parties.FirstOrDefault(x => x.Party == fixture.Party.Party);
        if (winner == null || !playerLootRosters.TryGetValue(winner, out var roster)) return;
        fixture.Seed(Campaign.Current, mapEvent, winner.Party.MobileParty, () =>
        {
            foreach (var element in roster.Where(x => x.EquipmentElement.Item == DefaultItems.Grain).ToArray())
                roster.AddToCounts(element.EquipmentElement, -element.Amount);
            roster.AddToCounts(DefaultItems.Grain, 1);
            fixture.SeededLoot = roster;
        });
    }

    private bool HasExpectedParticipants(MapEvent mapEvent)
    {
        var militia = fixture.Settlement.MilitiaPartyComponent?.MobileParty;
        return mapEvent.AttackerSide.Parties.Count == 1 &&
            mapEvent.AttackerSide.Parties[0].Party == fixture.Party.Party &&
            mapEvent.DefenderSide.Parties.All(x => x.Party == fixture.Settlement.Party || x.Party == militia?.Party) &&
            (militia?.MemberRoster.TotalManCount ?? 0) == 0;
    }

    public CoopCommandResult ReadState(string controllerId)
    {
        if (Campaign.Current == null) return Failed("No campaign is loaded.");
        if (!TryFindPlayer(controllerId, out var player, out var party, out var error)) return Failed(error);
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement))
            return Failed("Registered Polisia (village_ES1_2) is missing.");
        if (ModInformation.IsClient && party != MobileParty.MainParty)
            return Failed("Client UI observation requires that client's own controller id.");

        bool localClient = ModInformation.IsClient;
        var session = fixture?.Campaign == Campaign.Current && fixture.ControllerId == player.ControllerId ? fixture : null;
        var action = localClient && clientAction?.Matches(Campaign.Current, player.ControllerId, party, settlement) == true
            ? clientAction
            : null;
        var activeState = localClient ? Game.Current?.GameStateManager?.ActiveState : null;
        var topScreen = localClient ? ScreenManager.TopScreen : null;
        var inventory = (topScreen as GauntletInventoryScreen)?._dataSource?._inventoryLogic;
        var simulation = ((topScreen as MapScreen)?._battleSimulationView as GauntletMapBattleSimulationView)?._dataSource;
        var currentEvent = party.MapEvent;
        var capturedEvent = session?.MapEvent;
        var militia = settlement.MilitiaPartyComponent?.MobileParty;
        string militiaId = null;
        if (militia != null) objectManager.TryGetId(militia, out militiaId);
        objectManager.TryGetId(party, out var partyId);
        string eventId = null;
        if (currentEvent != null) objectManager.TryGetId(currentEvent, out eventId);

        return Succeeded(new
        {
            fixtureVersion = 3,
            buildVersion = ModInformation.BuildVersion,
            sourceCommit = ModInformation.Commit,
            assemblyMvid = typeof(RaidLootWarningFixture).Assembly.ManifestModule.ModuleVersionId,
            side = localClient ? "client" : "server",
            controllerId = player.ControllerId,
            partyId,
            partyStringId = party.StringId,
            fixtureToken = session?.Token,
            fixturePhase = session?.Phase ?? "not-prepared-on-this-side",
            clientActionPhase = action?.Phase ?? (localClient ? "not-started-on-this-client" : null),
            clientActionMapEventObserved = action?.MapEvent != null,
            clientPendingLootWarning = action?.HasPendingLootWarning,
            expectedSeed = new { item = "grain", count = 1 },
            seedApplied = session?.Seeded,
            rawLootCount = session?.SeededLoot?.Sum(x => x.Amount),
            rawLootGrainCount = session?.SeededLoot == null ? (int?)null : GrainCount(session.SeededLoot),
            capturedMapEventId = session?.MapEventId,
            capturedMapEventFinalized = capturedEvent?.IsFinalized,
            capturedMapEventRegistered = capturedEvent == null ? (bool?)null :
                objectManager.TryGetObject<MapEvent>(session.MapEventId, out var registeredEvent) && registeredEvent == capturedEvent,
            capturedMapEventStillAttached = capturedEvent == null ? (bool?)null :
                party.MapEvent == capturedEvent || settlement.Party.MapEvent == capturedEvent,
            mapEventId = eventId,
            mapEventState = currentEvent?.BattleState.ToString(),
            mapEventType = currentEvent?.Component?.GetType().Name,
            attackerParties = currentEvent?.AttackerSide.Parties.Select(x => new { id = x.Party.Id, members = RosterState(x.Party.MemberRoster) }).ToArray(),
            defenderParties = currentEvent?.DefenderSide.Parties.Select(x => new { id = x.Party.Id, members = RosterState(x.Party.MemberRoster) }).ToArray(),
            mapEventSettlementId = currentEvent?.MapEventSettlement?.StringId,
            settlementId = settlement.StringId,
            settlementName = settlement.Name.ToString(),
            villageId = settlement.Village.StringId,
            boundTownId = settlement.Village.Bound?.StringId,
            villageState = settlement.Village.VillageState.ToString(),
            villageHitPoints = settlement.SettlementHitPoints,
            partyCurrentSettlementId = party.CurrentSettlement?.StringId,
            partyMoveMode = party.PartyMoveMode.ToString(),
            partyPosition = new { x = party.Position.X, y = party.Position.Y, land = party.Position.IsOnLand },
            playerMembers = RosterState(party.MemberRoster),
            villageMembers = RosterState(settlement.Party.MemberRoster),
            militiaId,
            militiaCurrentSettlementId = militia?.CurrentSettlement?.StringId,
            militiaMembers = militia == null ? null : RosterState(militia.MemberRoster),
            leaderHitPoints = party.LeaderHero?.HitPoints,
            atWar = FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction),
            allowRaidAiIntervention = MapEventConfig.AllowRaidAiIntervention,
            partyLootActive = activeState is PartyState partyState && partyState.PartyScreenMode == PartyScreenHelper.PartyScreenMode.Loot,
            inventoryActive = activeState is InventoryState,
            topScreen = topScreen?.GetType().Name,
            otherItemCount = inventory?.GetElementCountOnSide(InventoryLogic.InventorySide.OtherInventory),
            otherGrainCount = inventory == null ? (int?)null : GrainCount(inventory.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)),
            inquiryActive = localClient ? (bool?)InformationManager.IsAnyInquiryActive() : null,
            simulationActive = simulation?.IsSimulation,
            simulationResultVisible = simulation?.IsSimulation == true && simulation.IsOver,
            encounterState = localClient ? PlayerEncounter.Current?.EncounterState.ToString() : null,
            encounterSettlementId = localClient ? PlayerEncounter.EncounterSettlement?.StringId : null,
            menuId = localClient ? Campaign.Current.CurrentMenuContext?.GameMenu?.StringId : null,
        });
    }

    private bool TryFindPlayer(string controllerId, out Player player, out MobileParty party, out string error)
    {
        player = null;
        party = null;
        error = "Controller must resolve to a registered client party.";
        if (controllerId == "only-connected")
        {
            var candidates = playerManager.Players.ToArray();
            if (candidates.Length != 1)
            {
                error = "only-connected requires exactly one registered player; use coop.debug.players.list and supply its ControllerId.";
                return false;
            }
            controllerId = candidates[0].ControllerId;
        }
        return playerManager.TryGetPlayer(controllerId, out player) &&
            objectManager.TryGetObject(player.MobilePartyId, out party);
    }

    private bool TryGetClientAction(
        string controllerId,
        out MobileParty party,
        out ClientActionSession action,
        out string error)
    {
        party = null;
        action = null;
        error = "Run this command on the client that started the fixture raid.";
        if (ModInformation.IsServer || Campaign.Current == null) return false;
        if (!TryFindPlayer(controllerId, out var player, out party, out error)) return false;
        if (party != MobileParty.MainParty)
        {
            error = "Run this command on the client that owns the fixture controller.";
            return false;
        }
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement))
        {
            error = "Registered Polisia (village_ES1_2) is missing.";
            return false;
        }
        if (clientAction?.Matches(Campaign.Current, player.ControllerId, party, settlement) != true)
        {
            error = "No matching fixture raid is active on this client. Start from the prepared baseline first.";
            return false;
        }

        action = clientAction;
        return true;
    }

    private static bool IsExpectedRaidMapEvent(MobileParty party, Settlement settlement)
    {
        var mapEvent = party?.MapEvent;
        return mapEvent != null && mapEvent.IsRaidHostileAction() &&
            mapEvent.MapEventSettlement == settlement &&
            mapEvent.AttackerSide?.LeaderParty == party.Party &&
            mapEvent.DefenderSide?.LeaderParty == settlement.Party;
    }

    private static object RosterState(TroopRoster roster) => roster.GetTroopRoster()
        .Where(x => x.Number > 0)
        .Select(x => new { id = x.Character.StringId, count = x.Number, wounded = x.WoundedNumber }).ToArray();

    private static int GrainCount(IEnumerable<ItemRosterElement> roster) => roster
        .Where(x => x.EquipmentElement.Item == DefaultItems.Grain).Sum(x => x.Amount);

    private static CoopCommandResult Failed(string message) => new CoopCommandResult(false, message, "fixture_rejected");
    private static CoopCommandResult Succeeded(object state) => new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

    internal sealed class FixtureSession
    {
        internal Campaign Campaign { get; }
        internal string ControllerId { get; }
        internal MobileParty Party { get; }
        internal Settlement Settlement { get; }
        internal string Token { get; } = Guid.NewGuid().ToString("N");
        internal string Phase { get; private set; } = "preparing";
        internal MapEvent MapEvent { get; private set; }
        internal string MapEventId { get; private set; }
        internal bool Seeded { get; private set; }
        internal ItemRoster SeededLoot { get; set; }

        internal FixtureSession(Campaign campaign, string controllerId, MobileParty party, Settlement settlement)
        {
            Campaign = campaign;
            ControllerId = controllerId;
            Party = party;
            Settlement = settlement;
        }

        internal bool CanRepeat(Campaign campaign, string controllerId, MobileParty party, Settlement settlement) =>
            Phase == "prepared" && Campaign == campaign && ControllerId == controllerId && Party == party && Settlement == settlement;

        internal void Reject() => Phase = "failed-reload-baseline";

        internal void Prepare(Action stage)
        {
            if (Phase != "preparing") throw new InvalidOperationException("Preparation has already run.");
            // Failed mutations cannot be rolled back safely without reloading the entire baseline.
            Phase = "failed-reload-baseline";
            stage();
            Phase = "prepared";
        }

        internal void Capture(Campaign campaign, MobileParty party, Settlement settlement, MapEvent mapEvent, string mapEventId)
        {
            if (Phase != "prepared" || Campaign != campaign || Party != party || Settlement != settlement || mapEvent == null) return;
            MapEvent = mapEvent;
            MapEventId = mapEventId;
            Phase = "captured";
        }

        internal void Seed(Campaign campaign, MapEvent mapEvent, MobileParty winner, Action seed)
        {
            if (Phase != "captured" || Campaign != campaign || MapEvent != mapEvent || winner != Party) return;
            Phase = "failed-reload-baseline";
            seed();
            Seeded = true;
            Phase = "seeded";
        }
    }

    internal sealed class ClientActionSession
    {
        internal Campaign Campaign { get; }
        internal string ControllerId { get; }
        internal MobileParty Party { get; }
        internal Settlement Settlement { get; }
        internal string Phase { get; private set; } = "starting";
        internal MapEvent MapEvent { get; private set; }
        internal bool HasPendingLootWarning => pendingLootWarning != null;
        private InquiryData pendingLootWarning;

        internal ClientActionSession(Campaign campaign, string controllerId, MobileParty party, Settlement settlement)
        {
            Campaign = campaign;
            ControllerId = controllerId;
            Party = party;
            Settlement = settlement;
        }

        internal bool Matches(Campaign campaign, string controllerId, MobileParty party, Settlement settlement) =>
            Campaign == campaign && ControllerId == controllerId && Party == party && Settlement == settlement;

        internal void RequestSettlementEntry()
        {
            if (Phase != "starting") throw new InvalidOperationException("Settlement entry request has already run.");
            Phase = "settlement-entry-requested";
        }

        internal bool IsAwaitingSettlementEntryApproval => Phase == "settlement-entry-requested";

        internal void ObserveSettlementEntryApproval()
        {
            if (!IsAwaitingSettlementEntryApproval)
                throw new InvalidOperationException("Settlement entry approval is out of order.");
            Phase = "settlement-entry-approved";
        }

        internal bool CanRequestRaid() => Phase == "settlement-entry-approved";

        internal void RequestRaid()
        {
            if (!CanRequestRaid()) throw new InvalidOperationException("Raid request is out of order.");
            Phase = "raid-requested";
        }

        internal bool CanCompleteSimulation() => Phase == "raid-requested";

        internal void CompleteSimulation()
        {
            if (!CanCompleteSimulation()) throw new InvalidOperationException("Simulation completion is out of order.");
            Phase = "simulation-complete";
        }

        internal bool CanCompleteLootParty() => Phase == "simulation-complete";

        internal void CompleteLootParty()
        {
            if (!CanCompleteLootParty()) throw new InvalidOperationException("Loot Party completion is out of order.");
            Phase = "loot-party-complete";
        }

        internal bool CanShowLootWarning() => Phase == "loot-party-complete";

        internal void ShowLootWarning(InquiryData inquiry)
        {
            if (!CanShowLootWarning() || inquiry?.AffirmativeAction == null)
                throw new InvalidOperationException("Loot warning capture is out of order.");
            pendingLootWarning = inquiry;
            Phase = "loot-warning-shown";
        }

        internal bool TryGetPendingLootWarning(out InquiryData inquiry)
        {
            inquiry = pendingLootWarning;
            return Phase == "loot-warning-shown" && inquiry?.AffirmativeAction != null;
        }

        internal void AcceptLootWarning()
        {
            if (Phase != "loot-warning-shown") throw new InvalidOperationException("Loot warning acceptance is out of order.");
            pendingLootWarning = null;
            Phase = "loot-warning-accepted";
        }

        internal void ObserveMapEvent(MapEvent mapEvent)
        {
            if (mapEvent == null) throw new InvalidOperationException("Fixture map event is unavailable.");
            if (MapEvent != null && MapEvent != mapEvent)
                throw new InvalidOperationException("Fixture map event changed during the scenario.");
            MapEvent = mapEvent;
        }

        internal void Fail()
        {
            pendingLootWarning = null;
            Phase = "failed-reload-baseline";
        }
    }
}

[HarmonyPatch(typeof(MapEventManager), nameof(MapEventManager.OnMapEventCreated))]
internal static class RaidLootWarningFixtureMapEventPatch
{
    [HarmonyPostfix]
    private static void Postfix(MapEvent mapEvent)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IRaidLootWarningFixture>(out var fixture))
            fixture.CaptureMapEvent(mapEvent);
    }
}
#endif
