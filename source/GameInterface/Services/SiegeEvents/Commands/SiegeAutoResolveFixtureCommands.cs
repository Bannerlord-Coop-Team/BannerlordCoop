#if DEBUG
using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.Time.Interfaces;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

internal static class SiegeAutoResolveFixtureCommands
{
    private const int FixturePlayerTroops = 5;
    private const int FixtureDefenderTroops = 200;

    private static SiegeAutoResolveFixture fixture;
    private static FixtureRestorationResult lastRestoration;

    [CommandLineArgumentFunction("auto_resolve_fixture_capture", "coop.debug.siege")]
    public static string Capture(List<string> args)
    {
        if (ModInformation.IsClient)
            return Error("Run this command on the server.");
        if (args.Count != 2 || !int.TryParse(args[1], out int expectedPlayerCount) || expectedPlayerCount != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_capture <settlementId> 1");
        if (fixture != null)
            return Error("The siege auto-resolve fixture is already captured.");
        if (!TryResolveServices(
                out var objectManager,
                out var playerManager,
                out var behaviorSnapshot,
                out var timeControl,
                out var error))
            return Error(error);
        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
            return Error($"Settlement with id {args[0]} not found.");
        if (!settlement.IsFortification || settlement.SiegeEvent != null || settlement.Party.MapEvent != null)
            return Error($"{settlement.Name} must be a fortification with no active siege or map event.");

        var players = playerManager.Players.Where(playerManager.IsConnected).ToArray();
        if (players.Length != expectedPlayerCount)
            return Error($"Expected {expectedPlayerCount} connected player, found {players.Length}.");
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(players[0].MobilePartyId, out var playerParty))
            return Error($"Unable to resolve player party {players[0].MobilePartyId}.");
        if (!playerParty.IsActive || playerParty.MapEvent != null || playerParty.BesiegerCamp != null ||
            playerParty.CurrentSettlement != null || playerParty.MapFaction == null ||
            playerParty._attachedParties.Count > 0)
            return Error("The connected player party is not clean for the fixture.");
        if (playerParty.MapFaction == settlement.MapFaction)
            return Error("The connected player party belongs to Danustica's faction.");
        if (!playerParty.MapFaction.IsAtWarWith(settlement.MapFaction))
            return Error("The connected player party must already be at war with Danustica's faction.");

        try
        {
            // Mirror Town.GetDefenderParties before StartSiegeEvent creates the siege graph it normally reads.
            var defenseParties = settlement.Parties
                .Where(party =>
                    party.MapFaction?.IsAtWarWith(playerParty.MapFaction) == true &&
                    party.IsActive &&
                    !party.IsVillager &&
                    !party.IsCaravan &&
                    (!party.IsMilitia || !settlement.Town.InRebelliousState))
                .Distinct()
                .ToArray();
            if (defenseParties.Length == 0)
                return Error($"{settlement.Name} has no mobile siege defenders for the fixture.");

            var involvedParties = defenseParties
                .Append(playerParty)
                .Distinct()
                .Select(party => CaptureParty(party, behaviorSnapshot))
                .ToArray();
            var settlementParty = CapturePartyBase(settlement.Party);
            var involvedHeroes = involvedParties
                .SelectMany(snapshot => snapshot.MemberRoster.Concat(snapshot.PrisonRoster))
                .Concat(settlementParty.MemberRoster)
                .Concat(settlementParty.PrisonRoster)
                .Where(element => element.Character.IsHero)
                .Select(element => element.Character.HeroObject)
                .Concat(involvedParties.Select(snapshot => snapshot.LeaderHero))
                .Append(settlementParty.LeaderHero)
                .Where(hero => hero != null)
                .Distinct()
                .Select(CaptureHero)
                .ToArray();
            var involvedClans = involvedHeroes
                .Select(snapshot => snapshot.Hero.Clan)
                .Concat(involvedParties.Select(snapshot => snapshot.Party.ActualClan))
                .Append(settlement.OwnerClan)
                .Where(clan => clan != null)
                .Distinct()
                .Select(CaptureClan)
                .ToArray();

            fixture = new SiegeAutoResolveFixture(
                Guid.NewGuid().ToString("N"),
                settlement,
                playerParty,
                settlementParty,
                involvedParties,
                involvedHeroes,
                involvedClans,
                timeControl.GetTimeControl());
            fixture.BaselineFingerprint = BuildCurrentFingerprint(
                fixture,
                behaviorSnapshot,
                timeControl);
            lastRestoration = null;
            return StateJson("captured", fixture, settlement, playerParty);
        }
        catch (Exception e)
        {
            fixture = null;
            return Error($"Fixture capture failed: {e.Message}");
        }
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_stage", "coop.debug.siege")]
    public static string Stage(List<string> args)
    {
        if (ModInformation.IsClient)
            return Error("Run this command on the server.");
        if (args.Count != 2 || args[1] != "1")
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_stage <settlementId> 1");
        if (!TryGetFixture(args[0], out var activeFixture, out var error))
            return Error(error);
        if (activeFixture.Staged)
            return Error("The siege auto-resolve fixture was already staged.");
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface) ||
            !ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            return Error("Unable to resolve siege fixture services.");

        try
        {
            timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
            PreparePlayerParty(activeFixture.PlayerParty, activeFixture.Settlement);
            foreach (var defender in activeFixture.Parties.Where(snapshot => snapshot.Party != activeFixture.PlayerParty))
                PrepareDefenderParty(defender.Party, activeFixture.Settlement);
            siegeEventInterface.StartSiegeEvent(activeFixture.PlayerParty, activeFixture.Settlement);

            var siegeEvent = activeFixture.Settlement.SiegeEvent;
            var preparations = siegeEvent?.BesiegerCamp?.SiegeEngines?.SiegePreparations;
            if (siegeEvent == null || preparations == null)
                throw new InvalidOperationException("The authoritative siege graph was not created.");
            if (!preparations.IsConstructed)
            {
                preparations.SetProgress(1f);
                siegeEvent.CreateSiegeObject(
                    preparations,
                    siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker));
            }

            activeFixture.Staged = true;
            return StateJson("staged", activeFixture, activeFixture.Settlement, activeFixture.PlayerParty);
        }
        catch (Exception e)
        {
            return Error($"Fixture staging failed: {e.Message}. Run the restore command.");
        }
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_prepare_client", "coop.debug.siege")]
    public static string PrepareClient(List<string> args)
    {
        if (ModInformation.IsServer)
            return Error("Run this command on the client.");
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_prepare_client <settlementId>");
        if (!TryGetLocalSettlement(args[0], out var settlement, out var error))
            return Error(error);
        if (MobileParty.MainParty?.BesiegerCamp?.SiegeEvent != settlement.SiegeEvent)
            return Error("The replicated player siege is not ready.");
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
            return Error("Unable to resolve the siege presentation service.");

        using (new AllowedThread())
        {
            siegeEventInterface.StartLocalPlayerSiegePreparation();
        }
        return StateJson("client-prepared", null, settlement, MobileParty.MainParty);
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_request_assault", "coop.debug.siege")]
    public static string RequestAssault(List<string> args)
    {
        if (ModInformation.IsServer)
            return Error("Run this command on the client.");
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_request_assault <settlementId>");
        if (!TryGetLocalSettlement(args[0], out var settlement, out var error))
            return Error(error);
        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "menu_siege_strategies")
            return Error("The native siege strategies menu is not active.");
        if (MobileParty.MainParty?.BesiegedSettlement != settlement)
            return Error("The local player is not besieging the requested settlement.");

        var callbackArgs = new MenuCallbackArgs(Campaign.Current.CurrentMenuContext, null);
        SiegeEventCampaignBehavior.menu_order_an_assault_on_consequence(callbackArgs);
        return StateJson("assault-requested", null, settlement, MobileParty.MainParty);
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_send_troops", "coop.debug.siege")]
    public static string SendTroops(List<string> args)
    {
        if (ModInformation.IsServer)
            return Error("Run this command on the client.");
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_send_troops <settlementId>");
        if (!TryGetLocalSettlement(args[0], out var settlement, out var error))
            return Error(error);
        if (MobileParty.MainParty?.MapEvent?.IsSiegeAssault != true ||
            MobileParty.MainParty.MapEvent.MapEventSettlement != settlement ||
            PlayerEncounter.Current == null)
            return Error("The local siege-assault encounter is not ready.");
        if (PlayerEncounter.CurrentBattleSimulation != null)
            return Error("The battle simulation is already active.");

        var callbackArgs = new MenuCallbackArgs(Campaign.Current?.CurrentMenuContext, null);
        new EncounterGameMenuBehavior().game_menu_encounter_order_attack_on_consequence(callbackArgs);
        if (PlayerEncounter.CurrentBattleSimulation == null)
            return Error("The production Send Troops consequence did not open a battle simulation.");

        return StateJson("simulation-started", null, settlement, MobileParty.MainParty);
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_skip", "coop.debug.siege")]
    public static string SkipSimulation(List<string> args)
    {
        if (ModInformation.IsServer)
            return Error("Run this command on the client.");
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_skip <settlementId>");
        if (!TryGetLocalSettlement(args[0], out var settlement, out var error))
            return Error(error);
        var simulation = PlayerEncounter.CurrentBattleSimulation;
        if (simulation == null)
            return Error("No battle simulation is active.");

        simulation.Skip();
        return StateJson("simulation-skip-requested", null, settlement, MobileParty.MainParty);
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_finish_scoreboard", "coop.debug.siege")]
    public static string FinishScoreboard(List<string> args)
    {
        if (ModInformation.IsServer)
            return Error("Run this command on the client.");
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_finish_scoreboard <settlementId>");
        if (!TryGetLocalSettlement(args[0], out var settlement, out var error))
            return Error(error);

        var simulation = PlayerEncounter.CurrentBattleSimulation;
        var mapState = Game.Current?.GameStateManager?.LastOrDefault<MapState>();
        if (simulation?.IsSimulationFinished != true || mapState?.IsSimulationActive != true)
            return Error("The finished battle-simulation scoreboard is not active.");

        // Mirror the finished simulation branch of SPScoreboardVM.OnExitBattle.
        mapState.EndBattleSimulation();
        simulation.OnFinished();
        return StateJson("scoreboard-finished", null, settlement, MobileParty.MainParty);
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_state", "coop.debug.siege")]
    public static string State(List<string> args)
    {
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_state <settlementId>");
        if (!TryGetLocalSettlement(args[0], out var settlement, out var error))
            return Error(error);

        var party = GetObservedPlayerParty();
        return StateJson("observed", fixture, settlement, party);
    }

    [CommandLineArgumentFunction("auto_resolve_fixture_restore", "coop.debug.siege")]
    public static string Restore(List<string> args)
    {
        if (ModInformation.IsClient)
            return Error("Run this command on the server.");
        if (args.Count != 1)
            return Error("Usage: coop.debug.siege.auto_resolve_fixture_restore <captureJson>");
        if (fixture == null)
            return JsonResult(new { ok = true, phase = "already-restored", fixtureActive = false });

        string token;
        try
        {
            using var document = JsonDocument.Parse(args[0]);
            token = document.RootElement.GetProperty("token").GetString();
        }
        catch (Exception e)
        {
            return Error($"Unable to read the fixture capture token: {e.Message}");
        }
        if (token != fixture.Token)
            return Error("The fixture capture token does not match the active fixture.");
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            return Error("Unable to resolve fixture restore services.");

        try
        {
            var activeFixture = fixture;
            RestoreWorld(activeFixture, siegeEventInterface, behaviorSnapshot, timeControl);
            string currentFingerprint = BuildCurrentFingerprint(
                activeFixture,
                behaviorSnapshot,
                timeControl);
            if (currentFingerprint != activeFixture.BaselineFingerprint)
                throw new InvalidOperationException("The restored fixture does not match its captured baseline.");

            lastRestoration = new FixtureRestorationResult(
                activeFixture.Token,
                activeFixture.BaselineFingerprint,
                currentFingerprint);
            fixture = null;
            return JsonResult(new
            {
                ok = true,
                phase = "restored",
                fixtureActive = false,
                settlement = activeFixture.Settlement.StringId,
                restorationVerified = true,
                baselineFingerprint = activeFixture.BaselineFingerprint,
                currentFingerprint,
            });
        }
        catch (Exception e)
        {
            return Error($"Fixture restore failed: {e.Message}. Retry the restore command.");
        }
    }

    private static bool TryResolveServices(
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out ITimeControlInterface timeControl,
        out string error)
    {
        objectManager = null;
        playerManager = null;
        behaviorSnapshot = null;
        timeControl = null;
        error = null;

        if (!ContainerProvider.TryGetContainer(out var container) ||
            !container.TryResolve(out objectManager) ||
            !container.TryResolve(out playerManager) ||
            !container.TryResolve(out behaviorSnapshot) ||
            !container.TryResolve(out timeControl))
        {
            error = "Unable to resolve siege auto-resolve fixture services.";
            return false;
        }

        return true;
    }

    private static bool TryGetFixture(
        string settlementId,
        out SiegeAutoResolveFixture activeFixture,
        out string error)
    {
        activeFixture = fixture;
        error = null;
        if (activeFixture == null)
        {
            error = "Capture the siege auto-resolve fixture first.";
            return false;
        }
        if (activeFixture.Settlement.StringId != settlementId)
        {
            error = $"The active fixture belongs to {activeFixture.Settlement.StringId}.";
            return false;
        }
        return true;
    }

    private static bool TryGetLocalSettlement(string settlementId, out Settlement settlement, out string error)
    {
        settlement = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(settlementId, out settlement))
        {
            error = $"Settlement with id {settlementId} not found.";
            return false;
        }
        return true;
    }

    private static MobileParty GetObservedPlayerParty()
    {
        if (ModInformation.IsClient)
            return MobileParty.MainParty;
        if (fixture != null)
            return fixture.PlayerParty;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return null;

        var player = playerManager.Players.SingleOrDefault(playerManager.IsConnected);
        if (player == null || !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
            return null;
        return party;
    }

    private static PartySnapshot CaptureParty(
        MobileParty party,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (!behaviorSnapshot.TryCreate(party, out var behavior))
            throw new InvalidOperationException($"Unable to capture movement state for {party.StringId}.");

        return new PartySnapshot(
            party,
            party.MemberRoster.GetTroopRoster().ToArray(),
            party.PrisonRoster.GetTroopRoster().ToArray(),
            party.ItemRoster.ToArray(),
            party.LeaderHero,
            party.Position,
            party.IsActive,
            party.RecentEventsMorale,
            party.PartyTradeGold,
            behavior);
    }

    private static PartyBaseSnapshot CapturePartyBase(PartyBase party) =>
        new PartyBaseSnapshot(
            party,
            party.MemberRoster.GetTroopRoster().ToArray(),
            party.PrisonRoster.GetTroopRoster().ToArray(),
            party.ItemRoster.ToArray(),
            party.LeaderHero);

    private static HeroSnapshot CaptureHero(Hero hero) =>
        new HeroSnapshot(
            hero,
            hero.HeroState,
            hero.PartyBelongedTo,
            hero.PartyBelongedToAsPrisoner,
            hero.HitPoints,
            hero.Gold,
            hero.DeathMark,
            hero.DeathMarkKillerHero,
            Skills.All.ToDictionary(skill => skill, hero.GetSkillValue),
            hero.HeroDeveloper == null
                ? null
                : Skills.All.ToDictionary(skill => skill, hero.HeroDeveloper.GetSkillXp),
            hero.HeroDeveloper?._totalXp ?? 0,
            hero.HeroDeveloper?.UnspentFocusPoints ?? 0,
            hero.HeroDeveloper?.UnspentAttributePoints ?? 0);

    private static ClanSnapshot CaptureClan(Clan clan) =>
        new ClanSnapshot(clan, clan._influence, clan.Renown, clan._tier);

    private static void PreparePlayerParty(MobileParty party, Settlement settlement)
    {
        var leaderHero = party.LeaderHero;
        ClearRoster(party.MemberRoster);
        if (leaderHero != null)
        {
            party.MemberRoster.AddToCounts(leaderHero.CharacterObject, 1);
            party.ChangePartyLeader(leaderHero);
            leaderHero.HitPoints = Math.Max(leaderHero.HitPoints, 50);
        }

        var troop = settlement.Culture?.BasicTroop;
        if (troop == null)
            throw new InvalidOperationException("Danustica's culture has no basic troop for the fixture.");
        party.MemberRoster.AddToCounts(troop, FixturePlayerTroops - party.MemberRoster.TotalManCount);
        party.Position = settlement.GatePosition;
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(SiegeAutoResolveFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: true));
    }

    private static void PrepareDefenderParty(MobileParty party, Settlement settlement)
    {
        var leaderHero = party.LeaderHero;
        ClearRoster(party.MemberRoster);
        if (leaderHero != null)
        {
            party.MemberRoster.AddToCounts(leaderHero.CharacterObject, 1);
            party.ChangePartyLeader(leaderHero);
            leaderHero.HitPoints = Math.Max(leaderHero.HitPoints, 50);
        }

        var troop = settlement.Culture?.BasicTroop;
        if (troop == null)
            throw new InvalidOperationException("Danustica's culture has no basic troop for the fixture.");
        party.MemberRoster.AddToCounts(troop, FixtureDefenderTroops - party.MemberRoster.TotalManCount);
    }

    private static void RestoreWorld(
        SiegeAutoResolveFixture activeFixture,
        ISiegeEventInterface siegeEventInterface,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ITimeControlInterface timeControl)
    {
        var mapEvent = activeFixture.PlayerParty.MapEvent ?? activeFixture.Settlement.Party.MapEvent;
        if (mapEvent != null && !mapEvent.IsFinalized)
            mapEvent.FinalizeEvent();

        var camp = activeFixture.Settlement.SiegeEvent?.BesiegerCamp;
        var siegeParties = activeFixture.Parties
            .Select(snapshot => snapshot.Party)
            .Concat(camp?._besiegerParties ?? Enumerable.Empty<MobileParty>())
            .Distinct()
            .ToArray();
        foreach (var party in siegeParties)
        {
            party._besiegerCampResetStarted = false;
            if (party.BesiegerCamp != null)
                siegeEventInterface.BreakSiege(party);
        }

        foreach (var hero in activeFixture.Heroes)
            RestoreHero(hero);
        foreach (var party in activeFixture.Parties)
            RestoreParty(party, behaviorSnapshot);
        RestorePartyBase(activeFixture.SettlementParty);
        foreach (var hero in activeFixture.Heroes)
            RestoreHeroMembership(hero);
        foreach (var clan in activeFixture.Clans)
            RestoreClan(clan);

        timeControl.ServerSetTimeControl(activeFixture.OriginalTimeControl);
    }

    private static void RestoreParty(PartySnapshot snapshot, IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        snapshot.Party.IsActive = snapshot.WasActive;
        RestoreRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.Items)
            snapshot.Party.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
        snapshot.Party.RecentEventsMorale = snapshot.RecentEventsMorale;
        snapshot.Party.PartyTradeGold = snapshot.PartyTradeGold;
        snapshot.Party.Position = snapshot.Position;
        snapshot.Party.ChangePartyLeader(snapshot.LeaderHero);
        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            throw new InvalidOperationException($"Unable to restore movement state for {snapshot.Party.StringId}.");

        MessageBroker.Instance.Publish(
            typeof(SiegeAutoResolveFixtureCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: snapshot.Behavior.IsCurrentlyAtSea));
    }

    private static void RestorePartyBase(PartyBaseSnapshot snapshot)
    {
        RestoreRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.Items)
            snapshot.Party.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
    }

    private static void RestoreHero(HeroSnapshot snapshot)
    {
        if (snapshot.Hero.IsPrisoner)
            EndCaptivityAction.ApplyByPeace(snapshot.Hero);
        snapshot.Hero.DeathMark = snapshot.DeathMark;
        snapshot.Hero.DeathMarkKillerHero = snapshot.DeathMarkKillerHero;
        snapshot.Hero.HitPoints = snapshot.HitPoints;
        snapshot.Hero.Gold = snapshot.Gold;
        snapshot.Hero.ChangeState(snapshot.State);

        foreach (var skill in snapshot.SkillLevels)
            snapshot.Hero.SetSkillValue(skill.Key, skill.Value);
        if (snapshot.Hero.HeroDeveloper == null || snapshot.SkillXps == null)
            return;

        foreach (var skillXp in snapshot.SkillXps)
            snapshot.Hero.HeroDeveloper.SetSkillXp(skillXp.Key, skillXp.Value);
        snapshot.Hero.HeroDeveloper._totalXp = snapshot.TotalXp;
        snapshot.Hero.HeroDeveloper.UnspentFocusPoints = snapshot.UnspentFocusPoints;
        snapshot.Hero.HeroDeveloper.UnspentAttributePoints = snapshot.UnspentAttributePoints;
    }

    private static void RestoreHeroMembership(HeroSnapshot snapshot)
    {
        if (snapshot.Hero.PartyBelongedToAsPrisoner != snapshot.PrisonerParty)
        {
            if (snapshot.Hero.PartyBelongedToAsPrisoner != null)
                snapshot.Hero.OnRemovedFromPartyAsPrisoner(snapshot.Hero.PartyBelongedToAsPrisoner);
            if (snapshot.PrisonerParty != null)
                snapshot.Hero.OnAddedToPartyAsPrisoner(snapshot.PrisonerParty);
        }
        if (snapshot.Hero.PartyBelongedTo != snapshot.Party)
        {
            if (snapshot.Hero.PartyBelongedTo != null)
                snapshot.Hero.OnRemovedFromParty(snapshot.Hero.PartyBelongedTo);
            if (snapshot.Party != null)
                snapshot.Hero.OnAddedToParty(snapshot.Party);
        }
    }

    private static void RestoreClan(ClanSnapshot snapshot)
    {
        snapshot.Clan._influence = snapshot.Influence;
        snapshot.Clan.Renown = snapshot.Renown;
        snapshot.Clan._tier = snapshot.Tier;
    }

    private static string BuildCurrentFingerprint(
        SiegeAutoResolveFixture activeFixture,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ITimeControlInterface timeControl)
    {
        var output = new StringBuilder();
        output.Append("time=").Append(timeControl.GetTimeControl()).AppendLine();
        output.Append("siege=").Append(activeFixture.Settlement.SiegeEvent != null)
            .Append('|').Append(activeFixture.PlayerParty.BesiegerCamp != null)
            .Append('|').Append(activeFixture.PlayerParty.MapEvent != null)
            .AppendLine();
        AppendPartyBaseFingerprint(output, activeFixture.SettlementParty.Party);

        foreach (var snapshot in activeFixture.Parties.OrderBy(snapshot => snapshot.Party.StringId))
        {
            if (!behaviorSnapshot.TryCreate(snapshot.Party, out var behavior))
                throw new InvalidOperationException($"Unable to verify movement state for {snapshot.Party.StringId}.");

            var party = snapshot.Party;
            output.Append("party=").Append(party.StringId)
                .Append('|').Append(party.IsActive)
                .Append('|').Append(FormatPosition(party.Position))
                .Append('|').Append(party.RecentEventsMorale.ToString("R", CultureInfo.InvariantCulture))
                .Append('|').Append(party.PartyTradeGold)
                .Append('|').Append(party.LeaderHero?.StringId ?? string.Empty)
                .Append('|').Append(party._besiegerCampResetStarted)
                .Append('|').Append(FormatBehavior(behavior))
                .AppendLine();
            AppendRosterFingerprint(output, "members", party.MemberRoster);
            AppendRosterFingerprint(output, "prisoners", party.PrisonRoster);
            AppendItemsFingerprint(output, party.ItemRoster);
        }

        foreach (var snapshot in activeFixture.Heroes.OrderBy(snapshot => snapshot.Hero.StringId))
        {
            var hero = snapshot.Hero;
            output.Append("hero=").Append(hero.StringId)
                .Append('|').Append(hero.HeroState)
                .Append('|').Append(hero.PartyBelongedTo?.StringId ?? string.Empty)
                .Append('|').Append(GetPartyBaseId(hero.PartyBelongedToAsPrisoner))
                .Append('|').Append(hero.HitPoints)
                .Append('|').Append(hero.Gold)
                .Append('|').Append(hero.DeathMark)
                .Append('|').Append(hero.DeathMarkKillerHero?.StringId ?? string.Empty)
                .Append('|').Append(hero.HeroDeveloper?._totalXp ?? 0)
                .Append('|').Append(hero.HeroDeveloper?.UnspentFocusPoints ?? 0)
                .Append('|').Append(hero.HeroDeveloper?.UnspentAttributePoints ?? 0)
                .AppendLine();

            foreach (var skill in Skills.All.OrderBy(skill => skill.StringId))
            {
                output.Append("skill=").Append(skill.StringId)
                    .Append('|').Append(hero.GetSkillValue(skill))
                    .Append('|').Append((hero.HeroDeveloper?.GetSkillXp(skill) ?? 0f)
                        .ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine();
            }
        }

        foreach (var snapshot in activeFixture.Clans.OrderBy(snapshot => snapshot.Clan.StringId))
        {
            output.Append("clan=").Append(snapshot.Clan.StringId)
                .Append('|').Append(snapshot.Clan._influence.ToString("R", CultureInfo.InvariantCulture))
                .Append('|').Append(snapshot.Clan.Renown.ToString("R", CultureInfo.InvariantCulture))
                .Append('|').Append(snapshot.Clan._tier)
                .AppendLine();
        }

        using var sha256 = SHA256.Create();
        return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(output.ToString()))
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static void AppendPartyBaseFingerprint(StringBuilder output, PartyBase party)
    {
        output.Append("party-base=").Append(GetPartyBaseId(party))
            .Append('|').Append(party.LeaderHero?.StringId ?? string.Empty)
            .AppendLine();
        AppendRosterFingerprint(output, "members", party.MemberRoster);
        AppendRosterFingerprint(output, "prisoners", party.PrisonRoster);
        AppendItemsFingerprint(output, party.ItemRoster);
    }

    private static void AppendRosterFingerprint(StringBuilder output, string label, TroopRoster roster)
    {
        foreach (var element in roster.GetTroopRoster().OrderBy(element => element.Character.StringId))
        {
            output.Append(label).Append('=').Append(element.Character.StringId)
                .Append('|').Append(element.Number)
                .Append('|').Append(element.WoundedNumber)
                .Append('|').Append(element.Xp)
                .AppendLine();
        }
    }

    private static void AppendItemsFingerprint(StringBuilder output, ItemRoster roster)
    {
        foreach (var element in roster.OrderBy(element => element.EquipmentElement.ToString()))
        {
            output.Append("item=").Append(element.EquipmentElement)
                .Append('|').Append(element.Amount)
                .AppendLine();
        }
    }

    private static string FormatBehavior(PartyBehaviorUpdateData behavior) =>
        string.Join("|", new[]
        {
            behavior.NewAiBehavior.ToString(),
            behavior.InteractablePointId ?? string.Empty,
            FormatPosition(behavior.BestTargetPoint),
            behavior.DefaultBehavior.ToString(),
            FormatPosition(behavior.TargetPosition),
            behavior.DesiredAiNavigationType.ToString(),
            behavior.TargetPartyId ?? string.Empty,
            behavior.TargetSettlementId ?? string.Empty,
            FormatPosition(behavior.MoveTargetPoint),
            behavior.IsTargetingPort.ToString(),
            behavior.PartyMoveMode.ToString(),
            behavior.MoveTargetPartyId ?? string.Empty,
            behavior.IsInteractableAnchor.ToString(),
            behavior.IsCurrentlyAtSea.ToString(),
        });

    private static string FormatPosition(CampaignVec2 position) =>
        string.Join(",", new[]
        {
            position.X.ToString("R", CultureInfo.InvariantCulture),
            position.Y.ToString("R", CultureInfo.InvariantCulture),
            position.IsOnLand.ToString(),
        });

    private static string GetPartyBaseId(PartyBase party) =>
        party?.MobileParty?.StringId ?? party?.Settlement?.StringId ?? string.Empty;

    internal static bool IsFixtureHero(Hero hero) =>
        fixture?.Heroes.Any(snapshot => snapshot.Hero == hero) == true;

    private static void RestoreRoster(TroopRoster roster, TroopRosterElement[] baseline)
    {
        ClearRoster(roster);
        foreach (var element in baseline)
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
    }

    private static void ClearRoster(TroopRoster roster)
    {
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }
        roster.RemoveZeroCounts();
    }

    private static string StateJson(
        string phase,
        SiegeAutoResolveFixture activeFixture,
        Settlement settlement,
        MobileParty playerParty)
    {
        var mapState = Game.Current?.GameStateManager?.LastOrDefault<MapState>();
        var playerPartyMapEvent = playerParty?.MapEvent;
        var settlementMapEvent = settlement?.Party?.MapEvent;
        var mapEvent = playerPartyMapEvent ?? settlementMapEvent;
        var encounter = ModInformation.IsClient ? PlayerEncounter.Current : null;
        var captorParty = ModInformation.IsClient ? PlayerCaptivity.CaptorParty : null;
        return JsonResult(new
        {
            ok = true,
            phase,
            token = activeFixture?.Token,
            role = ModInformation.IsServer ? "server" : "client",
            serverAlive = ModInformation.IsServer,
            fixtureActive = activeFixture != null,
            fixtureStaged = activeFixture?.Staged ?? false,
            restorationVerified = lastRestoration?.Verified ?? false,
            baselineFingerprint = activeFixture?.BaselineFingerprint ?? lastRestoration?.BaselineFingerprint,
            currentFingerprint = activeFixture == null ? lastRestoration?.CurrentFingerprint : null,
            settlement = settlement?.StringId,
            siegeActive = settlement?.SiegeEvent != null,
            siegeGraphComplete = settlement?.SiegeEvent?.BesiegerCamp?.SiegeEngines != null && settlement.SiegeEngines != null,
            playerParty = playerParty?.StringId,
            factionsAtWar = playerParty?.MapFaction?.IsAtWarWith(settlement?.MapFaction) == true,
            playerBesieger = playerParty?.BesiegerCamp?.SiegeEvent == settlement?.SiegeEvent,
            mapEvent = mapEvent?.StringId,
            playerPartyMapEvent = playerPartyMapEvent?.StringId,
            settlementMapEvent = settlementMapEvent?.StringId,
            siegeAssault = mapEvent?.IsSiegeAssault == true,
            battleState = mapEvent?.BattleState.ToString(),
            menu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
            aftermathMenu = IsSiegeAftermathMenu(Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId),
            encounterActive = encounter != null,
            locationEncounterActive = ModInformation.IsClient && PlayerEncounter.LocationEncounter != null,
            encounterState = encounter?.EncounterState.ToString(),
            playerCaptive = ModInformation.IsClient && PlayerCaptivity.IsCaptive,
            captorParty = GetPartyBaseId(captorParty),
            captorIsSettlement = captorParty?.IsSettlement == true,
            simulationActive = mapState?.IsSimulationActive == true,
            simulationFinished = encounter?.BattleSimulation?.IsSimulationFinished,
        });
    }

    private static bool IsSiegeAftermathMenu(string menuId) =>
        menuId != null && (menuId.StartsWith("menu_settlement_taken", StringComparison.Ordinal) ||
                           menuId == "siege_aftermath_contextual_summary");

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonSerializer.Serialize(value);

    private static string Error(string message) =>
        JsonResult(new { ok = false, error = message });

    private sealed class SiegeAutoResolveFixture
    {
        public string Token { get; }
        public Settlement Settlement { get; }
        public MobileParty PlayerParty { get; }
        public PartyBaseSnapshot SettlementParty { get; }
        public PartySnapshot[] Parties { get; }
        public HeroSnapshot[] Heroes { get; }
        public ClanSnapshot[] Clans { get; }
        public TimeControlEnum OriginalTimeControl { get; }
        public string BaselineFingerprint { get; set; }
        public bool Staged { get; set; }

        public SiegeAutoResolveFixture(
            string token,
            Settlement settlement,
            MobileParty playerParty,
            PartyBaseSnapshot settlementParty,
            PartySnapshot[] parties,
            HeroSnapshot[] heroes,
            ClanSnapshot[] clans,
            TimeControlEnum originalTimeControl)
        {
            Token = token;
            Settlement = settlement;
            PlayerParty = playerParty;
            SettlementParty = settlementParty;
            Parties = parties;
            Heroes = heroes;
            Clans = clans;
            OriginalTimeControl = originalTimeControl;
        }
    }

    private sealed class FixtureRestorationResult
    {
        public string Token { get; }
        public string BaselineFingerprint { get; }
        public string CurrentFingerprint { get; }
        public bool Verified => BaselineFingerprint == CurrentFingerprint;

        public FixtureRestorationResult(
            string token,
            string baselineFingerprint,
            string currentFingerprint)
        {
            Token = token;
            BaselineFingerprint = baselineFingerprint;
            CurrentFingerprint = currentFingerprint;
        }
    }

    private sealed class PartyBaseSnapshot
    {
        public PartyBase Party { get; }
        public TroopRosterElement[] MemberRoster { get; }
        public TroopRosterElement[] PrisonRoster { get; }
        public ItemRosterElement[] Items { get; }
        public Hero LeaderHero { get; }

        public PartyBaseSnapshot(
            PartyBase party,
            TroopRosterElement[] memberRoster,
            TroopRosterElement[] prisonRoster,
            ItemRosterElement[] items,
            Hero leaderHero)
        {
            Party = party;
            MemberRoster = memberRoster;
            PrisonRoster = prisonRoster;
            Items = items;
            LeaderHero = leaderHero;
        }
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party { get; }
        public TroopRosterElement[] MemberRoster { get; }
        public TroopRosterElement[] PrisonRoster { get; }
        public ItemRosterElement[] Items { get; }
        public Hero LeaderHero { get; }
        public CampaignVec2 Position { get; }
        public bool WasActive { get; }
        public float RecentEventsMorale { get; }
        public int PartyTradeGold { get; }
        public PartyBehaviorUpdateData Behavior { get; }

        public PartySnapshot(
            MobileParty party,
            TroopRosterElement[] memberRoster,
            TroopRosterElement[] prisonRoster,
            ItemRosterElement[] items,
            Hero leaderHero,
            CampaignVec2 position,
            bool wasActive,
            float recentEventsMorale,
            int partyTradeGold,
            PartyBehaviorUpdateData behavior)
        {
            Party = party;
            MemberRoster = memberRoster;
            PrisonRoster = prisonRoster;
            Items = items;
            LeaderHero = leaderHero;
            Position = position;
            WasActive = wasActive;
            RecentEventsMorale = recentEventsMorale;
            PartyTradeGold = partyTradeGold;
            Behavior = behavior;
        }
    }

    private sealed class HeroSnapshot
    {
        public Hero Hero { get; }
        public Hero.CharacterStates State { get; }
        public MobileParty Party { get; }
        public PartyBase PrisonerParty { get; }
        public int HitPoints { get; }
        public int Gold { get; }
        public KillCharacterAction.KillCharacterActionDetail DeathMark { get; }
        public Hero DeathMarkKillerHero { get; }
        public Dictionary<SkillObject, int> SkillLevels { get; }
        public Dictionary<SkillObject, float> SkillXps { get; }
        public int TotalXp { get; }
        public int UnspentFocusPoints { get; }
        public int UnspentAttributePoints { get; }

        public HeroSnapshot(
            Hero hero,
            Hero.CharacterStates state,
            MobileParty party,
            PartyBase prisonerParty,
            int hitPoints,
            int gold,
            KillCharacterAction.KillCharacterActionDetail deathMark,
            Hero deathMarkKillerHero,
            Dictionary<SkillObject, int> skillLevels,
            Dictionary<SkillObject, float> skillXps,
            int totalXp,
            int unspentFocusPoints,
            int unspentAttributePoints)
        {
            Hero = hero;
            State = state;
            Party = party;
            PrisonerParty = prisonerParty;
            HitPoints = hitPoints;
            Gold = gold;
            DeathMark = deathMark;
            DeathMarkKillerHero = deathMarkKillerHero;
            SkillLevels = skillLevels;
            SkillXps = skillXps;
            TotalXp = totalXp;
            UnspentFocusPoints = unspentFocusPoints;
            UnspentAttributePoints = unspentAttributePoints;
        }
    }

    private sealed class ClanSnapshot
    {
        public Clan Clan { get; }
        public float Influence { get; }
        public float Renown { get; }
        public int Tier { get; }

        public ClanSnapshot(Clan clan, float influence, float renown, int tier)
        {
            Clan = clan;
            Influence = influence;
            Renown = renown;
            Tier = tier;
        }
    }
}

[HarmonyPatch(typeof(Hero), nameof(Hero.CanDie))]
internal static class SiegeAutoResolveFixtureHeroDeathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Hero __instance,
        KillCharacterAction.KillCharacterActionDetail causeOfDeath,
        ref bool __result)
    {
        if (causeOfDeath != KillCharacterAction.KillCharacterActionDetail.DiedInBattle ||
            !SiegeAutoResolveFixtureCommands.IsFixtureHero(__instance))
            return true;

        __result = false;
        return false;
    }
}
#endif
