using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

internal class SiegeEntryDebugCommand
{
    private sealed class FixtureTimeGuard
    {
        public bool CanUnpause() => activeFixture == null;
    }

    private static SiegeEntryFixture activeFixture;
    private static SiegeEntryFixture lastRestoredFixture;
    private static string lastDisplayedInformationMessage;
    private static readonly FixtureTimeGuard TimeGuard = new FixtureTimeGuard();
    private static readonly Func<bool> TimeUnpausePolicy = TimeGuard.CanUnpause;

    private static void CaptureInformationMessage(InformationMessage message) =>
        lastDisplayedInformationMessage = message?.Information;

    private static void ResetInformationMessageProbe()
    {
        lastDisplayedInformationMessage = null;
        InformationManager.DisplayMessageInternal -= CaptureInformationMessage;
        InformationManager.DisplayMessageInternal += CaptureInformationMessage;
    }

    [CommandLineArgumentFunction("entry_fixture_start", "coop.debug.siege")]
    public static string StartFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.siege.entry_fixture_start <controllerId> <settlementId>";

        if (activeFixture != null)
            return $"Siege-entry fixture {activeFixture.Token} is already active.";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface) ||
            !ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
        {
            return "Unable to resolve siege-entry fixture services.";
        }

        if (!playerManager.TryGetPlayer(args[0], out var player) ||
            !playerManager.IsConnected(player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return $"Unable to resolve connected player {args[0]}.";
        }

        if (!objectManager.TryGetObject<Settlement>(args[1], out var settlement))
            return $"Settlement with id {args[1]} not found.";

        if (!settlement.IsFortification ||
            settlement.MapFaction == null ||
            settlement.Town == null ||
            settlement.OwnerClan?.Leader == null)
            return $"{settlement.Name} must be an owned fortification.";

        if (!IsClean(playerParty, allowDisorganized: true))
        {
            return $"{playerParty.Name} must be active, organized, unattached, and outside settlements, sieges, armies, and map events: " +
                $"active={playerParty.IsActive}|disorganized={playerParty.IsDisorganized}|" +
                $"attached={playerParty.AttachedTo?.StringId ?? "none"}|" +
                $"army={playerParty.Army?.LeaderParty?.StringId ?? "none"}|" +
                $"mapEvent={(playerParty.MapEvent == null ? "none" : playerParty.MapEvent.EventType.ToString())}|" +
                $"settlement={playerParty.CurrentSettlement?.StringId ?? "none"}|" +
                $"besiegerCamp={playerParty.BesiegerCamp?.SiegeEvent?.BesiegedSettlement?.StringId ?? "none"}";
        }

        bool playerWasDisorganized = playerParty.IsDisorganized;

        if (settlement.SiegeEvent != null)
            return $"{settlement.Name} is already under siege.";

        if (playerParty.MapFaction == null ||
            playerParty.MapFaction == settlement.MapFaction ||
            playerParty.ActualClan == null ||
            playerParty.ActualClan == settlement.OwnerClan)
        {
            return $"{playerParty.Name} must belong to a different clan and faction than {settlement.Name}.";
        }

        var aiBesieger = MobileParty.AllLordParties
            .Where(party => IsClean(party) &&
                !party.IsPlayerParty() &&
                party.LeaderHero != null &&
                party.MemberRoster.TotalHealthyCount > 0 &&
                party.MapFaction != null &&
                party.MapFaction != settlement.MapFaction &&
                party.MapFaction != playerParty.MapFaction)
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .FirstOrDefault();

        if (aiBesieger == null)
            return $"No clean third-faction AI lord party is available to besiege {settlement.Name}.";

        if (!behaviorSnapshot.TryCreate(playerParty, out var playerBehavior) ||
            !behaviorSnapshot.TryCreate(aiBesieger, out var aiBehavior))
        {
            return "Unable to snapshot the fixture parties' movement state.";
        }

        if (!objectManager.TryGetId(settlement, out string settlementId) ||
            !objectManager.TryGetId(settlement.OwnerClan, out string originalOwnerClanId))
        {
            return "Unable to resolve the fixture settlement and owner ids.";
        }

        var playerSettlementStance = StanceLinkSnapshot.Capture(
            playerParty.MapFaction,
            settlement.MapFaction);
        var besiegerSettlementStance = StanceLinkSnapshot.Capture(
            aiBesieger.MapFaction,
            settlement.MapFaction);
        var besiegerPlayerStance = StanceLinkSnapshot.Capture(
            aiBesieger.MapFaction,
            playerParty.MapFaction);
        var settlementThreatSnapshots = Settlement.All
            .Where(candidate =>
                candidate == settlement ||
                ((candidate.IsFortification || candidate.IsVillage) &&
                 candidate.LastAttackerParty == aiBesieger))
            .OrderBy(candidate => candidate.StringId)
            .Select(SettlementThreatSnapshot.Capture)
            .ToArray();

        var fixture = new SiegeEntryFixture(
            Guid.NewGuid().ToString("N"),
            player.ControllerId,
            playerParty,
            playerBehavior,
            aiBesieger,
            aiBehavior,
            settlement,
            settlementId,
            settlement.OwnerClan,
            originalOwnerClanId,
            playerParty.MapFaction,
            aiBesieger.MapFaction,
            settlement.MapFaction,
            playerSettlementStance,
            besiegerSettlementStance,
            besiegerPlayerStance,
            settlementThreatSnapshots,
            timeControl.GetTimeControl(),
            playerWasDisorganized,
            CharacterRelationManager.GetHeroRelation(
                settlement.OwnerClan.Leader,
                aiBesieger.LeaderHero));

        try
        {
            activeFixture = fixture;
            playerParty.SetDisorganized(false);
            if (playerParty.IsDisorganized)
                throw new InvalidOperationException($"Unable to organize {playerParty.Name} for the fixture.");

            timeControl.AddUnpausePolicy(TimeUnpausePolicy);
            fixture.TimePolicyAdded = true;
            timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
            if (timeControl.GetTimeControl() != TimeControlEnum.Pause)
                throw new InvalidOperationException("Unable to pause authoritative campaign time.");

            if (!fixture.PlayerWasAtWar)
                DeclareWarAction.ApplyByDefault(fixture.PlayerFaction, fixture.SettlementFaction);
            if (!fixture.BesiegerWasAtWar)
                DeclareWarAction.ApplyByDefault(fixture.BesiegerFaction, fixture.SettlementFaction);

            aiBesieger.Position = settlement.GatePosition;
            aiBesieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            siegeEventInterface.StartSiegeEvent(aiBesieger, settlement);
            if (settlement.SiegeEvent?.BesiegerCamp?.LeaderParty != aiBesieger)
                throw new InvalidOperationException($"Failed to start the AI siege of {settlement.Name}.");

            StageAtSettlement(playerParty, settlement);

            return $"token={fixture.Token}|controller={fixture.ControllerId}|" +
                $"playerParty={fixture.PlayerBehavior.MobilePartyId}|" +
                $"aiBesieger={fixture.AiBehavior.MobilePartyId}|settlement={fixture.SettlementId}|" +
                $"playerWarAdded={!fixture.PlayerWasAtWar}|besiegerWarAdded={!fixture.BesiegerWasAtWar}|" +
                $"timeMode={timeControl.GetTimeControl()}|originalTimeMode={fixture.OriginalTimeControl}";
        }
        catch (Exception exception)
        {
            if (TryRestoreFixture(
                    fixture,
                    behaviorSnapshot,
                    siegeEventInterface,
                    timeControl,
                    out var restoreError))
            {
                activeFixture = null;
                return $"Fixture setup failed: {exception.Message}. Restore result: restored.";
            }

            return $"setupFailed=true|restoreFailed=true|token={fixture.Token}|" +
                $"controller={fixture.ControllerId}|playerParty={fixture.PlayerBehavior.MobilePartyId}|" +
                $"aiBesieger={fixture.AiBehavior.MobilePartyId}|settlement={fixture.SettlementId}|" +
                $"timeMode={timeControl.GetTimeControl()}|originalTimeMode={fixture.OriginalTimeControl}|" +
                $"setupError={SanitizeStateValue(exception.Message)}|" +
                $"restoreError={SanitizeStateValue(restoreError)}";
        }
    }

    [CommandLineArgumentFunction("entry_fixture_baseline", "coop.debug.siege")]
    public static string FixtureBaseline(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_fixture_baseline <token>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        return FormatFixtureBaseline(fixture);
    }

    [CommandLineArgumentFunction("entry_fixture_retarget", "coop.debug.siege")]
    public static string RetargetFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.siege.entry_fixture_retarget <token> <settlementId>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        if (fixture.OwnerOverridden)
            return "Restore the owner override before retargeting the fixture.";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(args[1], out var settlement))
        {
            return $"Settlement with id {args[1]} not found.";
        }

        StageOutsideSettlement(fixture.PlayerParty, settlement);
        fixture.PhysicalSettlementId = settlement.StringId;

        return $"token={fixture.Token}|playerParty={fixture.PlayerParty.StringId}|" +
            $"targetSettlement={fixture.PlayerParty.TargetSettlement?.StringId ?? "none"}|" +
            $"physicalSettlement={settlement.StringId}|" +
            $"requestedSettlement={fixture.Settlement.StringId}";
    }

    [CommandLineArgumentFunction("entry_fixture_override_owner", "coop.debug.siege")]
    public static string OverrideFixtureOwner(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_fixture_override_owner <token>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        if (fixture.OwnerOverridden)
            return $"Fixture {fixture.Token} already has the owner override.";

        if (fixture.PlayerParty.ActualClan == null)
            return $"{fixture.PlayerParty.Name} has no clan.";

        // Change only the authoritative backing field so the temporary stale-client fixture does not alter fief caches.
        fixture.Settlement.Town._ownerClan = fixture.PlayerParty.ActualClan;
        fixture.OwnerOverridden = true;

        return $"token={fixture.Token}|settlement={fixture.Settlement.StringId}|" +
            $"ownerClan={fixture.PlayerParty.ActualClan.StringId}|" +
            "ownerSiegeWarAdded=False";
    }

    [CommandLineArgumentFunction("entry_fixture_force_stale_reconnect", "coop.debug.siege")]
    public static string ForceStaleReconnectFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.siege.entry_fixture_force_stale_reconnect <token> <settlementId>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(args[1], out var settlement))
        {
            return $"Settlement with id {args[1]} not found.";
        }

        if (fixture.PlayerParty.BesiegerCamp != fixture.Settlement.SiegeEvent?.BesiegerCamp)
            return $"{fixture.PlayerParty.Name} must first join the siege of {fixture.Settlement.Name}.";

        fixture.PlayerParty.Position = settlement.GatePosition;
        MessageBroker.Instance.Publish(
            typeof(SiegeEntryDebugCommand),
            new PartyBehaviorChangeAttempted(
                fixture.PlayerParty,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: false));
        fixture.StaleReconnectForced = true;
        fixture.PhysicalSettlementId = settlement.StringId;

        return $"token={fixture.Token}|party={fixture.PlayerParty.StringId}|" +
            $"besiegerSettlement={fixture.Settlement.StringId}|physicalSettlement={settlement.StringId}|" +
            "staleReconnect=true";
    }

    [CommandLineArgumentFunction("entry_fixture_state", "coop.debug.siege")]
    public static string FixtureState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_fixture_state <token>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        var party = fixture.PlayerParty;
        var settlement = fixture.Settlement;
        return $"token={fixture.Token}|party={party.StringId}|position={party.Position.X:R},{party.Position.Y:R}|" +
            $"physicalSettlement={fixture.PhysicalSettlementId}|" +
            $"targetSettlement={party.TargetSettlement?.StringId ?? "none"}|" +
            $"currentSettlement={party.CurrentSettlement?.StringId ?? "none"}|" +
            $"besiegerCamp={(party.BesiegerCamp == null ? "none" : settlement.StringId)}|" +
            $"mapEvent={(party.MapEvent == null ? "none" : party.MapEvent.EventType.ToString())}|" +
            $"siegeActive={settlement.SiegeEvent != null}|" +
            $"ownerMatchesPlayer={settlement.OwnerClan == party.ActualClan}|" +
            $"ownerOverridden={fixture.OwnerOverridden}|" +
            $"staleReconnectForced={fixture.StaleReconnectForced}";
    }

    [CommandLineArgumentFunction("entry_fixture_restore_owner", "coop.debug.siege")]
    public static string RestoreFixtureOwner(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_fixture_restore_owner <token>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        if (!fixture.OwnerOverridden)
            return $"Fixture {fixture.Token} has no owner override.";

        fixture.Settlement.Town._ownerClan = fixture.OriginalOwnerClan;
        fixture.OwnerOverridden = false;
        return $"Restored {fixture.Settlement.StringId} owner to {fixture.OriginalOwnerClan?.StringId ?? "none"}.";
    }

    [CommandLineArgumentFunction("entry_fixture_restore", "coop.debug.siege")]
    public static string RestoreFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_fixture_restore <token>";

        if (!TryGetFixture(args[0], out var fixture, out var error))
            return error;

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface) ||
            !ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
        {
            return "Unable to resolve siege-entry fixture restore services.";
        }

        if (!TryRestoreFixture(
                fixture,
                behaviorSnapshot,
                siegeEventInterface,
                timeControl,
                out var restoreError))
            return $"Fixture restore failed: {restoreError}. Retry the restore command.";

        lastRestoredFixture = fixture;
        activeFixture = null;
        return $"Restored siege-entry fixture {fixture.Token}; player={fixture.PlayerBehavior.MobilePartyId} " +
            $"aiBesieger={fixture.AiBehavior.MobilePartyId} settlement={fixture.SettlementId}.";
    }

    [CommandLineArgumentFunction("entry_fixture_restored_state", "coop.debug.siege")]
    public static string RestoredFixtureState(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_fixture_restored_state <token>";

        if (activeFixture != null)
            return $"Siege-entry fixture {activeFixture.Token} is still active.";

        if (lastRestoredFixture == null ||
            !string.Equals(lastRestoredFixture.Token, args[0], StringComparison.Ordinal))
        {
            return $"No restored siege-entry fixture receipt exists for {args[0]}.";
        }

        return TryFormatRestorationState(
            lastRestoredFixture.PlayerParty,
            lastRestoredFixture.AiBesieger,
            lastRestoredFixture.Settlement,
            lastRestoredFixture.OriginalOwnerClan,
            lastRestoredFixture,
            out var state,
            out var error)
            ? state
            : error;
    }

    [CommandLineArgumentFunction("open_settlement_encounter", "coop.debug.siege")]
    public static string OpenSettlementEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.open_settlement_encounter <settlementId>";

        ResetInformationMessageProbe();

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found.";
        }

        EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
        return $"Opened the settlement encounter at {settlement.Name}.";
    }

    [CommandLineArgumentFunction("entry_information_message_state", "coop.debug.siege")]
    public static string InformationMessageState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count == 0)
            return "Usage: coop.debug.siege.entry_information_message_state <expectedText>";

        string expectedText = string.Join(" ", args);
        return $"matched={string.Equals(lastDisplayedInformationMessage, expectedText, StringComparison.Ordinal)}|" +
            $"message={SanitizeStateValue(lastDisplayedInformationMessage)}";
    }

    [CommandLineArgumentFunction("entry_join_option_state", "coop.debug.siege")]
    public static string JoinOptionState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.siege.entry_join_option_state <settlementId>";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found.";
        }

        string ownerClanId = "none";
        if (settlement.OwnerClan != null &&
            !objectManager.TryGetId(settlement.OwnerClan, out ownerClanId))
        {
            return "Unable to resolve the local settlement owner id.";
        }
        string playerClanId = "none";
        if (MobileParty.MainParty?.ActualClan != null &&
            !objectManager.TryGetId(MobileParty.MainParty.ActualClan, out playerClanId))
        {
            return "Unable to resolve the local player clan id.";
        }

        var menuContext = Campaign.Current?.CurrentMenuContext;
        var menu = menuContext?.GameMenu;
        var options = menu?.MenuOptions.ToList();
        int optionIndex = options?.FindIndex(
            menuOption => menuOption.IdString == "join_siege_event") ?? -1;
        var option = optionIndex >= 0 ? options[optionIndex] : null;
        bool optionRendered = option != null &&
            menu.GetMenuOptionConditionsHold(Game.Current, menuContext, optionIndex);
        return $"settlement={settlement.StringId}|ownerClan={ownerClanId}|" +
            $"playerClan={playerClanId}|" +
            $"ownerMatchesPlayer={settlement.OwnerClan == MobileParty.MainParty?.ActualClan}|" +
            $"menu={menu?.StringId ?? "none"}|optionRegistered={option != null}|" +
            $"optionRendered={optionRendered}|" +
            $"optionEnabled={optionRendered && option.IsEnabled}";
    }

    [CommandLineArgumentFunction("choose_rendered_join", "coop.debug.siege")]
    public static string ChooseRenderedJoin(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.siege.choose_rendered_join";

        var menuContext = Campaign.Current?.CurrentMenuContext;
        var menu = menuContext?.GameMenu;
        if (menu?.StringId != "join_siege_event")
            return $"The rendered join-siege menu is not active; current menu is {menu?.StringId ?? "none"}.";

        var options = menu.MenuOptions.ToList();
        int optionIndex = options.FindIndex(
            menuOption => menuOption.IdString == "join_siege_event");
        if (optionIndex < 0)
            return "The rendered join-siege option is not registered.";
        var option = options[optionIndex];
        if (!menu.GetMenuOptionConditionsHold(Game.Current, menuContext, optionIndex))
            return "The registered join-siege option is not rendered.";
        if (!option.IsEnabled)
            return "The rendered join-siege option is disabled.";

        string settlementId = Settlement.CurrentSettlement?.StringId ?? "none";
        menu.RunMenuOptionConsequence(menuContext, optionIndex);
        return $"menu=join_siege_event|option=join_siege_event|settlement={settlementId}|" +
            "rendered=True|enabled=True|consequenceInvoked=true";
    }

    [CommandLineArgumentFunction("choose_rendered_leave", "coop.debug.siege")]
    public static string ChooseRenderedLeave(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.siege.choose_rendered_leave";

        var menuContext = Campaign.Current?.CurrentMenuContext;
        var menu = menuContext?.GameMenu;
        if (menu?.StringId != "join_siege_event")
            return $"The rendered join-siege menu is not active; current menu is {menu?.StringId ?? "none"}.";

        var options = menu.MenuOptions.ToList();
        int leaveOptionIndex = -1;
        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            var option = options[optionIndex];
            if (!menu.GetMenuOptionConditionsHold(Game.Current, menuContext, optionIndex) ||
                (!option.IsLeave &&
                 option.OptionLeaveType != TaleWorlds.CampaignSystem.GameMenus.GameMenuOption.LeaveType.Leave))
            {
                continue;
            }

            if (leaveOptionIndex >= 0)
                return "The rendered join-siege menu has more than one Leave option.";

            leaveOptionIndex = optionIndex;
        }

        if (leaveOptionIndex < 0)
            return "The rendered join-siege menu has no Leave option.";

        var leaveOption = options[leaveOptionIndex];
        if (!leaveOption.IsEnabled)
        {
            return $"menu=join_siege_event|option={leaveOption.IdString}|" +
                $"leaveType={leaveOption.OptionLeaveType}|rendered=True|enabled=False|consequenceInvoked=false";
        }

        menu.RunMenuOptionConsequence(menuContext, leaveOptionIndex);
        return $"menu=join_siege_event|option={leaveOption.IdString}|" +
            $"leaveType={leaveOption.OptionLeaveType}|rendered=True|enabled=True|consequenceInvoked=true";
    }

    [CommandLineArgumentFunction("entry_restoration_state", "coop.debug.siege")]
    public static string RestorationState(List<string> args)
    {
        if (args.Count != 4)
        {
            return "Usage: coop.debug.siege.entry_restoration_state " +
                "<playerPartyId> <aiPartyId> <settlementId> <originalOwnerClanId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(args[0], out var playerParty) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(args[1], out var aiParty) ||
            !objectManager.TryGetObjectWithLogging<Settlement>(args[2], out var settlement) ||
            !objectManager.TryGetObjectWithLogging<Clan>(args[3], out var originalOwnerClan))
        {
            return "Unable to resolve the siege-entry restoration objects.";
        }

        return TryFormatRestorationState(
            playerParty,
            aiParty,
            settlement,
            originalOwnerClan,
            null,
            out var state,
            out var error)
            ? state
            : error;
    }

    private static bool IsClean(MobileParty party, bool allowDisorganized = false) =>
        party?.IsActive == true &&
        (allowDisorganized || !party.IsDisorganized) &&
        party.AttachedTo == null &&
        party.Army == null &&
        party.MapEvent == null &&
        party.CurrentSettlement == null &&
        party.BesiegerCamp == null;

    private static void StageAtSettlement(MobileParty party, Settlement settlement)
    {
        party.Position = settlement.GatePosition;
        party.SetMoveGoToSettlement(
            settlement,
            MobileParty.NavigationType.Default,
            isTargetingThePort: false);
        MessageBroker.Instance.Publish(
            typeof(SiegeEntryDebugCommand),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: false));
    }

    private static void StageOutsideSettlement(MobileParty party, Settlement settlement)
    {
        if (party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(party);

        party.Position = settlement.GatePosition;
        party.SetMoveModeHold();
        MessageBroker.Instance.Publish(
            typeof(SiegeEntryDebugCommand),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: false));
    }

    private static string FormatFixtureBaseline(SiegeEntryFixture fixture) =>
        $"token={fixture.Token}|controller={fixture.ControllerId}|" +
        $"playerParty={fixture.PlayerBehavior.MobilePartyId}|aiBesieger={fixture.AiBehavior.MobilePartyId}|" +
        $"settlement={fixture.SettlementId}|ownerClan={fixture.OriginalOwnerClanId}|" +
        $"ownerMatchesExpected=True|playerWar={fixture.PlayerWasAtWar}|" +
        $"aiWar={fixture.BesiegerWasAtWar}|ownerSiegeWar={fixture.BesiegerPlayerWasAtWar}|" +
        $"ownerRelation={fixture.OriginalOwnerLeaderRelation}|" +
        $"timeMode={fixture.OriginalTimeControl}|" +
        FormatPartyState(
            "player",
            fixture.PlayerBehavior,
            null,
            fixture.PlayerWasDisorganized) + "|" +
        FormatPartyState("ai", fixture.AiBehavior, null) + "|" +
        "siegeActive=False|fixtureActive=False|" +
        $"playerDiplomacy={fixture.PlayerSettlementStance.OriginalFingerprint}|" +
        $"aiDiplomacy={fixture.BesiegerSettlementStance.OriginalFingerprint}|" +
        $"ownerSiegeDiplomacy={fixture.BesiegerPlayerStance.OriginalFingerprint}|" +
        $"settlementThreat={SettlementThreatSnapshot.GetOriginalFingerprint(fixture.SettlementThreatSnapshots)}";

    private static bool TryFormatRestorationState(
        MobileParty playerParty,
        MobileParty aiParty,
        Settlement settlement,
        Clan originalOwnerClan,
        SiegeEntryFixture fixture,
        out string state,
        out string error)
    {
        state = null;
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
        {
            error = "Unable to resolve the restoration-state services.";
            return false;
        }

        if (!behaviorSnapshot.TryCreate(playerParty, out var playerBehavior) ||
            !behaviorSnapshot.TryCreate(aiParty, out var aiBehavior))
        {
            error = "Unable to snapshot the fixture parties' current movement state.";
            return false;
        }

        if (playerParty.MapFaction == null ||
            aiParty.MapFaction == null ||
            settlement.MapFaction == null ||
            originalOwnerClan?.Leader == null ||
            aiParty.LeaderHero == null)
        {
            error = "Unable to resolve the fixture parties' diplomacy state.";
            return false;
        }

        if (!objectManager.TryGetId(settlement, out string settlementId))
        {
            error = "Unable to resolve the restored settlement id.";
            return false;
        }
        string ownerClanId = "none";
        if (settlement.OwnerClan != null &&
            !objectManager.TryGetId(settlement.OwnerClan, out ownerClanId))
        {
            error = "Unable to resolve the restored owner clan id.";
            return false;
        }

        string tokenState = fixture == null ? string.Empty : $"token={fixture.Token}|";
        string authoritativeState = fixture == null
            ? string.Empty
            : $"|playerDiplomacy={StanceLinkSnapshot.GetFingerprint(fixture.PlayerFaction, fixture.SettlementFaction)}" +
              $"|aiDiplomacy={StanceLinkSnapshot.GetFingerprint(fixture.BesiegerFaction, fixture.SettlementFaction)}" +
              $"|ownerSiegeDiplomacy={StanceLinkSnapshot.GetFingerprint(fixture.BesiegerFaction, fixture.PlayerFaction)}" +
              $"|settlementThreat={SettlementThreatSnapshot.GetFingerprint(fixture.SettlementThreatSnapshots)}";
        state = tokenState +
            $"playerParty={playerBehavior.MobilePartyId}|aiBesieger={aiBehavior.MobilePartyId}|" +
            $"settlement={settlementId}|ownerClan={ownerClanId}|" +
            $"ownerMatchesExpected={settlement.OwnerClan == originalOwnerClan}|" +
            $"playerWar={playerParty.MapFaction.IsAtWarWith(settlement.MapFaction)}|" +
            $"aiWar={aiParty.MapFaction.IsAtWarWith(settlement.MapFaction)}|" +
            $"ownerSiegeWar={aiParty.MapFaction.IsAtWarWith(playerParty.MapFaction)}|" +
            $"ownerRelation={CharacterRelationManager.GetHeroRelation(originalOwnerClan.Leader, aiParty.LeaderHero)}|" +
            $"timeMode={timeControl.GetTimeControl()}|" +
            FormatPartyState("player", playerBehavior, playerParty) + "|" +
            FormatPartyState("ai", aiBehavior, aiParty) + "|" +
            $"siegeActive={settlement.SiegeEvent != null}|fixtureActive={activeFixture != null}" +
            authoritativeState;
        error = null;
        return true;
    }

    private static string FormatPartyState(
        string prefix,
        PartyBehaviorUpdateData behavior,
        MobileParty party,
        bool? recordedDisorganized = null)
    {
        string besiegerCamp = party?.BesiegerCamp?.SiegeEvent?.BesiegedSettlement?.StringId ?? "none";
        string mapEvent = party?.MapEvent?.EventType.ToString() ?? "none";
        string currentSettlement = party?.CurrentSettlement?.StringId ?? "none";
        bool isDisorganized = recordedDisorganized ?? party?.IsDisorganized ?? false;
        return $"{prefix}Position={FormatPosition(behavior.PartyPosition)}|" +
            $"{prefix}AtSea={behavior.IsCurrentlyAtSea}|" +
            $"{prefix}Behavior={FormatBehavior(behavior)}|" +
            $"{prefix}BesiegerCamp={besiegerCamp}|{prefix}MapEvent={mapEvent}|" +
            $"{prefix}CurrentSettlement={currentSettlement}|{prefix}Disorganized={isDisorganized}";
    }

    private static string FormatBehavior(PartyBehaviorUpdateData behavior) =>
        $"short:{behavior.NewAiBehavior};interactable:{behavior.InteractablePointId ?? "none"};" +
        $"best:{FormatPosition(behavior.BestTargetPoint)};default:{behavior.DefaultBehavior};" +
        $"target:{FormatPosition(behavior.TargetPosition)};navigation:{behavior.DesiredAiNavigationType};" +
        $"targetParty:{behavior.TargetPartyId ?? "none"};" +
        $"targetSettlement:{behavior.TargetSettlementId ?? "none"};" +
        $"movePoint:{FormatPosition(behavior.MoveTargetPoint)};targetingPort:{behavior.IsTargetingPort};" +
        $"moveMode:{behavior.PartyMoveMode};moveParty:{behavior.MoveTargetPartyId ?? "none"};" +
        $"interactableAnchor:{behavior.IsInteractableAnchor}";

    private static string FormatPosition(CampaignVec2 position) =>
        FormattableString.Invariant($"{position.X:R},{position.Y:R}");

    private static string SanitizeStateValue(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

    private static bool TryGetFixture(
        string token,
        out SiegeEntryFixture fixture,
        out string error)
    {
        fixture = activeFixture;
        if (fixture == null)
        {
            error = "No siege-entry fixture is active.";
            return false;
        }

        if (!string.Equals(fixture.Token, token, StringComparison.Ordinal))
        {
            error = $"Active siege-entry fixture token is {fixture.Token}, not {token}.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryRestoreFixture(
        SiegeEntryFixture fixture,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ISiegeEventInterface siegeEventInterface,
        ITimeControlInterface timeControl,
        out string error)
    {
        Exception restoreException = null;
        try
        {
            timeControl.ServerSetTimeControl(TimeControlEnum.Pause);

            if (fixture.OwnerOverridden)
            {
                fixture.Settlement.Town._ownerClan = fixture.OriginalOwnerClan;
                fixture.OwnerOverridden = false;
            }

            if (fixture.PlayerParty.BesiegerCamp != null)
                siegeEventInterface.BreakSiege(fixture.PlayerParty);

            if (fixture.Settlement.SiegeEvent?.BesiegerCamp?.LeaderParty == fixture.AiBesieger)
                siegeEventInterface.BreakSiege(fixture.AiBesieger);

            if (fixture.PlayerParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(fixture.PlayerParty);

            RestoreBehavior(fixture.PlayerParty, fixture.PlayerBehavior, behaviorSnapshot);
            RestoreBehavior(fixture.AiBesieger, fixture.AiBehavior, behaviorSnapshot);
            fixture.PlayerParty.SetDisorganized(fixture.PlayerWasDisorganized);
            fixture.AiBesieger.SetDisorganized(false);
            CharacterRelationManager.SetHeroRelation(
                fixture.OriginalOwnerClan.Leader,
                fixture.AiBesieger.LeaderHero,
                fixture.OriginalOwnerLeaderRelation);

            RestoreWar(fixture.PlayerFaction, fixture.SettlementFaction, fixture.PlayerWasAtWar);
            RestoreWar(fixture.BesiegerFaction, fixture.SettlementFaction, fixture.BesiegerWasAtWar);

            fixture.PlayerSettlementStance.Restore();
            fixture.BesiegerSettlementStance.Restore();
            fixture.BesiegerPlayerStance.Restore();
            foreach (var settlementThreat in fixture.SettlementThreatSnapshots)
                settlementThreat.Restore();
        }
        catch (Exception exception)
        {
            restoreException = exception;
        }

        if (restoreException == null)
        {
            try
            {
                if (fixture.TimePolicyAdded)
                {
                    timeControl.RemoveUnpausePolicy(TimeUnpausePolicy);
                    fixture.TimePolicyAdded = false;
                }
                timeControl.ServerSetTimeControl(fixture.OriginalTimeControl);
                if (timeControl.GetTimeControl() != fixture.OriginalTimeControl)
                    throw new InvalidOperationException("Unable to restore authoritative campaign time.");
            }
            catch (Exception exception)
            {
                restoreException = exception;
                try
                {
                    if (!fixture.TimePolicyAdded)
                    {
                        timeControl.AddUnpausePolicy(TimeUnpausePolicy);
                        fixture.TimePolicyAdded = true;
                    }
                    timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
                }
                catch (Exception guardException)
                {
                    restoreException = new AggregateException(restoreException, guardException);
                }
            }
        }

        error = restoreException?.Message;
        return restoreException == null;
    }

    private static void RestoreBehavior(
        MobileParty party,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        party.Position = behavior.PartyPosition;
        party.IsCurrentlyAtSea = behavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(party, behavior, out _))
            throw new InvalidOperationException($"Unable to restore movement state for {party.StringId}.");

        MessageBroker.Instance.Publish(
            typeof(SiegeEntryDebugCommand),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: behavior.IsCurrentlyAtSea,
                resetMovementToHold: false));
    }

    private static void RestoreWar(IFaction first, IFaction second, bool wasAtWar)
    {
        bool isAtWar = first.IsAtWarWith(second);
        if (isAtWar == wasAtWar)
            return;

        if (wasAtWar)
            DeclareWarAction.ApplyByDefault(first, second);
        else
            MakePeaceAction.Apply(first, second);
    }

    private sealed class StanceLinkSnapshot
    {
        private readonly IFaction faction1;
        private readonly IFaction faction2;
        private readonly StanceType stanceType;
        private readonly int behaviorPriority;
        private readonly CampaignTime warStartDate;
        private readonly CampaignTime peaceDeclarationDate;
        private readonly int troopCasualties1;
        private readonly int troopCasualties2;
        private readonly int shipCasualties1;
        private readonly int shipCasualties2;
        private readonly int successfulSieges1;
        private readonly int successfulSieges2;
        private readonly int successfulRaids1;
        private readonly int successfulRaids2;
        private readonly int totalTributePaidFrom1To2;
        private readonly int dailyTributeFrom1To2;
        private readonly int dailyTributeInstallments;
        private readonly int successfulTownSieges1;
        private readonly int successfulTownSieges2;
        private readonly int? faction1PoliticalStagnation;
        private readonly int? faction2PoliticalStagnation;

        public bool WasAtWar { get; }
        public string OriginalFingerprint { get; }

        private StanceLinkSnapshot(IFaction faction1, IFaction faction2, StanceLink stance)
        {
            this.faction1 = faction1;
            this.faction2 = faction2;
            stanceType = stance._stanceType;
            behaviorPriority = stance.BehaviorPriority;
            warStartDate = stance._warStartDate;
            peaceDeclarationDate = stance._peaceDeclarationDate;
            troopCasualties1 = stance._troopCasualties1;
            troopCasualties2 = stance._troopCasualties2;
            shipCasualties1 = stance.ShipCasualties1;
            shipCasualties2 = stance.ShipCasualties2;
            successfulSieges1 = stance._successfulSieges1;
            successfulSieges2 = stance._successfulSieges2;
            successfulRaids1 = stance._successfulRaids1;
            successfulRaids2 = stance._successfulRaids2;
            totalTributePaidFrom1To2 = stance._totalTributePaidFrom1To2;
            dailyTributeFrom1To2 = stance._dailyTributeFrom1To2;
            dailyTributeInstallments = stance._dailyTributeInstallments;
            successfulTownSieges1 = stance._successfulTownSieges1;
            successfulTownSieges2 = stance._successfulTownSieges2;
            faction1PoliticalStagnation = (faction1 as Kingdom)?.PoliticalStagnation;
            faction2PoliticalStagnation = (faction2 as Kingdom)?.PoliticalStagnation;
            WasAtWar = stance.IsAtWar;
            OriginalFingerprint = GetFingerprint(faction1, faction2);
        }

        public static StanceLinkSnapshot Capture(IFaction faction1, IFaction faction2) =>
            new StanceLinkSnapshot(faction1, faction2, faction1.GetStanceWith(faction2));

        public void Restore()
        {
            if (WasAtWar)
                FactionManager.DeclareWar(faction1, faction2);
            else
                FactionManager.SetNeutral(faction1, faction2);

            var stance = faction1.GetStanceWith(faction2);
            stance._stanceType = stanceType;
            stance.BehaviorPriority = behaviorPriority;
            stance._warStartDate = warStartDate;
            stance._peaceDeclarationDate = peaceDeclarationDate;
            stance._troopCasualties1 = troopCasualties1;
            stance._troopCasualties2 = troopCasualties2;
            stance.ShipCasualties1 = shipCasualties1;
            stance.ShipCasualties2 = shipCasualties2;
            stance._successfulSieges1 = successfulSieges1;
            stance._successfulSieges2 = successfulSieges2;
            stance._successfulRaids1 = successfulRaids1;
            stance._successfulRaids2 = successfulRaids2;
            stance._totalTributePaidFrom1To2 = totalTributePaidFrom1To2;
            stance._dailyTributeFrom1To2 = dailyTributeFrom1To2;
            stance._dailyTributeInstallments = dailyTributeInstallments;
            stance._successfulTownSieges1 = successfulTownSieges1;
            stance._successfulTownSieges2 = successfulTownSieges2;
            if (faction1 is Kingdom kingdom1 && faction1PoliticalStagnation.HasValue)
                kingdom1.PoliticalStagnation = faction1PoliticalStagnation.Value;
            if (faction2 is Kingdom kingdom2 && faction2PoliticalStagnation.HasValue)
                kingdom2.PoliticalStagnation = faction2PoliticalStagnation.Value;

            faction1.UpdateFactionsAtWarWith();
            faction2.UpdateFactionsAtWarWith();
        }

        public static string GetFingerprint(IFaction faction1, IFaction faction2)
        {
            var stance = faction1.GetStanceWith(faction2);
            var state = new StringBuilder()
                .Append(stance._stanceType).Append('|')
                .Append(stance.BehaviorPriority).Append('|')
                .Append(stance._warStartDate).Append('|')
                .Append(stance._peaceDeclarationDate).Append('|')
                .Append(stance._troopCasualties1).Append('|')
                .Append(stance._troopCasualties2).Append('|')
                .Append(stance.ShipCasualties1).Append('|')
                .Append(stance.ShipCasualties2).Append('|')
                .Append(stance._successfulSieges1).Append('|')
                .Append(stance._successfulSieges2).Append('|')
                .Append(stance._successfulRaids1).Append('|')
                .Append(stance._successfulRaids2).Append('|')
                .Append(stance._totalTributePaidFrom1To2).Append('|')
                .Append(stance._dailyTributeFrom1To2).Append('|')
                .Append(stance._dailyTributeInstallments).Append('|')
                .Append(stance._successfulTownSieges1).Append('|')
                .Append(stance._successfulTownSieges2).Append('|')
                .Append((faction1 as Kingdom)?.PoliticalStagnation.ToString(CultureInfo.InvariantCulture) ?? "none").Append('|')
                .Append((faction2 as Kingdom)?.PoliticalStagnation.ToString(CultureInfo.InvariantCulture) ?? "none")
                .ToString();
            return Fingerprint(state);
        }

        private static string Fingerprint(string state)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(state));
            var output = new StringBuilder(hash.Length * 2);
            foreach (var value in hash)
                output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return output.ToString();
        }
    }

    private sealed class SettlementThreatSnapshot
    {
        private readonly Settlement settlement;
        private readonly MobileParty lastAttackerParty;
        private readonly CampaignTime lastThreatTime;

        private SettlementThreatSnapshot(Settlement settlement)
        {
            this.settlement = settlement;
            lastAttackerParty = settlement.LastAttackerParty;
            lastThreatTime = settlement.LastThreatTime;
        }

        public static SettlementThreatSnapshot Capture(Settlement settlement) =>
            new SettlementThreatSnapshot(settlement);

        public void Restore()
        {
            settlement._lastAttackerParty = lastAttackerParty;
            settlement.LastThreatTime = lastThreatTime;
        }

        public static string GetFingerprint(IEnumerable<SettlementThreatSnapshot> snapshots)
        {
            return GetFingerprint(snapshots, useCapturedState: false);
        }

        public static string GetOriginalFingerprint(IEnumerable<SettlementThreatSnapshot> snapshots)
        {
            return GetFingerprint(snapshots, useCapturedState: true);
        }

        private static string GetFingerprint(
            IEnumerable<SettlementThreatSnapshot> snapshots,
            bool useCapturedState)
        {
            var state = new StringBuilder();
            foreach (var snapshot in snapshots.OrderBy(item => item.settlement.StringId))
            {
                state.Append(snapshot.settlement.StringId).Append('|')
                    .Append(
                        (useCapturedState
                            ? snapshot.lastAttackerParty
                            : snapshot.settlement.LastAttackerParty)?.StringId ?? "none")
                    .Append('|')
                    .Append(
                        (useCapturedState
                            ? snapshot.lastThreatTime
                            : snapshot.settlement.LastThreatTime).NumTicks)
                    .Append('|');
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(state.ToString()));
            var output = new StringBuilder(hash.Length * 2);
            foreach (var value in hash)
                output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            return output.ToString();
        }
    }

    private sealed class SiegeEntryFixture
    {
        public string Token { get; }
        public string ControllerId { get; }
        public MobileParty PlayerParty { get; }
        public PartyBehaviorUpdateData PlayerBehavior { get; }
        public MobileParty AiBesieger { get; }
        public PartyBehaviorUpdateData AiBehavior { get; }
        public Settlement Settlement { get; }
        public string SettlementId { get; }
        public Clan OriginalOwnerClan { get; }
        public string OriginalOwnerClanId { get; }
        public IFaction PlayerFaction { get; }
        public IFaction BesiegerFaction { get; }
        public IFaction SettlementFaction { get; }
        public StanceLinkSnapshot PlayerSettlementStance { get; }
        public StanceLinkSnapshot BesiegerSettlementStance { get; }
        public StanceLinkSnapshot BesiegerPlayerStance { get; }
        public SettlementThreatSnapshot[] SettlementThreatSnapshots { get; }
        public TimeControlEnum OriginalTimeControl { get; }
        public bool PlayerWasDisorganized { get; }
        public bool PlayerWasAtWar => PlayerSettlementStance.WasAtWar;
        public bool BesiegerWasAtWar => BesiegerSettlementStance.WasAtWar;
        public bool BesiegerPlayerWasAtWar => BesiegerPlayerStance.WasAtWar;
        public int OriginalOwnerLeaderRelation { get; }
        public bool OwnerOverridden { get; set; }
        public bool StaleReconnectForced { get; set; }
        public string PhysicalSettlementId { get; set; }
        public bool TimePolicyAdded { get; set; }

        public SiegeEntryFixture(
            string token,
            string controllerId,
            MobileParty playerParty,
            PartyBehaviorUpdateData playerBehavior,
            MobileParty aiBesieger,
            PartyBehaviorUpdateData aiBehavior,
            Settlement settlement,
            string settlementId,
            Clan originalOwnerClan,
            string originalOwnerClanId,
            IFaction playerFaction,
            IFaction besiegerFaction,
            IFaction settlementFaction,
            StanceLinkSnapshot playerSettlementStance,
            StanceLinkSnapshot besiegerSettlementStance,
            StanceLinkSnapshot besiegerPlayerStance,
            SettlementThreatSnapshot[] settlementThreatSnapshots,
            TimeControlEnum originalTimeControl,
            bool playerWasDisorganized,
            int originalOwnerLeaderRelation)
        {
            Token = token;
            ControllerId = controllerId;
            PlayerParty = playerParty;
            PlayerBehavior = playerBehavior;
            AiBesieger = aiBesieger;
            AiBehavior = aiBehavior;
            Settlement = settlement;
            SettlementId = settlementId;
            OriginalOwnerClan = originalOwnerClan;
            OriginalOwnerClanId = originalOwnerClanId;
            PlayerFaction = playerFaction;
            BesiegerFaction = besiegerFaction;
            SettlementFaction = settlementFaction;
            PlayerSettlementStance = playerSettlementStance;
            BesiegerSettlementStance = besiegerSettlementStance;
            BesiegerPlayerStance = besiegerPlayerStance;
            SettlementThreatSnapshots = settlementThreatSnapshots;
            OriginalTimeControl = originalTimeControl;
            PlayerWasDisorganized = playerWasDisorganized;
            OriginalOwnerLeaderRelation = originalOwnerLeaderRelation;
            PhysicalSettlementId = settlementId;
        }
    }
}
