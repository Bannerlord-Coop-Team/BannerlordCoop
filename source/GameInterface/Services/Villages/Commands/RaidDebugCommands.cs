using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using Newtonsoft.Json;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Villages.Commands;

public class RaidDebugCommands
{
    private static RaidLootWarningFixture raidLootWarningFixture;
    private static InquiryData pendingLootWarningInquiry;

    [CommandLineArgumentFunction("allow_raid_ai_intervention", "coop.debug.mapevent")]
    public static string AllowRaidAiIntervention(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.mapevent.allow_raid_ai_intervention <on|off|toggle|status>";
        }

        var value = args[0].ToLowerInvariant();
        switch (value)
        {
            case "on":
            case "true":
            case "1":
                return ApplyRaidAiInterventionConfig(true);
            case "off":
            case "false":
            case "0":
                return ApplyRaidAiInterventionConfig(false);
            case "toggle":
                return ApplyRaidAiInterventionConfig(!MapEventConfig.AllowRaidAiIntervention);
            case "status":
                return RaidAiInterventionConfigHandler.StatusText;
            default:
                return "Usage: coop.debug.mapevent.allow_raid_ai_intervention <on|off|toggle|status>";
        }
    }

    private static string ApplyRaidAiInterventionConfig(bool allow)
    {
        MapEventConfig.AllowRaidAiIntervention = allow;

        if (ModInformation.IsServer)
        {
            if (ContainerProvider.TryResolve<RaidAiInterventionConfigHandler>(out var handler))
                handler.SetAndBroadcast(allow);

            return RaidAiInterventionConfigHandler.StatusText;
        }

        if (ContainerProvider.TryResolve<INetwork>(out var network))
            network.SendAll(new NetworkRequestRaidAiInterventionConfigChange(allow));

        return RaidAiInterventionConfigHandler.StatusText + " (server update requested)";
    }

    [CommandLineArgumentFunction("raid_loot_warning_capture", "coop.debug.mapevent")]
    public static string CaptureRaidLootWarningFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_capture <controllerId> <settlementId>";
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 2) return usage;
        if (raidLootWarningFixture?.Campaign == Campaign.Current)
            return "A raid loot-warning fixture is already pending restoration.";
        raidLootWarningFixture = null;

        if (!TryResolveRaidFixtureServices(
                out var objectManager,
                out var playerManager,
                out _,
                out _,
                out _))
            return "Unable to resolve raid loot-warning fixture services.";

        if (!playerManager.TryGetPlayer(args[0], out var player) ||
            !playerManager.TryGetPeer(args[0], out var peer) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
            return $"Connected player '{args[0]}' was not found.";

        if (!objectManager.TryGetObject<Settlement>(args[1], out var settlement))
            settlement = Settlement.Find(args[1]);

        if (settlement?.Village == null)
            return $"Village settlement '{args[1]}' was not found.";
        if (playerParty.MapFaction == null || settlement.MapFaction == null ||
            playerParty.MapFaction == settlement.MapFaction)
            return "The player and village must belong to different factions.";
        if (playerParty.MapEvent != null || settlement.Party?.MapEvent != null)
            return "The player party and village must be outside a map event.";
        if (playerParty.PartyMoveMode != MoveModeType.Hold)
            return "The player party must be holding before the fixture is captured.";

        // Capture before staging war so restoration returns to the original diplomatic state.
        var factionState = CaptureFactionState(playerParty.MapFaction, settlement.MapFaction);
        if (!factionState.WasAtWar)
            DeclareWarAction.ApplyByDefault(playerParty.MapFaction, settlement.MapFaction);

        var token = "raid-loot-warning-" + Guid.NewGuid().ToString("N");
        raidLootWarningFixture = new RaidLootWarningFixture(
            token,
            Campaign.Current,
            playerParty,
            peer,
            settlement,
            playerParty.CurrentSettlement,
            playerParty.Position,
            settlement.Village.VillageState,
            settlement.SettlementHitPoints,
            CaptureParty(playerParty.Party),
            CaptureParty(settlement.Party),
            CaptureHeroes(playerParty.Party, settlement.Party),
            CaptureClans(playerParty, settlement),
            factionState);

        return LiveTestJson(token);
    }

    [CommandLineArgumentFunction("raid_loot_warning_position", "coop.debug.mapevent")]
    public static string PositionRaidLootWarningFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_position <snapshotToken>";
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1) return usage;
        if (!TryGetRaidLootWarningFixture(args[0], out var fixture, out var error)) return error;
        if (fixture.Positioned) return "The raid loot-warning fixture is already positioned.";

        if (!TryResolveRaidFixtureServices(
                out _,
                out _,
                out var settlementInterface,
                out _,
                out _))
            return "Unable to resolve raid loot-warning fixture services.";

        try
        {
            if (fixture.PlayerParty.CurrentSettlement != fixture.Settlement)
            {
                settlementInterface.PartyLeaveSettlement(fixture.PlayerParty);
                fixture.PlayerParty.Position = fixture.Settlement.GatePosition;
                HoldAndPublishPosition(fixture.PlayerParty);
                settlementInterface.PartyEnterSettlement(fixture.PlayerParty, fixture.Settlement);
            }

            fixture.Settlement.Village.VillageState = Village.VillageStates.Normal;
            fixture.Settlement.SettlementHitPoints = 1f;

            if (!AreFactionsAtWar(fixture.PlayerParty.MapFaction, fixture.Settlement.MapFaction))
                return "The fixture factions are no longer at war.";

            var playerHero = fixture.PlayerParty.LeaderHero;
            if (playerHero == null)
                return "The fixture player party needs a leader hero.";
            var basicTroop = playerHero.Culture?.BasicTroop;
            if (basicTroop == null)
                return "The fixture player hero needs a culture basic troop.";
            fixture.PlayerParty.MemberRoster.AddToCounts(basicTroop, 100);

            fixture.Positioned = true;
            return LiveTestJson(fixture.Token);
        }
        catch (Exception e)
        {
            return $"Failed to position the raid loot-warning fixture: {e.Message}. Run the restore command.";
        }
    }

    [CommandLineArgumentFunction("raid_loot_warning_enter", "coop.debug.mapevent")]
    public static string EnterRaidLootWarningEncounter(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_enter <settlementId>";
        if (ModInformation.IsServer) return "Run this command on the client.";
        if (args.Count != 1) return usage;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve the object manager.";
        if (!objectManager.TryGetObjectWithLogging<Settlement>(args[0], out var settlement))
            return $"Village settlement '{args[0]}' was not found.";

        var mainParty = MobileParty.MainParty;
        if (mainParty?.CurrentSettlement != settlement)
            return "The local player party has not reached the fixture village.";
        if (PlayerEncounter.Current != null)
            return "The local player already has an encounter.";

        EncounterManager.StartSettlementEncounter(mainParty, settlement);
        return LiveTestJson(new { success = true, requestedSettlementId = settlement.StringId });
    }

    [CommandLineArgumentFunction("raid_loot_warning_prepare", "coop.debug.mapevent")]
    public static string PrepareRaidLootWarningFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_prepare <snapshotToken>";
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1) return usage;
        if (!TryGetRaidLootWarningFixture(args[0], out var fixture, out var error)) return error;
        if (fixture.Prepared) return "The raid loot-warning fixture is already prepared.";
        if (!fixture.Positioned) return "Position the raid loot-warning fixture before preparing it.";

        if (!TryResolveRaidFixtureServices(
                out var objectManager,
                out _,
                out _,
                out var hostileActionInterface,
                out var network))
            return "Unable to resolve raid loot-warning fixture services.";

        var playerParty = fixture.PlayerParty;
        var settlement = fixture.Settlement;
        if (playerParty.MapEvent != null || settlement.Party?.MapEvent != null)
            return "The player party and village entered a map event after capture.";
        if (playerParty.CurrentSettlement != settlement)
            return "The player party left the fixture village before preparation.";
        if (!AreFactionsAtWar(playerParty.MapFaction, settlement.MapFaction))
            return "The fixture factions are not at war.";

        try
        {
            if (!hostileActionInterface.CanStartHostileAction(
                    playerParty,
                    settlement,
                    VillageHostileAction.Raid,
                    out var deniedReason))
                return $"The raid fixture could not start: {deniedReason}.";

            hostileActionInterface.ApplyHostileAction(playerParty, settlement, VillageHostileAction.Raid);
            hostileActionInterface.ApproveMapEventStart(playerParty.Party, settlement, VillageHostileAction.Raid);

            if (!objectManager.TryGetId(playerParty, out var mobilePartyId) ||
                !objectManager.TryGetId(settlement, out var settlementId))
                return "Unable to resolve the raid fixture network ids.";

            fixture.Prepared = true;
            network.Send(fixture.Peer, new NetworkVillageHostileActionStarted(
                VillageHostileAction.Raid,
                mobilePartyId,
                settlementId));

            return LiveTestJson(fixture.Token);
        }
        catch (Exception e)
        {
            return $"Failed to prepare the raid loot-warning fixture: {e.Message}. Run the restore command.";
        }
    }

    [CommandLineArgumentFunction("raid_loot_warning_state", "coop.debug.mapevent")]
    public static string GetRaidLootWarningState(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the client.";
        if (args.Count != 0) return "Usage: coop.debug.mapevent.raid_loot_warning_state";

        var inventoryScreen = ScreenManager.TopScreen as GauntletInventoryScreen;
        var inventoryVm = inventoryScreen?._dataSource;
        var otherItemCount = inventoryVm?._inventoryLogic?.GetElementCountOnSide(
            InventoryLogic.InventorySide.OtherInventory) ?? 0;
        var mapEvent = MapEvent.PlayerMapEvent;
        var mapEventId = string.Empty;
        if (mapEvent != null && ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            objectManager.TryGetId(mapEvent, out mapEventId);

        return LiveTestJson(new
        {
            success = true,
            inventoryActive = Game.Current?.GameStateManager?.ActiveState is InventoryState,
            topScreenIsInventory = inventoryScreen != null,
            otherItemCount,
            warningActive = InformationManager.IsAnyInquiryActive(),
            encounterState = PlayerEncounter.Current?.EncounterState.ToString() ?? "none",
            encounterSettlementId = PlayerEncounter.EncounterSettlement?.StringId ?? string.Empty,
            mapEventId = mapEventId ?? string.Empty,
            menuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? string.Empty,
            settlementId = Settlement.CurrentSettlement?.StringId ?? string.Empty
        });
    }

    [CommandLineArgumentFunction("raid_loot_warning_show", "coop.debug.mapevent")]
    public static string ShowRaidLootWarning(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the client.";
        if (args.Count != 0) return "Usage: coop.debug.mapevent.raid_loot_warning_show";
        if (!(ScreenManager.TopScreen is GauntletInventoryScreen inventoryScreen))
            return "The raid loot inventory is not the top screen.";
        if (InformationManager.IsAnyInquiryActive())
            return "An inquiry is already active.";

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
            !string.Equals(capturedInquiry.Text, expectedText, StringComparison.Ordinal))
            return "The leaving-loot-behind warning did not open.";

        pendingLootWarningInquiry = capturedInquiry;
        return LiveTestJson(new
        {
            success = true,
            warningActive = InformationManager.IsAnyInquiryActive(),
            warningText = capturedInquiry.Text
        });
    }

    [CommandLineArgumentFunction("raid_loot_warning_accept", "coop.debug.mapevent")]
    public static string AcceptRaidLootWarning(List<string> args)
    {
        if (ModInformation.IsServer) return "Run this command on the client.";
        if (args.Count != 0) return "Usage: coop.debug.mapevent.raid_loot_warning_accept";

        var inquiry = pendingLootWarningInquiry;
        if (inquiry?.AffirmativeAction == null || !InformationManager.IsAnyInquiryActive())
            return "The leaving-loot-behind warning is not active.";

        pendingLootWarningInquiry = null;
        InformationManager.HideInquiry();
        inquiry.AffirmativeAction();

        return LiveTestJson(new
        {
            success = true,
            inventoryActive = Game.Current?.GameStateManager?.ActiveState is InventoryState,
            warningActive = InformationManager.IsAnyInquiryActive(),
            encounterState = PlayerEncounter.Current?.EncounterState.ToString() ?? "none",
            settlementId = Settlement.CurrentSettlement?.StringId ?? string.Empty
        });
    }

    [CommandLineArgumentFunction("raid_loot_warning_restore", "coop.debug.mapevent")]
    public static string RestoreRaidLootWarningFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_restore <snapshotToken>";
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1) return usage;
        if (!TryGetRaidLootWarningFixture(args[0], out var fixture, out var error)) return error;

        if (!TryResolveRaidFixtureServices(
                out _,
                out _,
                out var settlementInterface,
                out _,
                out _))
            return "Unable to resolve raid loot-warning fixture services.";

        try
        {
            if (fixture.PlayerParty.MapEvent is MapEvent mapEvent && !mapEvent.IsFinalized)
                mapEvent.FinalizeEvent();
            if (fixture.PlayerParty.MapEvent != null)
                return "The raid map event is still attached; retry the restore command.";

            if (fixture.PlayerParty.CurrentSettlement != null &&
                fixture.PlayerParty.CurrentSettlement != fixture.OriginalSettlement)
                settlementInterface.PartyLeaveSettlement(fixture.PlayerParty);

            foreach (var hero in fixture.Heroes)
                RestoreHeroProgression(hero);
            RestoreParty(fixture.PlayerPartySnapshot);
            RestoreParty(fixture.SettlementPartySnapshot);
            foreach (var hero in fixture.Heroes)
                RestoreHeroMembership(hero);
            foreach (var clan in fixture.Clans)
                RestoreClan(clan);

            fixture.Settlement.Village.VillageState = fixture.OriginalVillageState;
            fixture.Settlement.SettlementHitPoints = fixture.OriginalSettlementHitPoints;

            if (fixture.OriginalSettlement != null &&
                fixture.PlayerParty.CurrentSettlement != fixture.OriginalSettlement)
            {
                fixture.PlayerParty.Position = fixture.OriginalSettlement.GatePosition;
                HoldAndPublishPosition(fixture.PlayerParty);
                settlementInterface.PartyEnterSettlement(fixture.PlayerParty, fixture.OriginalSettlement);
            }

            fixture.PlayerParty.Position = fixture.OriginalPosition;
            HoldAndPublishPosition(fixture.PlayerParty);

            RestoreFactionState(fixture.FactionState);

            fixture.Restored = true;
            return LiveTestJson(fixture.Token);
        }
        catch (Exception e)
        {
            return $"Failed to restore the raid loot-warning fixture: {e.Message}. Retry the restore command.";
        }
    }

    [CommandLineArgumentFunction("raid_loot_warning_verify", "coop.debug.mapevent")]
    public static string VerifyRaidLootWarningFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_verify <snapshotToken>";
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1) return usage;
        if (!TryGetRaidLootWarningFixture(args[0], out var fixture, out var error)) return error;

        var restored = fixture.Restored &&
            fixture.PlayerParty.MapEvent == null &&
            fixture.PlayerParty.CurrentSettlement == fixture.OriginalSettlement &&
            fixture.Settlement.Village.VillageState == fixture.OriginalVillageState &&
            Math.Abs(fixture.Settlement.SettlementHitPoints - fixture.OriginalSettlementHitPoints) < 0.001f &&
            PartyMatches(fixture.PlayerPartySnapshot) &&
            PartyMatches(fixture.SettlementPartySnapshot) &&
            fixture.Heroes.All(HeroMatches) &&
            fixture.Clans.All(ClanMatches) &&
            FactionStateMatches(fixture.FactionState) &&
            fixture.PlayerParty.Position == fixture.OriginalPosition;

        if (restored)
            raidLootWarningFixture = null;

        return LiveTestJson(restored);
    }

    private static bool TryResolveRaidFixtureServices(
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out ISettlementInterface settlementInterface,
        out IVillageHostileActionInterface hostileActionInterface,
        out INetwork network)
    {
        objectManager = null;
        playerManager = null;
        settlementInterface = null;
        hostileActionInterface = null;
        network = null;

        return ContainerProvider.TryResolve(out objectManager) &&
               ContainerProvider.TryResolve(out playerManager) &&
               ContainerProvider.TryResolve(out settlementInterface) &&
               ContainerProvider.TryResolve(out hostileActionInterface) &&
               ContainerProvider.TryResolve(out network);
    }

    private static bool TryGetRaidLootWarningFixture(
        string token,
        out RaidLootWarningFixture fixture,
        out string error)
    {
        fixture = raidLootWarningFixture;
        error = string.Empty;
        if (fixture == null)
        {
            error = "No raid loot-warning fixture is pending restoration.";
            return false;
        }
        if (fixture.Campaign != Campaign.Current || fixture.Token != token)
        {
            error = "The raid loot-warning fixture token does not match the current campaign.";
            return false;
        }

        return true;
    }

    internal static bool IsFixtureHero(Hero hero) =>
        raidLootWarningFixture?.Heroes.Any(snapshot => snapshot.Hero == hero) == true;

    private static void HoldAndPublishPosition(MobileParty party)
    {
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(RaidDebugCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea,
                resetMovementToHold: true));
    }

    private static bool AreFactionsAtWar(IFaction first, IFaction second)
    {
        if (first == null || second == null) return false;

        try
        {
            return FactionManager.IsAtWarAgainstFaction(first, second);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static PartySnapshot CaptureParty(PartyBase party) =>
        new PartySnapshot(
            party,
            party.MemberRoster.GetTroopRoster().ToArray(),
            party.PrisonRoster.GetTroopRoster().ToArray(),
            party.ItemRoster.ToArray(),
            party.MobileParty?.RecentEventsMorale ?? 0f,
            party.MobileParty?.PartyTradeGold ?? 0,
            party.MobileParty?.LeaderHero);

    private static HeroSnapshot[] CaptureHeroes(params PartyBase[] parties) =>
        parties
            .Where(party => party != null)
            .SelectMany(party => party.MemberRoster.GetTroopRoster()
                .Select(element => element.Character?.HeroObject)
                .Concat(party.PrisonRoster.GetTroopRoster()
                    .Select(element => element.Character?.HeroObject))
                .Concat(new[] { party.LeaderHero }))
            .Where(hero => hero != null)
            .Distinct()
            .Select(CaptureHero)
            .ToArray();

    private static HeroSnapshot CaptureHero(Hero hero) =>
        new HeroSnapshot(
            hero,
            hero.HeroState,
            hero.PartyBelongedTo,
            hero.PartyBelongedToAsPrisoner,
            hero.HitPoints,
            hero.Gold,
            hero.Level,
            hero.DeathMark,
            hero.DeathMarkKillerHero,
            Skills.All.ToDictionary(skill => skill, hero.GetSkillValue),
            hero.HeroDeveloper == null
                ? null
                : Skills.All.ToDictionary(skill => skill, hero.HeroDeveloper.GetSkillXp),
            hero.HeroDeveloper?._totalXp ?? 0,
            hero.HeroDeveloper?.UnspentFocusPoints ?? 0,
            hero.HeroDeveloper?.UnspentAttributePoints ?? 0);

    private static ClanSnapshot[] CaptureClans(MobileParty playerParty, Settlement settlement) =>
        new[] { playerParty.ActualClan, playerParty.LeaderHero?.Clan, settlement.OwnerClan }
            .Where(clan => clan != null)
            .Distinct()
            .Select(clan => new ClanSnapshot(clan, clan._influence, clan.Renown, clan._tier))
            .ToArray();

    private static FactionStateSnapshot CaptureFactionState(IFaction first, IFaction second)
    {
        var stance = first.GetStanceWith(second);
        return new FactionStateSnapshot(
            first,
            second,
            AreFactionsAtWar(first, second),
            stance,
            stance.StanceType,
            stance.BehaviorPriority,
            stance._warStartDate,
            stance._peaceDeclarationDate,
            stance.TroopCasualties1,
            stance.TroopCasualties2,
            stance.ShipCasualties1,
            stance.ShipCasualties2,
            stance.SuccessfulSieges1,
            stance.SuccessfulSieges2,
            stance.SuccessfulRaids1,
            stance.SuccessfulRaids2,
            stance.SuccessfulTownSieges1,
            stance.SuccessfulTownSieges2,
            stance.TotalTributePaidFrom1To2,
            stance._dailyTributeFrom1To2,
            stance.DailyTributeInstallments,
            (first as Kingdom)?.PoliticalStagnation,
            (second as Kingdom)?.PoliticalStagnation);
    }

    private static void RestoreParty(PartySnapshot snapshot)
    {
        RestoreRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.Items)
            snapshot.Party.ItemRoster.Add(element);

        if (snapshot.Party.MobileParty == null) return;

        snapshot.Party.MobileParty.RecentEventsMorale = snapshot.RecentEventsMorale;
        snapshot.Party.MobileParty.PartyTradeGold = snapshot.PartyTradeGold;
        snapshot.Party.MobileParty.ChangePartyLeader(snapshot.LeaderHero);
    }

    private static void RestoreRoster(TroopRoster roster, TroopRosterElement[] baseline)
    {
        for (int index = roster.Count - 1; index >= 0; index--)
        {
            var element = roster.GetElementCopyAtIndex(index);
            roster.AddToCountsAtIndex(
                index,
                -element.Number,
                -element.WoundedNumber,
                0,
                false);
        }
        roster.RemoveZeroCounts();

        foreach (var element in baseline)
        {
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }
    }

    private static void RestoreHeroProgression(HeroSnapshot snapshot)
    {
        if (snapshot.PrisonerParty == null && snapshot.Hero.IsPrisoner)
            EndCaptivityAction.ApplyByPeace(snapshot.Hero);

        snapshot.Hero.DeathMark = snapshot.DeathMark;
        snapshot.Hero.DeathMarkKillerHero = snapshot.DeathMarkKillerHero;
        snapshot.Hero.HitPoints = snapshot.HitPoints;
        snapshot.Hero.Gold = snapshot.Gold;
        snapshot.Hero.Level = snapshot.Level;
        snapshot.Hero.ChangeState(snapshot.State);

        foreach (var skill in snapshot.SkillLevels)
            snapshot.Hero.SetSkillValue(skill.Key, skill.Value);

        if (snapshot.Hero.HeroDeveloper == null || snapshot.SkillXps == null)
            return;

        foreach (var skillXp in snapshot.SkillXps)
            snapshot.Hero.HeroDeveloper.SetSkillXp(skillXp.Key, skillXp.Value);
        snapshot.Hero.HeroDeveloper.TotalXp = snapshot.TotalXp;
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
        float influenceDelta = snapshot.Influence - snapshot.Clan.Influence;
        if (Math.Abs(influenceDelta) >= 0.001f)
            ChangeClanInfluenceAction.Apply(snapshot.Clan, influenceDelta);
        if (Math.Abs(snapshot.Clan.Renown - snapshot.Renown) >= 0.001f)
        {
            snapshot.Clan.ResetClanRenown();
            if (snapshot.Renown > 0f)
                snapshot.Clan.AddRenown(snapshot.Renown, shouldNotify: false);
        }
    }

    private static void RestoreFactionState(FactionStateSnapshot snapshot)
    {
        if (AreFactionsAtWar(snapshot.First, snapshot.Second) != snapshot.WasAtWar)
        {
            if (snapshot.WasAtWar)
                DeclareWarAction.ApplyByDefault(snapshot.First, snapshot.Second);
            else
                MakePeaceAction.Apply(snapshot.First, snapshot.Second);
        }

        var stance = snapshot.Stance;
        stance._stanceType = snapshot.StanceType;
        stance.BehaviorPriority = snapshot.BehaviorPriority;
        stance._warStartDate = snapshot.WarStartDate;
        stance._peaceDeclarationDate = snapshot.PeaceDeclarationDate;
        stance.TroopCasualties1 = snapshot.TroopCasualties1;
        stance.TroopCasualties2 = snapshot.TroopCasualties2;
        stance.ShipCasualties1 = snapshot.ShipCasualties1;
        stance.ShipCasualties2 = snapshot.ShipCasualties2;
        stance.SuccessfulSieges1 = snapshot.SuccessfulSieges1;
        stance.SuccessfulSieges2 = snapshot.SuccessfulSieges2;
        stance.SuccessfulRaids1 = snapshot.SuccessfulRaids1;
        stance.SuccessfulRaids2 = snapshot.SuccessfulRaids2;
        stance.SuccessfulTownSieges1 = snapshot.SuccessfulTownSieges1;
        stance.SuccessfulTownSieges2 = snapshot.SuccessfulTownSieges2;
        stance.TotalTributePaidFrom1To2 = snapshot.TotalTributePaidFrom1To2;
        stance._dailyTributeFrom1To2 = snapshot.DailyTributeFrom1To2;
        stance.DailyTributeInstallments = snapshot.DailyTributeInstallments;
        if (snapshot.First is Kingdom firstKingdom && snapshot.FirstPoliticalStagnation.HasValue)
            firstKingdom.PoliticalStagnation = snapshot.FirstPoliticalStagnation.Value;
        if (snapshot.Second is Kingdom secondKingdom && snapshot.SecondPoliticalStagnation.HasValue)
            secondKingdom.PoliticalStagnation = snapshot.SecondPoliticalStagnation.Value;
    }

    private static bool PartyMatches(PartySnapshot snapshot)
    {
        if (!RosterMatches(snapshot.Party.MemberRoster, snapshot.MemberRoster) ||
            !RosterMatches(snapshot.Party.PrisonRoster, snapshot.PrisonRoster) ||
            !snapshot.Party.ItemRoster.SequenceEqual(snapshot.Items))
            return false;

        var mobileParty = snapshot.Party.MobileParty;
        return mobileParty == null ||
            (Math.Abs(mobileParty.RecentEventsMorale - snapshot.RecentEventsMorale) < 0.001f &&
             mobileParty.PartyTradeGold == snapshot.PartyTradeGold &&
             mobileParty.LeaderHero == snapshot.LeaderHero);
    }

    private static bool RosterMatches(TroopRoster roster, TroopRosterElement[] baseline)
    {
        var current = roster.GetTroopRoster();
        if (current.Count != baseline.Length) return false;

        for (int index = 0; index < baseline.Length; index++)
        {
            var first = current[index];
            var second = baseline[index];
            if (first.Character != second.Character ||
                first.Number != second.Number ||
                first.WoundedNumber != second.WoundedNumber ||
                first.Xp != second.Xp)
                return false;
        }

        return true;
    }

    private static bool HeroMatches(HeroSnapshot snapshot)
    {
        if (snapshot.Hero.HeroState != snapshot.State ||
            snapshot.Hero.PartyBelongedTo != snapshot.Party ||
            snapshot.Hero.PartyBelongedToAsPrisoner != snapshot.PrisonerParty ||
            snapshot.Hero.HitPoints != snapshot.HitPoints ||
            snapshot.Hero.Gold != snapshot.Gold ||
            snapshot.Hero.Level != snapshot.Level ||
            snapshot.Hero.DeathMark != snapshot.DeathMark ||
            snapshot.Hero.DeathMarkKillerHero != snapshot.DeathMarkKillerHero ||
            snapshot.SkillLevels.Any(skill => snapshot.Hero.GetSkillValue(skill.Key) != skill.Value))
            return false;

        if (snapshot.Hero.HeroDeveloper == null || snapshot.SkillXps == null)
            return true;

        return snapshot.SkillXps.All(skill =>
                   Math.Abs(snapshot.Hero.HeroDeveloper.GetSkillXp(skill.Key) - skill.Value) < 0.001f) &&
               Math.Abs(snapshot.Hero.HeroDeveloper._totalXp - snapshot.TotalXp) < 0.001f &&
               snapshot.Hero.HeroDeveloper.UnspentFocusPoints == snapshot.UnspentFocusPoints &&
               snapshot.Hero.HeroDeveloper.UnspentAttributePoints == snapshot.UnspentAttributePoints;
    }

    private static bool ClanMatches(ClanSnapshot snapshot) =>
        Math.Abs(snapshot.Clan._influence - snapshot.Influence) < 0.001f &&
        Math.Abs(snapshot.Clan.Renown - snapshot.Renown) < 0.001f &&
        snapshot.Clan._tier == snapshot.Tier;

    private static bool FactionStateMatches(FactionStateSnapshot snapshot)
    {
        var stance = snapshot.Stance;
        return AreFactionsAtWar(snapshot.First, snapshot.Second) == snapshot.WasAtWar &&
            stance.StanceType == snapshot.StanceType &&
            stance.BehaviorPriority == snapshot.BehaviorPriority &&
            stance._warStartDate == snapshot.WarStartDate &&
            stance._peaceDeclarationDate == snapshot.PeaceDeclarationDate &&
            stance.TroopCasualties1 == snapshot.TroopCasualties1 &&
            stance.TroopCasualties2 == snapshot.TroopCasualties2 &&
            stance.ShipCasualties1 == snapshot.ShipCasualties1 &&
            stance.ShipCasualties2 == snapshot.ShipCasualties2 &&
            stance.SuccessfulSieges1 == snapshot.SuccessfulSieges1 &&
            stance.SuccessfulSieges2 == snapshot.SuccessfulSieges2 &&
            stance.SuccessfulRaids1 == snapshot.SuccessfulRaids1 &&
            stance.SuccessfulRaids2 == snapshot.SuccessfulRaids2 &&
            stance.SuccessfulTownSieges1 == snapshot.SuccessfulTownSieges1 &&
            stance.SuccessfulTownSieges2 == snapshot.SuccessfulTownSieges2 &&
            stance.TotalTributePaidFrom1To2 == snapshot.TotalTributePaidFrom1To2 &&
            stance._dailyTributeFrom1To2 == snapshot.DailyTributeFrom1To2 &&
            stance.DailyTributeInstallments == snapshot.DailyTributeInstallments &&
            (!(snapshot.First is Kingdom firstKingdom) ||
             firstKingdom.PoliticalStagnation == snapshot.FirstPoliticalStagnation) &&
            (!(snapshot.Second is Kingdom secondKingdom) ||
             secondKingdom.PoliticalStagnation == snapshot.SecondPoliticalStagnation);
    }

    private static string LiveTestJson(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private sealed class RaidLootWarningFixture
    {
        public string Token { get; }
        public Campaign Campaign { get; }
        public MobileParty PlayerParty { get; }
        public LiteNetLib.NetPeer Peer { get; }
        public Settlement Settlement { get; }
        public Settlement OriginalSettlement { get; }
        public CampaignVec2 OriginalPosition { get; }
        public Village.VillageStates OriginalVillageState { get; }
        public float OriginalSettlementHitPoints { get; }
        public PartySnapshot PlayerPartySnapshot { get; }
        public PartySnapshot SettlementPartySnapshot { get; }
        public HeroSnapshot[] Heroes { get; }
        public ClanSnapshot[] Clans { get; }
        public FactionStateSnapshot FactionState { get; }
        public bool Positioned { get; set; }
        public bool Prepared { get; set; }
        public bool Restored { get; set; }

        public RaidLootWarningFixture(
            string token,
            Campaign campaign,
            MobileParty playerParty,
            LiteNetLib.NetPeer peer,
            Settlement settlement,
            Settlement originalSettlement,
            CampaignVec2 originalPosition,
            Village.VillageStates originalVillageState,
            float originalSettlementHitPoints,
            PartySnapshot playerPartySnapshot,
            PartySnapshot settlementPartySnapshot,
            HeroSnapshot[] heroes,
            ClanSnapshot[] clans,
            FactionStateSnapshot factionState)
        {
            Token = token;
            Campaign = campaign;
            PlayerParty = playerParty;
            Peer = peer;
            Settlement = settlement;
            OriginalSettlement = originalSettlement;
            OriginalPosition = originalPosition;
            OriginalVillageState = originalVillageState;
            OriginalSettlementHitPoints = originalSettlementHitPoints;
            PlayerPartySnapshot = playerPartySnapshot;
            SettlementPartySnapshot = settlementPartySnapshot;
            Heroes = heroes;
            Clans = clans;
            FactionState = factionState;
        }
    }

    private sealed class PartySnapshot
    {
        public PartyBase Party { get; }
        public TroopRosterElement[] MemberRoster { get; }
        public TroopRosterElement[] PrisonRoster { get; }
        public ItemRosterElement[] Items { get; }
        public float RecentEventsMorale { get; }
        public int PartyTradeGold { get; }
        public Hero LeaderHero { get; }

        public PartySnapshot(
            PartyBase party,
            TroopRosterElement[] memberRoster,
            TroopRosterElement[] prisonRoster,
            ItemRosterElement[] items,
            float recentEventsMorale,
            int partyTradeGold,
            Hero leaderHero)
        {
            Party = party;
            MemberRoster = memberRoster;
            PrisonRoster = prisonRoster;
            Items = items;
            RecentEventsMorale = recentEventsMorale;
            PartyTradeGold = partyTradeGold;
            LeaderHero = leaderHero;
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
        public int Level { get; }
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
            int level,
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
            Level = level;
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

    private sealed class FactionStateSnapshot
    {
        public IFaction First { get; }
        public IFaction Second { get; }
        public bool WasAtWar { get; }
        public StanceLink Stance { get; }
        public StanceType StanceType { get; }
        public int BehaviorPriority { get; }
        public CampaignTime WarStartDate { get; }
        public CampaignTime PeaceDeclarationDate { get; }
        public int TroopCasualties1 { get; }
        public int TroopCasualties2 { get; }
        public int ShipCasualties1 { get; }
        public int ShipCasualties2 { get; }
        public int SuccessfulSieges1 { get; }
        public int SuccessfulSieges2 { get; }
        public int SuccessfulRaids1 { get; }
        public int SuccessfulRaids2 { get; }
        public int SuccessfulTownSieges1 { get; }
        public int SuccessfulTownSieges2 { get; }
        public int TotalTributePaidFrom1To2 { get; }
        public int DailyTributeFrom1To2 { get; }
        public int DailyTributeInstallments { get; }
        public int? FirstPoliticalStagnation { get; }
        public int? SecondPoliticalStagnation { get; }

        public FactionStateSnapshot(
            IFaction first,
            IFaction second,
            bool wasAtWar,
            StanceLink stance,
            StanceType stanceType,
            int behaviorPriority,
            CampaignTime warStartDate,
            CampaignTime peaceDeclarationDate,
            int troopCasualties1,
            int troopCasualties2,
            int shipCasualties1,
            int shipCasualties2,
            int successfulSieges1,
            int successfulSieges2,
            int successfulRaids1,
            int successfulRaids2,
            int successfulTownSieges1,
            int successfulTownSieges2,
            int totalTributePaidFrom1To2,
            int dailyTributeFrom1To2,
            int dailyTributeInstallments,
            int? firstPoliticalStagnation,
            int? secondPoliticalStagnation)
        {
            First = first;
            Second = second;
            WasAtWar = wasAtWar;
            Stance = stance;
            StanceType = stanceType;
            BehaviorPriority = behaviorPriority;
            WarStartDate = warStartDate;
            PeaceDeclarationDate = peaceDeclarationDate;
            TroopCasualties1 = troopCasualties1;
            TroopCasualties2 = troopCasualties2;
            ShipCasualties1 = shipCasualties1;
            ShipCasualties2 = shipCasualties2;
            SuccessfulSieges1 = successfulSieges1;
            SuccessfulSieges2 = successfulSieges2;
            SuccessfulRaids1 = successfulRaids1;
            SuccessfulRaids2 = successfulRaids2;
            SuccessfulTownSieges1 = successfulTownSieges1;
            SuccessfulTownSieges2 = successfulTownSieges2;
            TotalTributePaidFrom1To2 = totalTributePaidFrom1To2;
            DailyTributeFrom1To2 = dailyTributeFrom1To2;
            DailyTributeInstallments = dailyTributeInstallments;
            FirstPoliticalStagnation = firstPoliticalStagnation;
            SecondPoliticalStagnation = secondPoliticalStagnation;
        }
    }
}

[HarmonyPatch(typeof(Hero), nameof(Hero.CanDie))]
internal static class RaidLootWarningFixtureHeroDeathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Hero __instance,
        KillCharacterAction.KillCharacterActionDetail causeOfDeath,
        ref bool __result)
    {
        if (causeOfDeath != KillCharacterAction.KillCharacterActionDetail.DiedInBattle ||
            !RaidDebugCommands.IsFixtureHero(__instance))
            return true;

        __result = false;
        return false;
    }
}
