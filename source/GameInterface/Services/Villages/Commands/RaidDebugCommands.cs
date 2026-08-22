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
using Newtonsoft.Json;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
        if (raidLootWarningFixture != null) return "A raid loot-warning fixture is already pending restoration.";

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
        if (playerParty.MapEvent != null || settlement.Party?.MapEvent != null)
            return "The player party and village must be outside a map event.";
        if (playerParty.PartyMoveMode != MoveModeType.Hold)
            return "The player party must be holding before the fixture is captured.";

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
            AreFactionsAtWar(playerParty.MapFaction, settlement.MapFaction));

        return LiveTestJson(token);
    }

    [CommandLineArgumentFunction("raid_loot_warning_prepare", "coop.debug.mapevent")]
    public static string PrepareRaidLootWarningFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mapevent.raid_loot_warning_prepare <snapshotToken>";
        if (ModInformation.IsClient) return "Run this command on the server.";
        if (args.Count != 1) return usage;
        if (!TryGetRaidLootWarningFixture(args[0], out var fixture, out var error)) return error;
        if (fixture.Prepared) return "The raid loot-warning fixture is already prepared.";

        if (!TryResolveRaidFixtureServices(
                out var objectManager,
                out _,
                out var settlementInterface,
                out var hostileActionInterface,
                out var network))
            return "Unable to resolve raid loot-warning fixture services.";

        var playerParty = fixture.PlayerParty;
        var settlement = fixture.Settlement;
        if (playerParty.MapEvent != null || settlement.Party?.MapEvent != null)
            return "The player party and village entered a map event after capture.";

        try
        {
            if (playerParty.CurrentSettlement != settlement)
            {
                settlementInterface.PartyLeaveSettlement(playerParty);
                playerParty.Position = settlement.GatePosition;
                HoldAndPublishPosition(playerParty);
                settlementInterface.PartyEnterSettlement(playerParty, settlement);
            }

            settlement.Village.VillageState = Village.VillageStates.Normal;
            settlement.SettlementHitPoints = 1f;

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

            fixture.Settlement.Village.VillageState = fixture.OriginalVillageState;
            fixture.Settlement.SettlementHitPoints = fixture.OriginalSettlementHitPoints;

            if (fixture.OriginalSettlement != null)
            {
                if (fixture.PlayerParty.CurrentSettlement != fixture.OriginalSettlement)
                {
                    fixture.PlayerParty.Position = fixture.OriginalSettlement.GatePosition;
                    HoldAndPublishPosition(fixture.PlayerParty);
                    settlementInterface.PartyEnterSettlement(fixture.PlayerParty, fixture.OriginalSettlement);
                }
            }
            else
            {
                fixture.PlayerParty.Position = fixture.OriginalPosition;
                HoldAndPublishPosition(fixture.PlayerParty);
            }

            if (!fixture.WasAtWar && AreFactionsAtWar(
                    fixture.PlayerParty.MapFaction,
                    fixture.Settlement.MapFaction))
                MakePeaceAction.Apply(fixture.PlayerParty.MapFaction, fixture.Settlement.MapFaction);

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
            AreFactionsAtWar(fixture.PlayerParty.MapFaction, fixture.Settlement.MapFaction) == fixture.WasAtWar &&
            (fixture.OriginalSettlement != null || fixture.PlayerParty.Position == fixture.OriginalPosition);

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
        public bool WasAtWar { get; }
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
            bool wasAtWar)
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
            WasAtWar = wasAtWar;
        }
    }
}
