using Common.Commands;
using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Utils.Commands;
using Helpers;
using Newtonsoft.Json;
using ProtoBuf;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Villages.Commands;

public class MapEventDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static readonly ILogger Logger = LogManager.GetLogger<MapEventDebugCommands>();
    private static LateJoinModeFixture lateJoinModeFixture;
    private static InquiryData pendingPrisonerPromptInquiry;

    public sealed class PrisonerPromptStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "prisoner_prompt_state";

        public string Description => "Reports prisoner prompt state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Run this command on a client.");
            return Succeeded(CreatePrisonerPromptStateResult());
        }
    }

    public sealed class PrisonerPromptCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "prisoner_prompt";

        public string Description => "Runs the prisoner prompt debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("action", "The action.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient) return Failed("Run this command on a client.");
            if ((args[0] != "commit" && args[0] != "accept"))
                return Failed("Invalid command argument value.");

            if (args[0] == "commit")
            {
                if (pendingPrisonerPromptInquiry != null)
                    return Failed("A prisoner warning is already pending acceptance.");
                if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
                    partyState.PartyScreenMode != PartyScreenHelper.PartyScreenMode.Loot)
                    return Failed("The real aftermath Party screen is not active.");
                if (partyState.PartyScreenLogic?.PrisonerRosters[0]?.TotalManCount <= 0)
                    return Failed("The aftermath screen has no prisoners left behind.");
                if (!((ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource is { } partyVm))
                    return Failed("The active aftermath Party view model is unavailable.");

                InquiryData capturedInquiry = null;
                Action<InquiryData, bool, bool> captureInquiry = (data, _, _) => capturedInquiry = data;
                InformationManager.OnShowInquiry += captureInquiry;
                try
                {
                    partyVm.ExecuteDone();
                }
                finally
                {
                    InformationManager.OnShowInquiry -= captureInquiry;
                }

                if (capturedInquiry?.AffirmativeAction == null || !InformationManager.IsAnyInquiryActive())
                    return Failed("The real leave-prisoners warning did not open.");

                pendingPrisonerPromptInquiry = capturedInquiry;
                return Succeeded(CreatePrisonerPromptStateResult());
            }

            var inquiry = pendingPrisonerPromptInquiry;
            if (inquiry?.AffirmativeAction == null)
                return Failed("No captured prisoner warning is pending acceptance.");

            pendingPrisonerPromptInquiry = null;
            InformationManager.HideInquiry();
            inquiry.AffirmativeAction();
            return Succeeded(CreatePrisonerPromptStateResult());
        }
    }

    private static string CreatePrisonerPromptStateResult()
    {
        var partyState = Game.Current?.GameStateManager?.ActiveState as PartyState;
        var logic = partyState?.PartyScreenLogic;
        var mainParty = MobileParty.MainParty;
        var army = mainParty?.Army;
        return "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            success = true,
            activeState = Game.Current?.GameStateManager?.ActiveState?.GetType().Name,
            partyScreenActive = partyState != null,
            partyScreenMode = partyState?.PartyScreenMode.ToString(),
            leftPrisoners = logic?.PrisonerRosters[0]?.TotalManCount ?? 0,
            inquiryActive = InformationManager.IsAnyInquiryActive(),
            capturedInquiryPending = pendingPrisonerPromptInquiry != null,
            mainPartyMapEventActive = mainParty?.MapEvent != null,
            mainPartyArmyActive = army != null,
            mainPartyIsNonLeaderArmyMember = army != null && army.LeaderParty != mainParty,
            currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId,
        });
    }

    private sealed class LateJoinModeFixture
    {
        public string MapEventId { get; set; }
        public string FirstControllerId { get; set; }
        public string FirstPlayerPartyId { get; set; }
        public string FirstPlayerMobilePartyId { get; set; }
        public PartyBehaviorUpdateData FirstPlayerBehavior { get; set; }
        public string JoiningControllerId { get; set; }
        public string JoiningPlayerPartyId { get; set; }
        public string JoiningPlayerMobilePartyId { get; set; }
        public PartyBehaviorUpdateData JoiningPlayerBehavior { get; set; }
        public string OpponentMobilePartyId { get; set; }
        public PartyBehaviorUpdateData OpponentBehavior { get; set; }
        public bool JoiningPartyJoined { get; set; }
    }

    private static WoundedAlliedFixture woundedAlliedFixture;
    private static BattleRewardFixture battleRewardFixture;
    private static PlayerFieldBattleFixture playerFieldBattleFixture;
    private static BanditAttackFixture banditAttackFixture;

    private sealed class WoundedAlliedFixture
    {
        public string ControllerId;
        public Hero PlayerHero;
        public MobileParty PlayerParty;
        public MapEvent MapEvent;
        public PartyBase[] InvolvedParties;
        public int OriginalHitPoints;
        public float OriginalRecentEventsMorale;
        public TroopRosterElement[] OriginalRoster;
        public CampaignVec2 OriginalPosition;
    }

    private sealed class BattleRewardFixture
    {
        public BattleRewardPlayerSnapshot Initiator;
        public BattleRewardPlayerSnapshot LateJoiner;
        public Army Army;
        public MobileParty BanditParty;
        public MobileParty ReinforcementParty;
        public CharacterObject BanditTroop;
        public CampaignVec2 FixturePosition;
        public MapEvent MapEvent;
        public MapEventParty InitiatorMapEventParty;
        public MapEventParty LateJoinerMapEventParty;
        public bool LateJoinerAdded;
        public bool ReinforcementAdded;
        public bool PartialRoutIssued;
        public bool EnemiesRouted;
        public bool InitiatorKingdomChanged;
    }

    private sealed class BattleRewardPlayerSnapshot
    {
        public string ControllerId;
        public Hero Hero;
        public Hero LeaderHero;
        public MobileParty Party;
        public TroopRosterElement[] MemberRoster;
        public TroopRosterElement[] PrisonRoster;
        public ItemRosterElement[] ItemRoster;
        public PartyBehaviorUpdateData Behavior;
        public int HitPoints;
        public float RecentEventsMorale;
        public float ClanInfluence;
        public Kingdom ClanKingdom;
        public CampaignTime ClanLastFactionChangeTime;
    }

    private sealed class PlayerFieldBattleFixture
    {
        public MobileParty AttackerParty;
        public MobileParty DefenderParty;
        public IFaction AttackerFaction;
        public IFaction DefenderFaction;
        public bool WasAtWar;
    }

    private sealed class BanditAttackFixture
    {
        public string ControllerId;
        public MobileParty PlayerParty;
        public MobileParty BanditParty;
        public Settlement PlayerSettlement;
        public Settlement BanditSettlement;
        public bool BanditWasActive;
        public TroopRosterElement[] BanditMemberRoster;
        public MapEvent MapEvent;
        public PartyBase[] InvolvedParties;
        public PartyBehaviorUpdateData PlayerBehavior;
        public PartyBehaviorUpdateData BanditBehavior;
    }

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    private static bool MatchesPartyId(IObjectManager objectManager, MobileParty party, string id)
    {
        if (party == null || string.IsNullOrEmpty(id)) return false;
        if (party.StringId == id) return true;
        if (objectManager.TryGetId(party, out string mobilePartyId) && mobilePartyId == id) return true;

        return party.Party != null &&
               objectManager.TryGetId(party.Party, out string partyBaseId) &&
               partyBaseId == id;
    }

    public sealed class StartPlayerFieldBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "start_player_field_battle";

        public string Description => "Runs the start player field battle debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("attacker_mobile_party_id", "The attacker mobile party id.", true),
            new ExpectedArgs("defender_mobile_party_id", "The defender mobile party id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
                return Failed("Run this command on the server.");


            if (playerFieldBattleFixture != null)
                return Failed("A player field-battle fixture is already pending restoration.");

            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager.");

            var attackerError = string.Empty;
            if ((!objectManager.TryGetObject(args[0], out MobileParty attacker) &&
                 !CommandHelpers.TryGetMobileParty(args[0], out attacker, out attackerError)) ||
                attacker?.Party == null)
                return Failed("Unable to resolve attacker party: " + attackerError);

            var defenderError = string.Empty;
            if ((!objectManager.TryGetObject(args[1], out MobileParty defender) &&
                 !CommandHelpers.TryGetMobileParty(args[1], out defender, out defenderError)) ||
                defender?.Party == null)
                return Failed("Unable to resolve defender party: " + defenderError);

            if (attacker == defender)
                return Failed("Attacker and defender parties must be distinct.");

            if (!attacker.IsActive || !defender.IsActive || attacker.MapEvent != null || defender.MapEvent != null)
                return Failed("Both player parties must be active and outside a map event.");

            if (attacker.CurrentSettlement != null || defender.CurrentSettlement != null)
                return Failed("Both player parties must be outside settlements.");

            var attackerFaction = attacker.MapFaction;
            var defenderFaction = defender.MapFaction;
            if (attackerFaction == null || defenderFaction == null || attackerFaction == defenderFaction)
                return Failed("Player parties must belong to distinct map factions.");

            if (!objectManager.TryGetId(attacker, out var attackerMobilePartyId) ||
                !objectManager.TryGetId(defender, out var defenderMobilePartyId) ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                return Failed("Unable to resolve the registered player-party identities.");

            var attackerPlayer = playerManager.Players.FirstOrDefault(player =>
                player.MobilePartyId == attackerMobilePartyId);
            var defenderPlayer = playerManager.Players.FirstOrDefault(player =>
                player.MobilePartyId == defenderMobilePartyId);
            if (attackerPlayer == null || defenderPlayer == null ||
                !playerManager.IsConnected(attackerPlayer) || !playerManager.IsConnected(defenderPlayer))
                return Failed("Both parties must belong to connected players.");

            if (!objectManager.TryGetId(attacker.Party, out var attackerPartyBaseId) ||
                !objectManager.TryGetId(defender.Party, out var defenderPartyBaseId))
                return Failed("Unable to resolve the registered PartyBase ids.");

            if (!ContainerProvider.TryResolve<IPlayerPartyHostileEncounterService>(out var encounterService))
                return Failed("Unable to resolve the player hostile-encounter service.");

            var fixture = new PlayerFieldBattleFixture
            {
                AttackerParty = attacker,
                DefenderParty = defender,
                AttackerFaction = attackerFaction,
                DefenderFaction = defenderFaction,
                WasAtWar = AreFactionsAtWar(attackerFaction, defenderFaction),
            };
            playerFieldBattleFixture = fixture;

            var sessionId = "live-test-" + Guid.NewGuid().ToString("N");
            if (!encounterService.TryStartHostileEncounter(
                    sessionId,
                    attackerPartyBaseId,
                    defenderPartyBaseId,
                    responderSurrenders: false))
            {
                var partiallyCreatedMapEvent = attacker.MapEvent;
                if (partiallyCreatedMapEvent != null &&
                    partiallyCreatedMapEvent == defender.MapEvent &&
                    !partiallyCreatedMapEvent.IsFinalized)
                    partiallyCreatedMapEvent.FinalizeEvent();

                var peaceRestored = RestoreFixtureWarState(fixture);
                playerFieldBattleFixture = null;
                return Failed($"Failed to start the player field-battle fixture. PeaceRestored: {peaceRestored}");
            }

            var mapEvent = attacker.MapEvent;
            var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out var resolvedMapEventId)
                ? resolvedMapEventId
                : "<unresolved>";

            return Succeeded("Player field-battle fixture started.\n" +
                $"MapEventId: {mapEventId}\n" +
                $"AttackerPartyId: {args[0]}\n" +
                $"DefenderPartyId: {args[1]}\n" +
                $"OriginalWarState: {fixture.WasAtWar}");
        }
    }

    public sealed class RestorePlayerFieldBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "restore_player_field_battle";

        public string Description => "Restores or clears restore player field battle.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
                return Failed("Run this command on the server.");


            var fixture = playerFieldBattleFixture;
            if (fixture == null)
                return Failed("No player field-battle fixture is pending restoration.");

            if (fixture.AttackerParty.MapEvent != null || fixture.DefenderParty.MapEvent != null)
                return Failed("Cannot restore the player field-battle fixture while its map event is active.");

            var peaceRestored = RestoreFixtureWarState(fixture);

            playerFieldBattleFixture = null;
            return Succeeded($"Player field-battle fixture restored. PeaceRestored: {peaceRestored}");
        }
    }

    private static bool RestoreFixtureWarState(PlayerFieldBattleFixture fixture)
    {
        if (fixture.WasAtWar || !AreFactionsAtWar(fixture.AttackerFaction, fixture.DefenderFaction))
            return false;

        MakePeaceAction.Apply(fixture.AttackerFaction, fixture.DefenderFaction);
        return true;
    }

    public sealed class RequestPlayerFieldBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "request_player_field_battle";

        public string Description => "Runs the request player field battle debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("defender_mobile_party_id", "The defender mobile party id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient)
                return Failed("Run this command on the attacking client.");


            var attacker = MobileParty.MainParty;
            if (attacker?.Party == null || !attacker.IsActive || attacker.MapEvent != null)
                return Failed("The local player must lead an active party outside a map event.");

            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager.");

            var defenderError = string.Empty;
            if ((!objectManager.TryGetObject(args[0], out MobileParty defender) &&
                 !CommandHelpers.TryGetMobileParty(args[0], out defender, out defenderError)) ||
                defender?.Party == null)
                return Failed("Unable to resolve defender party: " + defenderError);

            if (defender == attacker || !defender.IsActive || defender.MapEvent != null)
                return Failed("The defender must be a distinct active party outside a map event.");

            if (attacker.CurrentSettlement != null || defender.CurrentSettlement != null)
                return Failed("Both player parties must be outside settlements.");

            if (attacker.MapFaction == null || defender.MapFaction == null ||
                attacker.MapFaction == defender.MapFaction)
                return Failed("Player parties must belong to distinct map factions.");

            if (!objectManager.TryGetId(defender, out var defenderMobilePartyId) ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !playerManager.Players.Any(player => player.MobilePartyId == defenderMobilePartyId))
                return Failed("The defender must belong to a registered player.");

            if (!objectManager.TryGetId(attacker.Party, out var attackerPartyId) ||
                !objectManager.TryGetId(defender.Party, out var defenderPartyId))
                return Failed("Unable to resolve the registered PartyBase ids.");

            if (!ContainerProvider.TryResolve<INetwork>(out var network))
                return Failed("Unable to resolve the client network.");

            network.SendAll(new NetworkRequestConversation(
                defenderPartyId,
                attackerPartyId,
                forcePlayerOutFromSettlement: false,
                ConversationRestartSource.PlayerEncounter,
                armyTalkEncounter: false));

            return Succeeded("Player field-battle interaction requested through the production conversation path.\n" +
                $"AttackerPartyId: {attacker.StringId}\n" +
                $"DefenderPartyId: {defender.StringId}");
        }
    }

    public sealed class PlayerInteractionStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "player_interaction_state";

        public string Description => "Reports player interaction state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return Succeeded($"Active: {PlayerPartyInteractionDialogState.HasActiveState}\n" +
                $"SessionId: {PlayerPartyInteractionDialogState.SessionId ?? "none"}\n" +
                $"PartyId: {PlayerPartyInteractionDialogState.PartyId ?? "none"}\n" +
                $"OtherPartyId: {PlayerPartyInteractionDialogState.OtherPartyId ?? "none"}\n" +
                $"Phase: {PlayerPartyInteractionDialogState.Phase}\n" +
                $"Proposal: {PlayerPartyInteractionDialogState.Proposal}");
        }
    }

    public sealed class SubmitPlayerInteractionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "submit_player_interaction";

        public string Description => "Runs the submit player interaction debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("option", "The option.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsClient)
                return Failed("Run this command on a player client.");

            if (!Enum.TryParse(args[0], ignoreCase: true, out PlayerPartyInteractionOption option) ||
                option == PlayerPartyInteractionOption.None)
                return Failed("Invalid command argument value.");

            if (!PlayerPartyInteractionDialogState.HasActiveState)
                return Failed("No player-party interaction is active.");

            if (!PlayerPartyInteractionDialogState.IsOptionEnabled(option))
                return Failed($"Player-party interaction option '{option}' is not enabled.");

            var sessionId = PlayerPartyInteractionDialogState.SessionId;
            PlayerPartyInteractionDialogState.Submit(option);
            return Succeeded($"Submitted player-party interaction option '{option}' for session '{sessionId}'.");
        }
    }

    private static bool AreFactionsAtWar(IFaction first, IFaction second)
    {
        try
        {
            return FactionManager.IsAtWarAgainstFaction(first, second);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts the current battle through the normal client/server mission-start gate.
    /// </summary>
    public sealed class StartAttackMissionCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "start_attack_mission";

        public string Description => "Runs the start attack mission debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return StartAttackMissionCore(args);
        }
    }

    private static CoopCommandResult StartAttackMissionCore(ICoopCommandArgs args)
    {
        if (ModInformation.IsServer)
        {
            return Failed("Run this command on a client");
        }


        var mainParty = MobileParty.MainParty;
        var mapEvent = mainParty?.MapEvent;
        if (mapEvent == null)
        {
            return Failed("The main party has no replicated map event");
        }

        if (!TryGetObjectManager(out var objectManager)
            || !objectManager.TryGetId(mapEvent, out var mapEventId)
            || !objectManager.TryGetId(mainParty, out var partyId))
        {
            return Failed("Unable to resolve the current battle ids");
        }

        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null)
        {
            return Failed("Battle start coordinator is unavailable");
        }

        bool requested = coordinator.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId);
        return requested
            ? Succeeded($"Starting attack mission for {mapEventId}")
            : Failed($"Server rejected attack mission for {mapEventId}");


    }

    // coop.debug.map_event.start_looter
    /// <summary>
    /// Starts combat with looter
    /// </summary>
    public sealed class StartLooterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "start_looter";

        public string Description => "Runs the start looter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager");

            if (!objectManager.TryGetObject("sea_raiders_1", out PartyBase partyBase))
            {
                return Failed($"BesiegerCamp with ID: sea_raiders_1 not found");
            }

            EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, partyBase);


            return Succeeded($"MapEvent Started");
        }
    }

    // coop.debug.map_event.start_nearest_looter
    /// <summary>
    /// Forces an encounter between the player's party and the nearest active bandit/looter party, so
    /// the bandit surrender/recruit dialogue can be reached without chasing one down. Run on a client
    /// (uses the player's main party). Bring a much larger party than the bandits so they offer to
    /// surrender or join.
    /// </summary>
    public sealed class StartNearestLooterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "start_nearest_looter";

        public string Description => "Runs the start nearest looter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
            }

            var mainParty = MobileParty.MainParty;
            if (mainParty == null)
            {
                return Failed("No main party — run this on a client with a player party.");
            }

            var mainPos = mainParty.Position.ToVec2();
            var nearest = MobileParty.All
                .Where(p => p.IsActive && p.IsBandit && p != mainParty
                            && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0)
                .OrderBy(p => p.Position.ToVec2().DistanceSquared(mainPos))
                .FirstOrDefault();

            if (nearest == null)
            {
                return Failed("No active bandit/looter party found on the map.");
            }

            EncounterManager.StartPartyEncounter(mainParty.Party, nearest.Party);

            var partyId = objectManager.TryGetId(nearest, out string registryId) ? registryId : nearest.StringId;

            return Succeeded($"Started encounter with {nearest.Name} (StringId {nearest.StringId}, registry id {partyId}), " +
                   $"{nearest.MemberRoster.TotalManCount} troops, {nearest.Position.ToVec2().Distance(mainPos):0.0} away.");
        }
    }

    // coop.debug.map_event.start_nearest_bandit_attack PlayerOne [excludedPartyId]
    /// <summary>
    /// Starts a server-authoritative bandit attack encounter against a connected player.
    /// </summary>
    public sealed class StartNearestBanditAttackCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "start_nearest_bandit_attack";

        public string Description => "Runs the start nearest bandit attack debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
            new ExpectedArgs("excluded_party_id", "The excluded party id.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Run this command on the server.");
            }


            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
            {
                return Failed(error);
            }

            const int maximumFixtureTroops = 8;
            var remainingFixtureTroops = maximumFixtureTroops;
            var removedTroops = 0;
            for (var index = playerParty.MemberRoster.Count - 1; index >= 0; index--)
            {
                var element = playerParty.MemberRoster.GetElementCopyAtIndex(index);
                if (element.Character.IsHero)
                    continue;

                var kept = Math.Min(element.Number, remainingFixtureTroops);
                var removed = element.Number - kept;
                if (removed > 0)
                {
                    playerParty.MemberRoster.AddToCountsAtIndex(
                        index,
                        -removed,
                        -Math.Min(element.WoundedNumber, removed),
                        removeDepleted: false);
                    removedTroops += removed;
                }
                remainingFixtureTroops -= kept;
            }
            playerParty.MemberRoster.RemoveZeroCounts();

            if (playerParty.CurrentSettlement != null)
            {
                LeaveSettlementAction.ApplyForParty(playerParty);
            }

            var excludedPartyId = args.Count == 2 ? args[1] : null;
            var playerPosition = playerParty.Position.ToVec2();
            var banditParty = MobileParty.All
                .Where(p => p.IsActive && p.IsBandit && p != playerParty
                            && p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalManCount > 0
                            && !MatchesPartyId(objectManager, p, excludedPartyId))
                .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
                .FirstOrDefault();

            if (banditParty == null)
            {
                return Failed("No active bandit/looter party found on the map.");
            }

            StartBattleAction.Apply(banditParty.Party, playerParty.Party);

            var partyId = objectManager.TryGetId(banditParty, out string registryId)
                ? registryId
                : banditParty.StringId;
            var partyBaseId = objectManager.TryGetId(banditParty.Party, out string partyBaseRegistryId)
                ? partyBaseRegistryId
                : "<unregistered>";

            return Succeeded($"Started attack by {banditParty.Name} (StringId {banditParty.StringId}, " +
                   $"registry id {partyId}, PartyBase id {partyBaseId}) " +
                   $"against player {args[0]} after removing {removedTroops} excess fixture troops.");
        }
    }

    // coop.debug.map_event.bandit_attack_fixture_prepare PlayerOne mountain_bandits_24
    /// <summary>Prepares a reversible exact-bandit attack fixture for evidence capture.</summary>
    public sealed class BanditAttackFixturePrepareCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "bandit_attack_fixture_prepare";

        public string Description => "Runs the bandit attack fixture prepare debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
            new ExpectedArgs("bandit_party_id", "The bandit party id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (banditAttackFixture != null)
                return Failed("A bandit attack fixture is already active.");

            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
                return Failed(error);

            if ((!objectManager.TryGetObject(args[1], out MobileParty banditParty) &&
                 !CommandHelpers.TryGetMobileParty(args[1], out banditParty, out error)) ||
                banditParty?.Party == null)
            {
                return Failed($"Unable to resolve bandit party {args[1]}: {error}");
            }

            if (!banditParty.IsBandit || banditParty.MapEvent != null)
            {
                return Failed($"Bandit party {args[1]} must be a bandit outside a map event.");
            }

            if (playerParty.Army?.LeaderParty == playerParty && playerParty.AttachedParties.Count > 0)
                return Failed($"Player {args[0]} must not lead an army with attached parties.");

            if (banditParty.CurrentSettlement?.SettlementComponent is Hideout &&
                banditParty.CurrentSettlement.Parties.Count <= 1)
            {
                return Failed($"Bandit party {args[1]} must not be the last party in its hideout.");
            }

            if (!objectManager.TryGetId(playerParty, out string playerPartyId) ||
                !objectManager.TryGetId(playerParty.Party, out string playerPartyBaseId) ||
                !objectManager.TryGetId(banditParty, out string banditPartyId) ||
                !objectManager.TryGetId(banditParty.Party, out string banditPartyBaseId))
            {
                return Failed("The player and bandit parties must have registered MobileParty and PartyBase ids.");
            }

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
                !behaviorSnapshot.TryCreate(playerParty, out PartyBehaviorUpdateData playerBehavior) ||
                !behaviorSnapshot.TryCreate(banditParty, out PartyBehaviorUpdateData banditBehavior))
            {
                return Failed("Unable to capture the original party behavior.");
            }

            CharacterObject fixtureTroop = null;
            if (banditParty.MemberRoster.TotalManCount <= 0)
            {
                fixtureTroop = MobileParty.All
                    .Where(party => party != banditParty && party.IsActive && party.IsBandit &&
                                    party.MemberRoster.TotalManCount > 0)
                    .SelectMany(party => party.MemberRoster.GetTroopRoster())
                    .Where(element => !element.Character.IsHero && element.Number > 0)
                    .OrderByDescending(element => element.Number)
                    .Select(element => element.Character)
                    .FirstOrDefault();
                if (fixtureTroop == null)
                    return Failed("No active bandit party has a regular troop for the fixture.");
            }

            var fixture = new BanditAttackFixture
            {
                ControllerId = args[0],
                PlayerParty = playerParty,
                BanditParty = banditParty,
                PlayerSettlement = playerParty.CurrentSettlement,
                BanditSettlement = banditParty.CurrentSettlement,
                BanditWasActive = banditParty.IsActive,
                BanditMemberRoster = banditParty.MemberRoster.GetTroopRoster().ToArray(),
                PlayerBehavior = playerBehavior,
                BanditBehavior = banditBehavior,
            };
            banditAttackFixture = fixture;

            try
            {
                if (playerParty.CurrentSettlement != null)
                    LeaveSettlementAction.ApplyForParty(playerParty);
                if (banditParty.CurrentSettlement != null)
                    LeaveSettlementAction.ApplyForParty(banditParty);

                if (fixtureTroop != null)
                    banditParty.MemberRoster.AddToCounts(fixtureTroop, 1);
                banditParty.IsActive = true;
                banditParty.Position = new CampaignVec2(
                    new Vec2(playerParty.Position.X - 0.4f, playerParty.Position.Y),
                    isOnLand: true);
                banditParty.SetMoveModeHold();
                banditParty.ResetNavigationToHold();

                MessageBroker.Instance.Publish(
                    typeof(MapEventDebugCommands),
                    new PartyBehaviorChangeAttempted(
                        banditParty,
                        forcePosition: true,
                        isCurrentlyAtSea: false,
                        resetMovementToHold: true));

                return Succeeded($"Bandit attack fixture prepared: controller={args[0]}, playerParty={playerPartyId}, " +
                       $"playerPartyBase={playerPartyBaseId}, banditParty={banditPartyId}, " +
                       $"banditPartyBase={banditPartyBaseId}, banditStringId={banditParty.StringId}, " +
                       $"originalSettlement={fixture.PlayerSettlement?.StringId ?? "none"}, " +
                       $"originalBanditSettlement={fixture.BanditSettlement?.StringId ?? "none"}, " +
                       $"originalBanditActive={fixture.BanditWasActive}, " +
                       $"originalBanditTroops={fixture.BanditMemberRoster.Sum(element => element.Number)}.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to prepare bandit attack fixture");
                if (TryRestoreBanditAttackFixture(fixture, out var restoreError))
                    banditAttackFixture = null;
                else
                    return Failed($"Fixture preparation failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.");

                return Failed($"Fixture preparation failed: {e.Message}");
            }
        }
    }

    // coop.debug.map_event.bandit_attack_fixture_start PlayerOne mountain_bandits_24
    /// <summary>Starts the prepared server-authoritative attack by the exact bandit party.</summary>
    public sealed class BanditAttackFixtureStartCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "bandit_attack_fixture_start";

        public string Description => "Runs the bandit attack fixture start debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
            new ExpectedArgs("bandit_party_id", "The bandit party id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager");

            var fixture = banditAttackFixture;
            if (fixture == null || fixture.ControllerId != args[0] ||
                !MatchesPartyId(objectManager, fixture.BanditParty, args[1]))
            {
                return Failed($"Prepare the bandit attack fixture for {args[0]} and {args[1]} first.");
            }

            if (fixture.MapEvent != null)
                return Failed("The bandit attack fixture was already started.");

            var playerParty = fixture.PlayerParty;
            var banditParty = fixture.BanditParty;
            if (!banditParty.IsActive || banditParty.MemberRoster.TotalManCount <= 0 ||
                playerParty.CurrentSettlement != null || banditParty.CurrentSettlement != null ||
                playerParty.MapEvent != null || banditParty.MapEvent != null)
            {
                return Failed("The prepared bandit attack fixture is no longer ready.");
            }

            if (!objectManager.TryGetId(playerParty, out string playerPartyId) ||
                !objectManager.TryGetId(playerParty.Party, out string playerPartyBaseId) ||
                !objectManager.TryGetId(banditParty, out string banditPartyId) ||
                !objectManager.TryGetId(banditParty.Party, out string banditPartyBaseId))
            {
                return Failed("The player and bandit parties must have registered MobileParty and PartyBase ids.");
            }

            try
            {
                StartBattleAction.Apply(banditParty.Party, playerParty.Party);
                fixture.MapEvent = playerParty.MapEvent;
                if (fixture.MapEvent == null || banditParty.MapEvent != fixture.MapEvent)
                    throw new InvalidOperationException("The bandit attack did not create a shared map event.");

                fixture.InvolvedParties = fixture.MapEvent.InvolvedParties.ToArray();

                objectManager.TryGetId(fixture.MapEvent, out string mapEventId);
                return Succeeded($"Bandit attack fixture started: controller={args[0]}, playerParty={playerPartyId}, " +
                       $"playerPartyBase={playerPartyBaseId}, banditParty={banditPartyId}, " +
                       $"banditPartyBase={banditPartyBaseId}, banditStringId={banditParty.StringId}, " +
                       $"mapEvent={mapEventId ?? "unregistered"}.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to start bandit attack fixture");
                fixture.MapEvent ??= playerParty.MapEvent ?? banditParty.MapEvent;
                fixture.InvolvedParties ??= fixture.MapEvent?.InvolvedParties.ToArray();
                if (TryRestoreBanditAttackFixture(fixture, out var restoreError))
                    banditAttackFixture = null;
                else
                    return Failed($"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.");

                return Failed($"Fixture setup failed: {e.Message}");
            }
        }
    }

    // coop.debug.map_event.bandit_attack_fixture_state PlayerOne mountain_bandits_24
    /// <summary>Reports the exact bandit attack state on the server or a client.</summary>
    public sealed class BanditAttackFixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "bandit_attack_fixture_state";

        public string Description => "Reports bandit attack fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
            new ExpectedArgs("bandit_party_id", "The bandit party id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetPlayerParty(
                    args[0],
                    requireReady: false,
                    out var objectManager,
                    out var playerParty,
                    out var error,
                    allowActiveMapEvent: true))
            {
                return Failed(error);
            }

            if ((!objectManager.TryGetObject(args[1], out MobileParty banditParty) &&
                 !CommandHelpers.TryGetMobileParty(args[1], out banditParty, out error)) ||
                banditParty == null)
            {
                return Failed($"Unable to resolve bandit party {args[1]}: {error}");
            }

            objectManager.TryGetId(playerParty, out string playerPartyId);
            objectManager.TryGetId(banditParty, out string banditPartyId);
            objectManager.TryGetId(playerParty.MapEvent, out string playerMapEventId);
            objectManager.TryGetId(banditParty.MapEvent, out string banditMapEventId);

            return Succeeded($"Bandit attack fixture state: controller={args[0]}, local={playerParty == MobileParty.MainParty}, " +
                   $"playerParty={playerPartyId ?? "unregistered"}, banditParty={banditPartyId ?? "unregistered"}, " +
                   $"banditStringId={banditParty.StringId}, playerMapEvent={playerMapEventId ?? "none"}, " +
                   $"banditMapEvent={banditMapEventId ?? "none"}, " +
                   $"sharedMapEvent={playerParty.MapEvent != null && playerParty.MapEvent == banditParty.MapEvent}, " +
                   $"playerSettlement={playerParty.CurrentSettlement?.StringId ?? "none"}, " +
                   $"banditSettlement={banditParty.CurrentSettlement?.StringId ?? "none"}, " +
                   $"banditActive={banditParty.IsActive}, banditTroops={banditParty.MemberRoster.TotalManCount}, " +
                   $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}.");
        }
    }

    // coop.debug.map_event.bandit_attack_fixture_restore PlayerOne
    /// <summary>Finalizes the bandit attack and restores both parties' original behavior.</summary>
    public sealed class BanditAttackFixtureRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "bandit_attack_fixture_restore";

        public string Description => "Restores or clears bandit attack fixture restore.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (banditAttackFixture == null || banditAttackFixture.ControllerId != args[0])
                return Failed($"No active bandit attack fixture exists for {args[0]}.");

            var fixture = banditAttackFixture;
            if (!TryRestoreBanditAttackFixture(fixture, out var error))
                return Failed($"Fixture restore failed: {error}. Retry the restore command.");

            banditAttackFixture = null;
            return Succeeded($"Bandit attack fixture restored: controller={args[0]}, banditStringId={fixture.BanditParty.StringId}.");
        }
    }

    private static bool TryRestoreBanditAttackFixture(BanditAttackFixture fixture, out string error)
    {
        try
        {
            if (fixture.MapEvent != null && !fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (HasAttachedParties(fixture.MapEvent, fixture.InvolvedParties))
                RecoverPartiallyFinalizedMapEvent(fixture.MapEvent, fixture.InvolvedParties);

            if (fixture.PlayerParty.MapEvent != null || fixture.BanditParty.MapEvent != null)
                throw new InvalidOperationException("The fixture parties are still attached to a map event.");

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
                !RestorePartyBehavior(fixture.PlayerParty, fixture.PlayerBehavior, behaviorSnapshot) ||
                !RestorePartyBehavior(fixture.BanditParty, fixture.BanditBehavior, behaviorSnapshot))
            {
                throw new InvalidOperationException("Unable to restore the original party behavior.");
            }

            MessageBroker.Instance.Publish(
                typeof(MapEventDebugCommands),
                new PartyBehaviorChangeAttempted(
                    fixture.PlayerParty,
                    forcePosition: true,
                    isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea));
            MessageBroker.Instance.Publish(
                typeof(MapEventDebugCommands),
                new PartyBehaviorChangeAttempted(
                    fixture.BanditParty,
                    forcePosition: true,
                    isCurrentlyAtSea: fixture.BanditParty.IsCurrentlyAtSea));

            RestoreTroopRoster(fixture.BanditParty.MemberRoster, fixture.BanditMemberRoster);
            fixture.BanditParty.IsActive = fixture.BanditWasActive;

            if (fixture.PlayerSettlement != null)
                EnterSettlementAction.ApplyForParty(fixture.PlayerParty, fixture.PlayerSettlement);
            if (fixture.BanditSettlement != null)
                EnterSettlementAction.ApplyForParty(fixture.BanditParty, fixture.BanditSettlement);

            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore bandit attack fixture");
            error = e.Message;
            return false;
        }
    }

    public sealed class FinishNonBattleEncounterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "finish_non_battle_encounter";

        public string Description => "Runs the finish non battle encounter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (PlayerEncounter.Current == null)
                return Failed("No player encounter is active.");
            if (PlayerEncounter.Battle != null || MobileParty.MainParty?.MapEvent != null)
                return Failed("Refusing to finish a battle encounter.");

            PlayerEncounter.Finish();
            return Succeeded("Finished the current non-battle encounter.");
        }
    }

    public sealed class JoinExistingCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "join_existing";

        public string Description => "Runs the join existing debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("map_event_id", "The map event id.", true),
            new ExpectedArgs("side", "The battle side.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            if (!Enum.TryParse(args[1], true, out BattleSideEnum side) ||
                (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
            {
                return Failed("Invalid command argument value.");
            }

            if (!TryGetObjectManager(out var objectManager))
                return Failed("Unable to resolve ObjectManager");
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(args[0], out var mapEvent))
                return Failed($"Unable to resolve map event {args[0]}.");
            if (mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None)
                return Failed($"Map event {args[0]} is already concluded.");

            var encounter = PlayerEncounter.Current;
            if (encounter != null && PlayerEncounter.Battle != mapEvent)
                return Failed("A player encounter for another map event is already active.");

            if (encounter?.IsJoinedBattle == true)
            {
                if (encounter.PlayerSide != side)
                    return Failed($"The active encounter already joined the {encounter.PlayerSide} side.");

                return Succeeded($"Started the {side} join encounter for map event {args[0]}.");
            }

            var opposingParty = mapEvent.GetLeaderParty(
                side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            if (opposingParty == null)
                return Failed($"Map event {args[0]} has no opposing leader party.");

            if (encounter == null)
            {
                PlayerEncounter.Start();
                if (side == BattleSideEnum.Attacker)
                    PlayerEncounter.Current.SetupFields(MobileParty.MainParty.Party, opposingParty);
                else
                    PlayerEncounter.Current.SetupFields(opposingParty, MobileParty.MainParty.Party);
            }

            PlayerEncounter.JoinBattle(side);

            return Succeeded($"Started the {side} join encounter for map event {args[0]}.");
        }
    }

    // coop.debug.map_event.battle_reward_fixture_prepare testclient testclient2
    /// <summary>Closes the unfinished idle player encounter loaded by the #2308 live-test save.</summary>
    public sealed class BattleRewardFixturePrepareCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_prepare";

        public string Description => "Runs the battle reward fixture prepare debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("initiator_controller_id", "The initiator controller id.", true),
            new ExpectedArgs("late_joiner_controller_id", "The late joiner controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (args[0] == args[1])
                return Failed("The initiator and late joiner must be different players.");

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !playerManager.TryGetPlayer(args[0], out var initiatorPlayer) ||
                !playerManager.TryGetPlayer(args[1], out var lateJoinerPlayer) ||
                !playerManager.IsConnected(initiatorPlayer) ||
                !playerManager.IsConnected(lateJoinerPlayer))
                return Failed("Both fixture players must be connected.");

            if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var initiatorParty, out var error))
                return Failed(error);

            if (!TryGetPlayerParty(args[1], requireReady: false, out _, out var lateJoinerParty, out error))
                return Failed(error);

            var mapEvent = initiatorParty.MapEvent;
            if (mapEvent == null && lateJoinerParty.MapEvent == null)
                return Failed("Battle reward fixture preflight is already clean.");

            if (mapEvent == null || lateJoinerParty.MapEvent != mapEvent)
                return Failed("The fixture players must share the same saved map event.");

            if (mapEvent.IsFinalized)
                return Failed("The saved map event is already finalized.");

            if (mapEvent.BattleState != BattleState.None)
                return Failed($"Refusing to finalize saved map event with battle state {mapEvent.BattleState}.");

            if (mapEvent.MapEventSettlement != null || mapEvent.BattleObserver != null)
                return Failed("Refusing to finalize a settlement or active simulation map event.");

            var mapEventId = objectManager.TryGetId(mapEvent, out string resolvedMapEventId)
                ? resolvedMapEventId
                : "<unregistered>";
            mapEvent.FinalizeEvent();

            if (!mapEvent.IsFinalized || initiatorParty.MapEvent != null || lateJoinerParty.MapEvent != null)
                return Failed($"Saved map event {mapEventId} did not finalize cleanly.");

            return Succeeded($"Battle reward fixture preflight prepared: finalized={mapEventId}, battleState=None.");
        }
    }

    // coop.debug.map_event.battle_reward_fixture_start testclient testclient2 army
    /// <summary>Creates the two-player late-join field battle from #2308.</summary>
    public sealed class BattleRewardFixtureStartCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_start";

        public string Description => "Runs the battle reward fixture start debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("initiator_controller_id", "The initiator controller id.", true),
            new ExpectedArgs("late_joiner_controller_id", "The late joiner controller id.", true),
            new ExpectedArgs("army", "The army.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            bool createArmy = args.Count == 3;
            if (createArmy && !string.Equals(args[2], "army", StringComparison.OrdinalIgnoreCase))
                return Failed("army must be the literal value 'army' when supplied.");

            if (args[0] == args[1])
                return Failed("The initiator and late joiner must be different players.");

            if (battleRewardFixture != null)
                return Failed("A battle reward fixture is already active.");

            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var initiatorParty, out var error))
                return Failed(error);

            if (!TryGetPlayerParty(args[1], requireReady: true, out _, out var lateJoinerParty, out error))
                return Failed(error);

            if (VillageHostileFactionStanceHelper.HasWarStance(initiatorParty.MapFaction, lateJoinerParty.MapFaction))
                return Failed("The fixture players must be allied.");

            if (createArmy && (initiatorParty.Army != null || lateJoinerParty.Army != null))
                return Failed("Both fixture players must be outside an army before starting army mode.");

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                return Failed("Unable to resolve the mobile-party behavior snapshot service.");

            if (!TryCreateBattleRewardPlayerSnapshot(
                    args[0],
                    objectManager,
                    initiatorParty,
                    behaviorSnapshot,
                    out var initiator,
                    out error))
                return Failed(error);

            if (!TryCreateBattleRewardPlayerSnapshot(
                    args[1],
                    objectManager,
                    lateJoinerParty,
                    behaviorSnapshot,
                    out var lateJoiner,
                    out error))
                return Failed(error);

            var danustica = Settlement.All.FirstOrDefault(settlement => settlement.StringId == "town_ES1");
            if (danustica == null)
                return Failed("Danustica (town_ES1) was not found.");

            var referenceBandit = MobileParty.All.FirstOrDefault(party =>
                party.IsActive &&
                party.IsBandit &&
                party.ActualClan != null &&
                party.PartyComponent is BanditPartyComponent &&
                party.MemberRoster.TotalManCount > 0);
            if (referenceBandit == null)
                return Failed("No active bandit party is available as a fixture template.");

            var banditTroop = referenceBandit.MemberRoster.GetTroopRoster()
                .Where(element => !element.Character.IsHero)
                .OrderByDescending(element => element.Number)
                .Select(element => element.Character)
                .FirstOrDefault();
            if (banditTroop == null)
                return Failed("The bandit fixture template has no regular troop.");

            var fixture = new BattleRewardFixture
            {
                Initiator = initiator,
                LateJoiner = lateJoiner,
                BanditTroop = banditTroop,
            };
            battleRewardFixture = fixture;

            try
            {
                var fixturePosition = new CampaignVec2(
                    new Vec2(danustica.GatePosition.X - 1.5f, danustica.GatePosition.Y),
                    isOnLand: true);
                fixture.FixturePosition = fixturePosition;
                PrepareBattleRewardPlayer(initiator, totalTroops: 60, fixturePosition);
                PrepareBattleRewardPlayer(
                    lateJoiner,
                    totalTroops: 20,
                    new CampaignVec2(new Vec2(fixturePosition.X - 0.2f, fixturePosition.Y), isOnLand: true));

                if (createArmy)
                {
                    var clan = initiator.Hero.Clan;
                    if (clan == null)
                        throw new InvalidOperationException($"Player {initiator.ControllerId} must belong to a clan for army mode.");

                    var kingdom = clan.Kingdom;
                    if (kingdom == null)
                    {
                        kingdom = danustica.MapFaction as Kingdom ?? danustica.OwnerClan?.Kingdom;
                        if (kingdom == null)
                            throw new InvalidOperationException("Danustica does not belong to a kingdom for army mode.");
                        if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
                            throw new InvalidOperationException("Unable to resolve the kingdom membership state service.");

                        fixture.InitiatorKingdomChanged = true;
                        kingdomMembershipState.MoveClanToKingdom(
                            null,
                            kingdom,
                            clan,
                            publishCollectionChanges: true,
                            republishExistingCollections: true);
                        if (clan.Kingdom != kingdom)
                            throw new InvalidOperationException("The initiator clan was not added to the fixture kingdom.");
                    }

                    if (initiator.Party.LeaderHero != initiator.Hero)
                        throw new InvalidOperationException("The fixture initiator must lead its party before creating an army.");

                    fixture.Army = new Army(kingdom, initiator.Party, Army.ArmyTypes.Raider);
                    fixture.Army.Gather(danustica);
                    CampaignEventDispatcher.Instance.OnArmyCreated(fixture.Army);
                    if (fixture.Army == null || fixture.Army.LeaderParty != initiator.Party)
                        throw new InvalidOperationException("The fixture army was not created with the initiator as its leader.");

                    lateJoiner.Party.Army = fixture.Army;
                    if (lateJoiner.Party.Army != fixture.Army)
                        throw new InvalidOperationException("The late joiner was not added to the fixture army.");
                }

                var banditComponent = (BanditPartyComponent)referenceBandit.PartyComponent;
                fixture.BanditParty = BanditPartyComponent.CreateBanditParty(
                    $"debug_2308_reward_bandits_{Guid.NewGuid():N}",
                    referenceBandit.ActualClan,
                    banditComponent.Hideout,
                    isBossParty: false,
                    pt: null,
                    new CampaignVec2(new Vec2(fixturePosition.X - 0.4f, fixturePosition.Y), isOnLand: true));
                fixture.BanditParty.MemberRoster.AddToCounts(banditTroop, 30);
                fixture.BanditParty.PrisonRoster.AddToCounts(banditTroop, 120);
                fixture.BanditParty.ItemRoster.AddToCounts(DefaultItems.Grain, 600);
                fixture.BanditParty.SetMoveModeHold();

                fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                    fixture.BanditParty.Party,
                    initiator.Party.Party,
                    default);
                if (fixture.MapEvent == null)
                    throw new InvalidOperationException("The fixture battle did not create a map event.");

                fixture.InitiatorMapEventParty = fixture.MapEvent.DefenderSide.Parties
                    .FirstOrDefault(party => party.Party == initiator.Party.Party);
                if (fixture.InitiatorMapEventParty == null)
                    throw new InvalidOperationException("The initiating party was not added to the fixture battle.");

                if (!ContainerProvider.TryResolve<INetwork>(out var network) ||
                    !objectManager.TryGetId(fixture.BanditParty.Party, out string banditPartyId) ||
                    !objectManager.TryGetId(initiator.Party.Party, out string initiatorPartyId) ||
                    !objectManager.TryGetId(fixture.MapEvent, out string mapEventId))
                {
                    throw new InvalidOperationException("Unable to resolve the fixture's network ids.");
                }

                var armyId = "none";
                if (fixture.Army != null && !objectManager.TryGetId(fixture.Army, out armyId))
                    throw new InvalidOperationException("Unable to resolve the fixture army's network id.");

                network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                    $"debug-2308-initiator-{Guid.NewGuid():N}",
                    banditPartyId,
                    initiatorPartyId,
                    mapEventId));

                return Succeeded($"Battle reward fixture started: mapEvent={mapEventId}, initiator={args[0]}, " +
                       $"initiatorTroops={initiator.Party.MemberRoster.TotalManCount}, lateJoiner={args[1]}, " +
                       $"lateJoinerTroops={lateJoiner.Party.MemberRoster.TotalManCount}, " +
                       $"bandit={fixture.BanditParty.StringId}, banditTroops={fixture.BanditParty.MemberRoster.TotalManCount}, " +
                       $"army={armyId}, armyMember={lateJoiner.Party.Army == fixture.Army && fixture.Army != null}, " +
                       $"position={fixturePosition.X:R}|{fixturePosition.Y:R}.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to create battle reward fixture");
                if (createArmy && fixture.Army == null)
                    fixture.Army = fixture.Initiator.Party.Army;
                if (TryRestoreBattleRewardFixture(fixture, out var restoreError))
                    battleRewardFixture = null;
                else
                    return Failed($"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.");

                return Failed($"Fixture setup failed: {e.Message}");
            }
        }
    }

    public sealed class BattleRewardFixtureReinforceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_reinforce";

        public string Description => "Runs the battle reward fixture reinforce debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            var fixture = battleRewardFixture;
            if (fixture == null)
                return Failed("No battle reward fixture is active.");
            if (fixture.MapEvent.IsFinalized)
                return Failed("The fixture battle is already finalized.");
            if (fixture.ReinforcementAdded)
                return Failed("The fixture reinforcement was already added.");

            var banditSide = fixture.BanditParty?.Party?.MapEventSide;
            var banditComponent = fixture.BanditParty?.PartyComponent as BanditPartyComponent;
            if (banditSide == null || banditComponent == null || fixture.BanditTroop == null)
                return Failed("The fixture bandit side is no longer available.");

            fixture.ReinforcementParty = BanditPartyComponent.CreateBanditParty(
                $"debug_2423_reward_reinforcement_{Guid.NewGuid():N}",
                fixture.BanditParty.ActualClan,
                banditComponent.Hideout,
                isBossParty: false,
                pt: null,
                new CampaignVec2(
                    new Vec2(fixture.FixturePosition.X - 0.6f, fixture.FixturePosition.Y),
                    isOnLand: true));
            fixture.ReinforcementParty.MemberRoster.AddToCounts(fixture.BanditTroop, 12);
            fixture.ReinforcementParty.SetMoveModeHold();
            fixture.ReinforcementParty.Party.MapEventSide = banditSide;
            fixture.ReinforcementAdded = true;

            return Succeeded($"Battle reward fixture reinforced: party={fixture.ReinforcementParty.StringId}, " +
                   $"troops={fixture.ReinforcementParty.MemberRoster.TotalManCount}, " +
                   $"enemyParties={banditSide.Parties.Count}.");
        }
    }

    // coop.debug.map_event.battle_reward_fixture_join
    /// <summary>Adds the second player to the active #2308 battle and opens its encounter.</summary>
    public sealed class BattleRewardFixtureJoinCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_join";

        public string Description => "Runs the battle reward fixture join debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            var fixture = battleRewardFixture;
            if (fixture == null)
                return Failed("No battle reward fixture is active.");

            if (fixture.LateJoinerAdded)
                return Failed($"Late joiner {fixture.LateJoiner.ControllerId} is already in the fixture battle.");

            if (fixture.MapEvent.IsFinalized)
                return Failed("The fixture battle is already finalized.");

            var joiningParty = fixture.LateJoiner.Party.Party;
            if (joiningParty.MapEventSide != null)
                return Failed($"Late joiner {fixture.LateJoiner.ControllerId} is already in a map event.");

            var joiningSide = fixture.Initiator.Party.Party.MapEventSide;
            if (joiningSide == null)
                return Failed("The initiating party is no longer in the fixture battle.");

            joiningParty.MapEventSide = joiningSide;
            fixture.LateJoinerMapEventParty = joiningSide.Parties
                .FirstOrDefault(party => party.Party == joiningParty);
            if (fixture.LateJoinerMapEventParty == null)
                return Failed("The late joiner was not added to the fixture battle.");

            if (!TryGetObjectManager(out var objectManager) ||
                !ContainerProvider.TryResolve<INetwork>(out var network) ||
                !objectManager.TryGetId(fixture.BanditParty.Party, out string banditPartyId) ||
                !objectManager.TryGetId(joiningParty, out string joiningPartyId) ||
                !objectManager.TryGetId(fixture.MapEvent, out string mapEventId))
            {
                return Failed("Unable to resolve the late join encounter ids.");
            }

            fixture.LateJoinerAdded = true;
            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                $"debug-2308-late-join-{Guid.NewGuid():N}",
                banditPartyId,
                joiningPartyId,
                mapEventId));

            return Succeeded($"Battle reward fixture late join opened: mapEvent={mapEventId}, " +
                   $"controller={fixture.LateJoiner.ControllerId}, party={fixture.LateJoiner.Party.StringId}.");
        }
    }

    public sealed class BattleRewardFixtureBeginRoutCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_begin_rout";

        public string Description => "Runs the battle reward fixture begin rout debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            var fixture = battleRewardFixture;
            if (fixture == null)
                return Failed("No battle reward fixture is active.");
            if (fixture.MapEvent.IsFinalized)
                return Failed("The fixture battle is already finalized.");
            if (!fixture.LateJoinerAdded)
                return Failed("Add the late joiner before routing the fixture enemies.");
            if (!fixture.ReinforcementAdded)
                return Failed("Add the fixture reinforcement before routing enemies.");
            if (fixture.PartialRoutIssued)
                return Failed("The fixture partial rout was already issued.");
            if (!TryGetObjectManager(out var objectManager) ||
                !objectManager.TryGetId(fixture.MapEvent, out string mapEventId) ||
                !ContainerProvider.TryResolve<INetwork>(out var network))
            {
                return Failed("Unable to resolve the fixture battle network state.");
            }

            fixture.PartialRoutIssued = true;
            network.SendAll(new NetworkRouteBattleEnemies(mapEventId, enemiesToLeaveFighting: 20));
            return Succeeded($"Ordered fixture enemies to retreat while leaving up to 20 fighting: mapEvent={mapEventId}.");
        }
    }

    public sealed class BattleRewardFixtureRouteEnemiesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_route_enemies";

        public string Description => "Runs the battle reward fixture route enemies debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            var fixture = battleRewardFixture;
            if (fixture == null)
                return Failed("No battle reward fixture is active.");
            if (fixture.MapEvent.IsFinalized)
                return Failed("The fixture battle is already finalized.");
            if (!fixture.PartialRoutIssued)
                return Failed("Begin the fixture rout before routing the final enemy.");
            if (fixture.EnemiesRouted)
                return Failed("The fixture enemies were already ordered to retreat.");
            if (!TryGetObjectManager(out var objectManager) ||
                !objectManager.TryGetId(fixture.MapEvent, out string mapEventId) ||
                !ContainerProvider.TryResolve<INetwork>(out var network))
            {
                return Failed("Unable to resolve the fixture battle network state.");
            }

            fixture.EnemiesRouted = true;
            network.SendAll(new NetworkRouteBattleEnemies(mapEventId, enemiesToLeaveFighting: 0));
            return Succeeded($"Ordered the battle authority to route fixture enemies: mapEvent={mapEventId}.");
        }
    }

    // coop.debug.map_event.battle_reward_fixture_state
    /// <summary>Reports contributions and roster reward deltas for the active #2308 fixture.</summary>
    public sealed class BattleRewardFixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_state";

        public string Description => "Reports battle reward fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            var fixture = battleRewardFixture;
            if (fixture == null)
                return Failed("No battle reward fixture is active.");

            TryGetObjectManager(out var objectManager);
            string mapEventId = null;
            objectManager?.TryGetId(fixture.MapEvent, out mapEventId);
            string armyId = null;
            if (fixture.Army != null)
                objectManager?.TryGetId(fixture.Army, out armyId);

            return Succeeded($"Battle reward fixture state: mapEvent={mapEventId ?? "unregistered"}, " +
                   $"finalized={fixture.MapEvent.IsFinalized}, lateJoinerAdded={fixture.LateJoinerAdded}, " +
                   $"reinforcementAdded={fixture.ReinforcementAdded}, partialRoutIssued={fixture.PartialRoutIssued}, " +
                   $"enemiesRouted={fixture.EnemiesRouted}, army={armyId ?? "none"}, " +
                   $"armyLeader={fixture.Army?.LeaderParty?.StringId ?? "none"}, " +
                   $"lateJoinerArmyMember={fixture.Army != null && fixture.LateJoiner.Party.Army == fixture.Army}, " +
                   FormatBattleRewardPlayerState("initiator", fixture.Initiator, fixture.InitiatorMapEventParty) + ", " +
                   FormatBattleRewardPlayerState("lateJoiner", fixture.LateJoiner, fixture.LateJoinerMapEventParty) + ".");
        }
    }

    // coop.debug.map_event.battle_reward_client_state
    /// <summary>Reports the local player's staged or already-applied native battle rewards.</summary>
    public sealed class BattleRewardClientStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_client_state";

        public string Description => "Reports battle reward client state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            var encounter = PlayerEncounter.Current;
            var mainParty = PartyBase.MainParty;
            var mainMobileParty = mainParty?.MobileParty;
            var army = mainMobileParty?.Army;
            string armyId = null;
            if (army != null && ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                objectManager.TryGetId(army, out armyId);
            return Succeeded($"Battle reward client state: encounter={encounter != null}, " +
                   $"encounterState={(encounter == null ? "none" : encounter.EncounterState.ToString())}, " +
                   $"activeState={GameStateManager.Current?.ActiveState?.GetType().Name ?? "none"}, " +
                   $"pendingItems={encounter?.RosterToReceiveLootItems.Sum(element => element.Amount) ?? 0}, " +
                   $"pendingMembers={encounter?.RosterToReceiveLootMembers.TotalManCount ?? 0}, " +
                   $"pendingPrisoners={encounter?.RosterToReceiveLootPrisoners.TotalManCount ?? 0}, " +
                   $"partyItems={mainParty?.ItemRoster.Sum(element => element.Amount) ?? 0}, " +
                   $"partyPrisoners={mainParty?.PrisonRoster.TotalManCount ?? 0}, " +
                   $"army={armyId ?? "none"}, armyLeader={army?.LeaderParty?.StringId ?? "none"}, " +
                   $"isArmyLeader={army != null && army.LeaderParty == mainMobileParty}.");
        }
    }

    // coop.debug.map_event.battle_reward_fixture_restore
    /// <summary>Finalizes the #2308 battle, removes its bandits, and restores both players.</summary>
    public sealed class BattleRewardFixtureRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "battle_reward_fixture_restore";

        public string Description => "Restores or clears battle reward fixture restore.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            var fixture = battleRewardFixture;
            if (fixture == null)
                return Failed("No battle reward fixture is active.");

            if (!TryRestoreBattleRewardFixture(fixture, out var error))
                return Failed($"Fixture restore failed: {error}. Retry the restore command.");

            battleRewardFixture = null;
            return Succeeded($"Battle reward fixture restored: initiator={fixture.Initiator.ControllerId}, " +
                   $"lateJoiner={fixture.LateJoiner.ControllerId}.");
        }
    }

    private static bool TryCreateBattleRewardPlayerSnapshot(
        string controllerId,
        IObjectManager objectManager,
        MobileParty party,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        out BattleRewardPlayerSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero))
        {
            error = $"Unable to resolve player hero for {controllerId}.";
            return false;
        }

        if (hero.PartyBelongedTo != party || party.LeaderHero != hero)
        {
            error = $"Player {controllerId} must be leading their active party.";
            return false;
        }

        if (!behaviorSnapshot.TryCreate(party, out var behavior))
        {
            error = $"Unable to snapshot party behavior for {controllerId}.";
            return false;
        }

        snapshot = new BattleRewardPlayerSnapshot
        {
            ControllerId = controllerId,
            Hero = hero,
            LeaderHero = party.LeaderHero,
            Party = party,
            MemberRoster = party.MemberRoster.GetTroopRoster().ToArray(),
            PrisonRoster = party.PrisonRoster.GetTroopRoster().ToArray(),
            ItemRoster = party.ItemRoster.ToArray(),
            Behavior = behavior,
            HitPoints = hero.HitPoints,
            RecentEventsMorale = party.RecentEventsMorale,
            ClanInfluence = hero.Clan?.Influence ?? 0f,
            ClanKingdom = hero.Clan?.Kingdom,
            ClanLastFactionChangeTime = hero.Clan?.LastFactionChangeTime ?? CampaignTime.Zero,
        };
        return true;
    }

    private static void PrepareBattleRewardPlayer(
        BattleRewardPlayerSnapshot snapshot,
        int totalTroops,
        CampaignVec2 position)
    {
        var heroRoster = snapshot.Party.MemberRoster.GetTroopRoster()
            .Where(element => element.Character.IsHero)
            .ToArray();
        RestoreTroopRoster(snapshot.Party.MemberRoster, heroRoster);
        RestoreTroopRoster(snapshot.Party.PrisonRoster, Array.Empty<TroopRosterElement>());
        snapshot.Party.ItemRoster.Clear();

        if (snapshot.Party.MemberRoster.GetTroopCount(snapshot.Hero.CharacterObject) == 0)
            snapshot.Party.MemberRoster.AddToCounts(snapshot.Hero.CharacterObject, 1, insertAtFront: true);

        var basicTroop = snapshot.Hero.Culture?.BasicTroop;
        if (basicTroop == null)
            throw new InvalidOperationException($"Player {snapshot.ControllerId} has no culture basic troop.");

        var regularTroopCount = totalTroops - snapshot.Party.MemberRoster.TotalManCount;
        if (regularTroopCount < 1)
            throw new InvalidOperationException($"Player {snapshot.ControllerId} has too many heroes for the fixture.");

        snapshot.Party.MemberRoster.AddToCounts(basicTroop, regularTroopCount);
        if (snapshot.Party.LeaderHero != snapshot.Hero)
            snapshot.Party.ChangePartyLeader(snapshot.Hero);
        if (snapshot.Party.LeaderHero != snapshot.Hero)
            throw new InvalidOperationException($"Player {snapshot.ControllerId} could not lead the fixture party.");

        snapshot.Hero.HitPoints = snapshot.Hero.MaxHitPoints;
        snapshot.Party.Position = position;
        snapshot.Party.SetMoveModeHold();
        snapshot.Party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: false,
                resetMovementToHold: true));
    }

    private static string FormatBattleRewardPlayerState(
        string role,
        BattleRewardPlayerSnapshot snapshot,
        MapEventParty mapEventParty)
    {
        return $"{role}Controller={snapshot.ControllerId}, {role}Party={snapshot.Party.StringId}, " +
               $"{role}Contribution={mapEventParty?.ContributionToBattle ?? 0}, " +
               $"{role}ItemsDelta={snapshot.Party.ItemRoster.Sum(element => element.Amount)}, " +
               $"{role}PrisonersDelta={snapshot.Party.PrisonRoster.TotalManCount}, " +
               $"{role}MapEvent={(snapshot.Party.MapEvent == null ? "none" : "attached")}";
    }

    private static bool TryRestoreBattleRewardFixture(BattleRewardFixture fixture, out string error)
    {
        try
        {
            if (fixture.MapEvent != null && !fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (fixture.Army != null && fixture.Initiator.Party.Army == fixture.Army)
                DisbandArmyAction.ApplyInternal(fixture.Army, Army.ArmyDispersionReason.NotEnoughParty);
            if (fixture.Army != null && fixture.LateJoiner.Party.Army == fixture.Army)
                fixture.LateJoiner.Party.Army = null;

            if (fixture.BanditParty?.IsActive == true && fixture.BanditParty.MapEvent == null)
                DestroyPartyAction.Apply(null, fixture.BanditParty);
            if (fixture.ReinforcementParty?.IsActive == true && fixture.ReinforcementParty.MapEvent == null)
                DestroyPartyAction.Apply(null, fixture.ReinforcementParty);

            if (fixture.InitiatorKingdomChanged)
                RestoreBattleRewardClanKingdom(fixture.Initiator);

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                throw new InvalidOperationException("Unable to resolve the mobile-party behavior snapshot service.");

            RestoreBattleRewardPlayer(fixture.Initiator, behaviorSnapshot);
            RestoreBattleRewardPlayer(fixture.LateJoiner, behaviorSnapshot);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore battle reward fixture");
            error = e.Message;
            return false;
        }
    }

    private static void RestoreBattleRewardClanKingdom(BattleRewardPlayerSnapshot snapshot)
    {
        var clan = snapshot.Hero.Clan;
        if (clan == null)
            throw new InvalidOperationException($"Unable to restore the clan kingdom for {snapshot.ControllerId}.");
        if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
            throw new InvalidOperationException("Unable to resolve the kingdom membership state service.");

        kingdomMembershipState.MoveClanToKingdom(
            clan.Kingdom,
            snapshot.ClanKingdom,
            clan,
            publishCollectionChanges: true,
            republishExistingCollections: true);
        clan.LastFactionChangeTime = snapshot.ClanLastFactionChangeTime;
    }

    private static void RestoreBattleRewardPlayer(
        BattleRewardPlayerSnapshot snapshot,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        RestoreTroopRoster(snapshot.Party.MemberRoster, snapshot.MemberRoster);
        RestoreTroopRoster(snapshot.Party.PrisonRoster, snapshot.PrisonRoster);
        snapshot.Party.ItemRoster.Clear();
        foreach (var element in snapshot.ItemRoster)
            snapshot.Party.ItemRoster.Add(element);

        if (snapshot.Party.LeaderHero != snapshot.LeaderHero)
            snapshot.Party.ChangePartyLeader(snapshot.LeaderHero);

        snapshot.Hero.HitPoints = snapshot.HitPoints;
        snapshot.Party.RecentEventsMorale = snapshot.RecentEventsMorale;
        var clan = snapshot.Hero.Clan;
        if (clan != null)
        {
            var influenceDelta = snapshot.ClanInfluence - clan.Influence;
            if (Math.Abs(influenceDelta) > 0.001f)
                ChangeClanInfluenceAction.Apply(clan, influenceDelta);
        }
        snapshot.Party.Position = snapshot.Behavior.PartyPosition;
        snapshot.Party.IsCurrentlyAtSea = snapshot.Behavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            throw new InvalidOperationException($"Unable to restore party behavior for {snapshot.ControllerId}.");

        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: snapshot.Party.IsCurrentlyAtSea,
                resetMovementToHold: false));
    }

    private static void RestoreTroopRoster(TroopRoster roster, TroopRosterElement[] elements)
    {
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }

        foreach (var element in elements)
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
    }

    // coop.debug.map_event.wounded_allied_fixture_start PlayerOne
    /// <summary>Creates the wounded, troop-less player plus healthy allied force field encounter from #2097.</summary>
    public sealed class WoundedAlliedFixtureStartCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "wounded_allied_fixture_start";

        public string Description => "Runs the wounded allied fixture start debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (woundedAlliedFixture != null)
                return Failed($"Fixture already active for {woundedAlliedFixture.ControllerId}.");

            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
                return Failed(error);

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !playerManager.TryGetPlayer(args[0], out var player) ||
                !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
            {
                return Failed($"Unable to resolve player hero for {args[0]}.");
            }

            if (!ContainerProvider.TryResolve<INetwork>(out var network))
                return Failed("Unable to resolve network.");

            if (playerParty.PartyMoveMode != MoveModeType.Hold)
                return Failed($"Player party {playerParty.StringId} must be holding before the fixture starts.");

            var playerPosition = playerParty.Position.ToVec2();
            var banditParty = MobileParty.All
                .Where(p => p.IsActive && p.IsBandit && p.MapEvent == null && p.CurrentSettlement == null &&
                            p.MemberRoster.TotalHealthyCount > 0)
                .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
                .FirstOrDefault();
            if (banditParty == null)
                return Failed("No active healthy bandit party is available.");

            var alliedParty = MobileParty.All
                .Where(p => p.IsActive && !p.IsBandit && !p.IsPlayerParty() && p != playerParty &&
                            p.MapEvent == null && p.CurrentSettlement == null && p.MemberRoster.TotalHealthyCount > 0 &&
                            p.MapFaction != null &&
                            !VillageHostileFactionStanceHelper.HasWarStance(playerParty.MapFaction, p.MapFaction) &&
                            VillageHostileFactionStanceHelper.HasWarStance(banditParty.MapFaction, p.MapFaction))
                .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
                .FirstOrDefault();
            if (alliedParty == null)
                return Failed("No active healthy AI party is available for the allied side.");

            var fixture = new WoundedAlliedFixture
            {
                ControllerId = args[0],
                PlayerHero = playerHero,
                PlayerParty = playerParty,
                OriginalHitPoints = playerHero.HitPoints,
                OriginalRecentEventsMorale = playerParty.RecentEventsMorale,
                OriginalRoster = playerParty.MemberRoster.GetTroopRoster().ToArray(),
                OriginalPosition = playerParty.Position,
            };

            try
            {
                playerHero.HitPoints = 1;
                RemoveHealthyPlayerTroops(fixture);
                playerParty.RecentEventsMorale = -1000f;

                fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                    banditParty.Party,
                    playerParty.Party,
                    default);
                if (fixture.MapEvent == null)
                    throw new InvalidOperationException("The bandit encounter did not create a map event.");

                alliedParty.Party.MapEventSide = playerParty.Party.MapEventSide;
                fixture.InvolvedParties = fixture.MapEvent.InvolvedParties.ToArray();

                if (!objectManager.TryGetId(banditParty.Party, out string banditPartyId) ||
                    !objectManager.TryGetId(playerParty.Party, out string playerPartyId) ||
                    !objectManager.TryGetId(fixture.MapEvent, out string fixtureMapEventId))
                {
                    throw new InvalidOperationException("Unable to resolve the fixture's network ids.");
                }

                network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                    $"debug-2097-{Guid.NewGuid():N}",
                    banditPartyId,
                    playerPartyId,
                    fixtureMapEventId));
                woundedAlliedFixture = fixture;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to create wounded allied force fixture");
                woundedAlliedFixture = fixture;
                if (TryRestoreWoundedAlliedFixture(fixture, out var restoreError))
                    woundedAlliedFixture = null;
                else
                    return Failed($"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.");

                return Failed($"Fixture setup failed: {e.Message}");
            }

            objectManager.TryGetId(fixture.MapEvent, out string mapEventId);
            return Succeeded($"Wounded allied fixture started: controller={args[0]}, mapEvent={mapEventId}, " +
                   $"playerHealthy={playerParty.Party.NumberOfHealthyMembers}, alliedParty={alliedParty.StringId}, " +
                   $"alliedHealthy={alliedParty.Party.NumberOfHealthyMembers}, banditParty={banditParty.StringId}.");
        }
    }

    // coop.debug.map_event.wounded_allied_fixture_state PlayerOne
    /// <summary>Reports the #2097 fixture state and the local patched order-attack option when applicable.</summary>
    public sealed class WoundedAlliedFixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "wounded_allied_fixture_state";

        public string Description => "Reports wounded allied fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
                return Failed(error);

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !playerManager.TryGetPlayer(args[0], out var player) ||
                !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
            {
                return Failed($"Unable to resolve player hero for {args[0]}.");
            }

            var mapEvent = playerParty.MapEvent;
            var side = playerParty.Party.MapEventSide;
            var alliedHealthy = side?.Parties
                .Where(p => p.Party != playerParty.Party)
                .Sum(p => p.Party.NumberOfHealthyMembers) ?? 0;

            var option = "not-local";
            if (ModInformation.IsClient && playerParty == MobileParty.MainParty && PlayerEncounter.Current != null)
            {
                var callbackArgs = new MenuCallbackArgs((MenuContext)null, null);
                var shown = new EncounterGameMenuBehavior()
                    .game_menu_encounter_order_attack_on_condition(callbackArgs);
                var renderedOption = Campaign.Current?.CurrentMenuContext?.GameMenu?.MenuOptions
                    .FirstOrDefault(menuOption => menuOption.IdString == "str_order_attack");
                option = $"conditionShown={shown},conditionEnabled={callbackArgs.IsEnabled}," +
                         $"leaveType={callbackArgs.optionLeaveType},renderedRegistered={renderedOption != null}," +
                         $"renderedEnabled={renderedOption?.IsEnabled ?? false}";
            }

            objectManager.TryGetId(mapEvent, out string mapEventId);
            return Succeeded($"Wounded allied fixture state: controller={args[0]}, local={playerParty == MobileParty.MainParty}, " +
                   $"hitPoints={playerHero.HitPoints}, wounded={playerHero.IsWounded}, " +
                   $"roster={playerParty.MemberRoster.TotalManCount}, playerHealthy={playerParty.Party.NumberOfHealthyMembers}, " +
                   $"morale={playerParty.Morale:0.##}, recentEventsMorale={playerParty.RecentEventsMorale:0.##}, " +
                   $"position={playerParty.Position.X:R}|{playerParty.Position.Y:R}, moveMode={playerParty.PartyMoveMode}, " +
                   $"alliedHealthy={alliedHealthy}, mapEvent={mapEventId ?? "none"}, " +
                   $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}, option={option}.");
        }
    }

    // coop.debug.map_event.wounded_allied_fixture_restore PlayerOne
    /// <summary>Finalizes the #2097 fixture and restores the player's original hero, morale, and roster state.</summary>
    public sealed class WoundedAlliedFixtureRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "wounded_allied_fixture_restore";

        public string Description => "Restores or clears wounded allied fixture restore.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (woundedAlliedFixture == null || woundedAlliedFixture.ControllerId != args[0])
                return Failed($"No active fixture exists for {args[0]}.");

            var fixture = woundedAlliedFixture;
            if (!TryRestoreWoundedAlliedFixture(fixture, out var error))
                return Failed($"Fixture restore failed: {error}. Retry the restore command.");

            woundedAlliedFixture = null;

            return Succeeded($"Wounded allied fixture restored: controller={args[0]}, hitPoints={fixture.PlayerHero.HitPoints}, " +
                   $"roster={fixture.PlayerParty.MemberRoster.TotalManCount}.");
        }
    }

    private static void RemoveHealthyPlayerTroops(WoundedAlliedFixture fixture)
    {
        var roster = fixture.PlayerParty.MemberRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character == fixture.PlayerHero.CharacterObject)
            {
                var woundedToAdd = element.Number - element.WoundedNumber;
                if (woundedToAdd > 0)
                    roster.AddToCounts(element.Character, 0, false, woundedToAdd);
                continue;
            }

            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }
    }

    private static void RestoreWoundedAlliedFixture(WoundedAlliedFixture fixture)
    {
        if (fixture.MapEvent != null)
        {
            if (!fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (HasAttachedFixtureParties(fixture))
                RecoverPartiallyFinalizedMapEvent(fixture);
        }

        fixture.PlayerHero.HitPoints = fixture.OriginalHitPoints;
        fixture.PlayerParty.RecentEventsMorale = fixture.OriginalRecentEventsMorale;
        fixture.PlayerParty.Position = fixture.OriginalPosition;
        fixture.PlayerParty.SetMoveModeHold();
        fixture.PlayerParty.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(MapEventDebugCommands),
            new PartyBehaviorChangeAttempted(
                fixture.PlayerParty,
                forcePosition: true,
                isCurrentlyAtSea: fixture.PlayerParty.IsCurrentlyAtSea,
                resetMovementToHold: true));

        var roster = fixture.PlayerParty.MemberRoster;
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            var element = roster.GetElementCopyAtIndex(i);
            roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
        }

        foreach (var element in fixture.OriginalRoster)
        {
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
        }
    }

    private static bool HasAttachedFixtureParties(WoundedAlliedFixture fixture) =>
        HasAttachedParties(fixture.MapEvent, fixture.InvolvedParties);

    private static bool HasAttachedParties(MapEvent mapEvent, PartyBase[] involvedParties) =>
        mapEvent != null &&
        (involvedParties?.Any(p => p?._mapEventSide?.MapEvent == mapEvent) == true ||
         mapEvent.AttackerSide?.Parties.Count > 0 ||
         mapEvent.DefenderSide?.Parties.Count > 0);

    private static void RecoverPartiallyFinalizedMapEvent(WoundedAlliedFixture fixture)
    {
        RecoverPartiallyFinalizedMapEvent(fixture.MapEvent, fixture.InvolvedParties);
    }

    private static void RecoverPartiallyFinalizedMapEvent(MapEvent mapEvent, PartyBase[] involvedParties)
    {
        foreach (var party in involvedParties ?? Array.Empty<PartyBase>())
        {
            if (party?._mapEventSide?.MapEvent != mapEvent) continue;

            party._mapEventSide = null;
            if (party.MobileParty != null)
                party.MobileParty.EventPositionAdder = TaleWorlds.Library.Vec2.Zero;
            party.SetVisualAsDirty();
        }

        mapEvent.AttackerSide?.Clear();
        mapEvent.DefenderSide?.Clear();
        if (HasAttachedParties(mapEvent, involvedParties))
            throw new InvalidOperationException("The partially finalized fixture still has attached parties.");

        MessageBroker.Instance.Publish(mapEvent, new MapEventFinalized(mapEvent));
        MessageBroker.Instance.Publish(mapEvent, new InstanceDestroyed<MapEvent>(mapEvent));
    }

    private static bool TryRestoreWoundedAlliedFixture(WoundedAlliedFixture fixture, out string error)
    {
        try
        {
            RestoreWoundedAlliedFixture(fixture);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to restore wounded allied force fixture");
            error = e.Message;
            return false;
        }
    }

    public sealed class LeaveSettlementCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "leave_settlement";

        public string Description => "Runs the leave settlement debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");


            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                return Failed("Unable to resolve PlayerManager");

            if (!playerManager.TryGetPlayer(args[0], out var player))
                return Failed($"No registered player has controller id {args[0]}.");

            if (!playerManager.IsConnected(player))
                return Failed($"Player {args[0]} is not connected.");

            if (!TryGetObjectManager(out var objectManager) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
                return Failed($"Unable to resolve player party {player.MobilePartyId}.");

            var settlement = playerParty.CurrentSettlement;
            if (settlement == null)
                return Failed($"Player {args[0]} is already outside a settlement.");

            LeaveSettlementAction.ApplyForParty(playerParty);
            return Succeeded($"Moved player {args[0]} out of {settlement.Name}.");
        }
    }

    public sealed class FinishCurrentEncounterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "finish_current_encounter";

        public string Description => "Runs the finish current encounter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (PlayerEncounter.Current == null)
                return Failed("No active encounter.");

            PlayerEncounter.Finish();
            return Succeeded("Finished the current local encounter.");
        }
    }

    public sealed class EnterCurrentBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "enter_current_battle";

        public string Description => "Runs the enter current battle debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (PlayerEncounter.Current == null)
                return Failed("No active encounter.");

            if (PlayerEncounter.Battle == null)
            {
                if (PlayerEncounter.StartBattle() == null)
                    return Failed("Unable to start the current battle.");

                GameMenu.SwitchToMenu("encounter");
            }

            CoopCommandResult startResult = StartAttackMissionCore(args);
            if (!startResult.Succeeded)
                return startResult;

            return Succeeded("Requested entry into the current battle.");
        }
    }

    // coop.debug.map_event.finish_player_encounter PlayerOne
    /// <summary>
    /// Closes the connected player's encounter through the existing authoritative leave path.
    /// </summary>
    public sealed class FinishPlayerEncounterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "finish_player_encounter";

        public string Description => "Runs the finish player encounter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Run this command on the server.");
            }


            if (!TryGetPlayerParty(
                    args[0],
                    requireReady: true,
                    out var objectManager,
                    out var playerParty,
                    out var error,
                    allowActiveMapEvent: true))
            {
                return Failed(error);
            }

            if (!objectManager.TryGetIdWithLogging(playerParty.Party, out var partyBaseId))
            {
                return Failed($"Unable to resolve PartyBase for player {args[0]}.");
            }

            MessageBroker.Instance.Publish(
                playerParty.Party,
                new PlayerLeaveBattleAttempted(playerParty.Party));
            return Succeeded($"Requested encounter finish for player {args[0]} (PartyBase id {partyBaseId}).");
        }
    }

    // coop.debug.map_event.conversation_hold_state <partyBaseId>
    /// <summary>
    /// Reports whether the server currently holds an AI PartyBase for a conversation.
    /// </summary>
    public sealed class ConversationHoldStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "conversation_hold_state";

        public string Description => "Reports conversation hold state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_base_id", "The party base id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Run this command on the server.");
            }


            var held = ConversationPartyTracker.Instance?.TryGetEngagement(args[0], out _) == true;
            return Succeeded($"Conversation hold for PartyBase id {args[0]}: {(held ? "held" : "released")}.");
        }
    }

    // coop.debug.map_event.late_join_mode_fixture PlayerOne PlayerTwo
    /// <summary>
    /// Creates a server-authoritative battle, claims mission mode before the second player joins, then routes the
    /// second player's join through the real request handler.
    /// </summary>
    public sealed class LateJoinModeFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_fixture";

        public string Description => "Runs the late join mode fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("first_controller_id", "The first controller id.", true),
            new ExpectedArgs("joining_controller_id", "The joining controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Run this command on the server.");
            }


            if (lateJoinModeFixture != null)
            {
                return Failed($"A late-join mode fixture is already active for map event {lateJoinModeFixture.MapEventId}.");
            }

            if (args[0] == args[1])
            {
                return Failed("The fixture requires two different connected players.");
            }

            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var firstParty, out var error))
            {
                return Failed(error);
            }

            if (!TryGetPlayerParty(args[1], requireReady: true, out _, out var joiningParty, out error))
            {
                return Failed(error);
            }

            if (firstParty.CurrentSettlement != null || joiningParty.CurrentSettlement != null)
            {
                return Failed("Both players must be on the campaign map, outside settlements.");
            }

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !playerManager.TryGetPeer(args[0], out var firstPeer) ||
                !playerManager.TryGetPeer(args[1], out _))
            {
                return Failed("Unable to resolve both connected player peers.");
            }

            if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
                !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            {
                return Failed("Unable to resolve the late-join mode fixture services.");
            }

            if (!behaviorSnapshot.TryCreate(firstParty, out var firstPlayerBehavior) ||
                !behaviorSnapshot.TryCreate(joiningParty, out var joiningPlayerBehavior))
            {
                return Failed("Unable to capture both players' original movement state.");
            }

            var firstFaction = firstParty.MapFaction?.MapFaction ?? firstParty.MapFaction;
            var joiningFaction = joiningParty.MapFaction?.MapFaction ?? joiningParty.MapFaction;
            var firstPosition = firstParty.Position.ToVec2();
            var opponentParty = MobileParty.All
                .Where(p => p.IsActive && p.IsBandit && p.MapEvent == null && p.CurrentSettlement == null &&
                            p.MemberRoster.TotalHealthyCount > 0 && p.MapFaction != null &&
                            VillageHostileFactionStanceHelper.HasWarStance(firstFaction, p.MapFaction) &&
                            VillageHostileFactionStanceHelper.HasWarStance(joiningFaction, p.MapFaction))
                .OrderBy(p => p.Position.ToVec2().DistanceSquared(firstPosition))
                .FirstOrDefault();

            if (opponentParty == null)
            {
                return Failed("No active healthy bandit party hostile to both players was found.");
            }

            if (!behaviorSnapshot.TryCreate(opponentParty, out var opponentBehavior))
            {
                return Failed($"Unable to capture the opponent movement state for {opponentParty.Name}.");
            }

            if (!objectManager.TryGetId(firstParty.Party, out string firstPartyId) ||
                !objectManager.TryGetId(firstParty, out string firstMobilePartyId) ||
                !objectManager.TryGetId(joiningParty.Party, out string joiningPartyId) ||
                !objectManager.TryGetId(joiningParty, out string joiningMobilePartyId) ||
                !objectManager.TryGetId(opponentParty, out string opponentMobilePartyId))
            {
                return Failed("Unable to resolve fixture party ids.");
            }

            var mapEvent = MapEventBattleFactory.CreateMapEvent(firstParty.Party, opponentParty.Party, default);
            if (mapEvent == null || !objectManager.TryGetId(mapEvent, out string mapEventId))
            {
                if (mapEvent != null && !mapEvent.IsFinalized)
                    mapEvent.FinalizeEvent();

                RestorePartyBehavior(firstParty, firstPlayerBehavior, behaviorSnapshot);
                RestorePartyBehavior(joiningParty, joiningPlayerBehavior, behaviorSnapshot);
                RestorePartyBehavior(opponentParty, opponentBehavior, behaviorSnapshot);
                return Failed("Unable to create or resolve the fixture map event.");
            }

            lateJoinModeFixture = new LateJoinModeFixture
            {
                MapEventId = mapEventId,
                FirstControllerId = args[0],
                FirstPlayerPartyId = firstPartyId,
                FirstPlayerMobilePartyId = firstMobilePartyId,
                FirstPlayerBehavior = firstPlayerBehavior,
                JoiningControllerId = args[1],
                JoiningPlayerPartyId = joiningPartyId,
                JoiningPlayerMobilePartyId = joiningMobilePartyId,
                JoiningPlayerBehavior = joiningPlayerBehavior,
                OpponentMobilePartyId = opponentMobilePartyId,
                OpponentBehavior = opponentBehavior,
            };

            var hasFieldBattleOpponent = mapEvent.EventType == MapEvent.BattleTypes.FieldBattle &&
                                         mapEvent.MapEventSettlement == null &&
                                         mapEvent.DefenderSide?.Parties.Any(
                                             p => p.Party == opponentParty.Party) == true;
            if (!hasFieldBattleOpponent)
            {
                CleanupLateJoinModeFixture(messageBroker, behaviorSnapshot, objectManager);
                return Failed($"Late-join fixture {mapEventId} did not create the required field battle.");
            }

            // Route the first player's Attack through the real server handler. The resulting mission-start and mode
            // broadcasts reach PlayerTwo before its party belongs to the event, reproducing the missed-claim timing.
            messageBroker.Publish(firstPeer, new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                mapEventId,
                firstMobilePartyId));

            return Succeeded($"Late-join field-battle fixture created and first mission requested: mapEvent={mapEventId}, " +
                   $"eventType={mapEvent.EventType}, opponent={opponentParty.Name} ({opponentParty.StringId}), " +
                   $"firstPlayer={args[0]}, joiningPlayer={args[1]}, firstSide=Attacker.");
        }
    }

    // coop.debug.map_event.late_join_mode_join
    /// <summary>Routes the waiting player's attacker-side join after the first player has entered the mission.</summary>
    public sealed class LateJoinModeJoinCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_join";

        public string Description => "Runs the late join mode join debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            var fixture = lateJoinModeFixture;
            if (fixture == null)
                return Failed("No late-join mode fixture is active.");
            if (fixture.JoiningPartyJoined)
                return Failed($"Player {fixture.JoiningControllerId} already joined fixture map event {fixture.MapEventId}.");

            if (!TryGetObjectManager(out var objectManager) ||
                !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) ||
                !playerManager.TryGetPeer(fixture.JoiningControllerId, out var joiningPeer))
            {
                return Failed("Unable to resolve the late-join fixture services.");
            }

            if (!missionMembership.IsControllerInMission(fixture.FirstControllerId))
                return Failed($"Player {fixture.FirstControllerId} has not entered the field battle mission.");
            if (missionMembership.IsControllerInMission(fixture.JoiningControllerId))
                return Failed($"Player {fixture.JoiningControllerId} is already in a mission.");
            if (!ServerBattleModeArbiter.TryGetMode(fixture.MapEventId, out var mode) ||
                mode != BattleStartMode.Mission)
            {
                return Failed($"Fixture map event {fixture.MapEventId} is not claimed for Mission mode.");
            }
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(fixture.MapEventId, out var mapEvent) ||
                !objectManager.TryGetObjectWithLogging<PartyBase>(fixture.JoiningPlayerPartyId, out var joiningParty))
            {
                return Failed("Unable to resolve the fixture map event or joining party.");
            }

            messageBroker.Publish(joiningPeer, new NetworkRequestJoinBattle(
                Guid.NewGuid().ToString(),
                fixture.MapEventId,
                fixture.JoiningPlayerPartyId,
                BattleSideEnum.Attacker));

            if (joiningParty.MapEvent != mapEvent)
                return Failed($"Player {fixture.JoiningControllerId} did not join fixture map event {fixture.MapEventId}.");

            fixture.JoiningPartyJoined = true;
            return Succeeded($"Late join accepted: mapEvent={fixture.MapEventId}, joiningPlayer={fixture.JoiningControllerId}, " +
                   "side=Attacker, replayedMode=Mission, firstPlayerInMission=True, joiningPlayerInMission=False.");
        }
    }

    // coop.debug.map_event.late_join_mode_enter
    /// <summary>Routes the late joiner's Attack request through the real mission-start handler.</summary>
    public sealed class LateJoinModeEnterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_enter";

        public string Description => "Runs the late join mode enter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            var fixture = lateJoinModeFixture;
            if (fixture == null)
                return Failed("No late-join mode fixture is active.");
            if (!fixture.JoiningPartyJoined)
                return Failed($"Player {fixture.JoiningControllerId} has not joined fixture map event {fixture.MapEventId}.");

            if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
                !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) ||
                !playerManager.TryGetPeer(fixture.JoiningControllerId, out var joiningPeer))
            {
                return Failed("Unable to resolve the late-join mission-entry services.");
            }

            if (!missionMembership.IsControllerInMission(fixture.FirstControllerId))
                return Failed($"Player {fixture.FirstControllerId} is no longer in the field battle mission.");
            if (missionMembership.IsControllerInMission(fixture.JoiningControllerId))
                return Failed($"Player {fixture.JoiningControllerId} already entered the field battle mission.");

            messageBroker.Publish(joiningPeer, new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                fixture.MapEventId,
                fixture.JoiningPlayerMobilePartyId));

            return Succeeded($"Late joiner mission requested: mapEvent={fixture.MapEventId}, " +
                   $"joiningPlayer={fixture.JoiningControllerId}, mode=Mission.");
        }
    }

#if DEBUG
    // coop.debug.map_event.late_join_mode_begin_field_battle
    /// <summary>Finishes the local deployment phase so live evidence shows the active field battle.</summary>
    public sealed class LateJoinModeBeginFieldBattleCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_begin_field_battle";

        public string Description => "Runs the late join mode begin field battle debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            var mission = Mission.Current;
            if (mission == null)
                return Failed("No mission is active.");

            var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
            if (deploymentController?.TeamSetupOver != true)
                return Failed("Local deployment is not ready.");

            var deploymentHandler = mission.GetMissionBehavior<DeploymentHandler>();
            if (deploymentHandler == null)
                return Failed("The field battle is already active.");

            mission.DisableDying = true;
            deploymentHandler.FinishDeployment();
            if (!ProtectLateJoinModeFixturePlayer(mission))
                return Failed("Local deployment finished, but the local player agent was not assigned.");

            return Succeeded("Local deployment finished; the field battle is active and the local player is protected.");
        }
    }

    // coop.debug.map_event.late_join_mode_disable_dying
    /// <summary>Prevents the live-test battle from resolving before both client views are captured.</summary>
    public sealed class LateJoinModeDisableDyingCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_disable_dying";

        public string Description => "Runs the late join mode disable dying debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            var mission = Mission.Current;
            if (mission == null)
                return Failed("No mission is active.");

            mission.DisableDying = true;
            var playerProtected = ProtectLateJoinModeFixturePlayer(mission);
            return Failed(playerProtected
                ? "Dying disabled for the local fixture mission; the local player is protected."
                : "Dying disabled for the local fixture mission; the local player is not assigned yet.");
        }
    }

    private static bool ProtectLateJoinModeFixturePlayer(Mission mission)
    {
        var mainAgent = mission.MainAgent;
        if (mainAgent == null)
            return false;

        mainAgent.SetMortalityState(Agent.MortalityState.Immortal);
        mainAgent.Health = mainAgent.HealthLimit;
        return true;
    }

    // coop.debug.map_event.late_join_mode_exit_missions
    /// <summary>Asks every fixture mission member to return to campaign before authoritative cleanup.</summary>
    public sealed class LateJoinModeExitMissionsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_exit_missions";

        public string Description => "Runs the late join mode exit missions debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return ExitLateJoinModeFixtureMissionsCore(args);
        }
    }

    private static CoopCommandResult ExitLateJoinModeFixtureMissionsCore(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient)
            return Failed("Run this command on the server.");

        var fixture = lateJoinModeFixture;
        if (fixture == null)
            return Failed("No late-join mode fixture is active.");

        if (!ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership))
        {
            return Failed("Unable to resolve the late-join mission-exit services.");
        }

        var requested = 0;
        foreach (var controllerId in new[] { fixture.FirstControllerId, fixture.JoiningControllerId })
        {
            if (!missionMembership.IsControllerInMission(controllerId) ||
                !playerManager.TryGetPeer(controllerId, out var peer))
                continue;

            network.Send(peer, new NetworkEndLateJoinModeFixtureMission(fixture.MapEventId));
            requested++;
        }

        return Succeeded($"Late-join fixture mission exit requested for {requested} player(s).");


    }

    /// <summary>Returns both fixture clients to campaign and restores the server-side battle state.</summary>
    public sealed class LateJoinModeRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_restore";

        public string Description => "Restores or clears late join mode restore.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            CoopCommandResult exitResult = ExitLateJoinModeFixtureMissionsCore(args);
            if (!exitResult.Succeeded || lateJoinModeFixture == null)
                return exitResult;

            CoopCommandResult cleanupResult = CleanupLateJoinModeFixtureCore(args);
            if (!cleanupResult.Succeeded)
                return cleanupResult;

            return Succeeded($"{exitResult.Output} {cleanupResult.Output}");
        }
    }
#endif

    // coop.debug.map_event.late_join_mode_state PlayerTwo
    /// <summary>Reports a player's map-event membership and known authoritative battle mode.</summary>
    public sealed class LateJoinModeStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_state";

        public string Description => "Reports late join mode state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
            {
                return Failed(error);
            }

            var mapEvent = playerParty.MapEvent;
            var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out string resolvedId)
                ? resolvedId
                : "none";
            var eventType = mapEvent?.EventType.ToString() ?? "none";
            var settlement = mapEvent?.MapEventSettlement;
            var settlementName = settlement != null ? $"{settlement.Name} ({settlement.StringId})" : "none";
            var opponentParties = mapEvent?.DefenderSide?.Parties.Count ?? 0;
            var side = playerParty.MapEventSide?.MissionSide.ToString() ?? "none";
            var mode = "Unclaimed";
            if (mapEventId != "none")
            {
                if (ModInformation.IsServer && ServerBattleModeArbiter.TryGetMode(mapEventId, out var serverMode))
                    mode = serverMode.ToString();
                else if (BattleModeRegistry.IsMission(mapEventId))
                    mode = BattleStartMode.Mission.ToString();
                else if (BattleModeRegistry.IsSimulation(mapEventId))
                    mode = BattleStartMode.Simulation.ToString();
            }

            var missionActive = ModInformation.IsServer
                ? ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var missionMembership) &&
                  missionMembership.IsControllerInMission(args[0])
                : MissionState.Current != null || Mission.Current != null;
            var missionAgents = ModInformation.IsClient && Mission.Current != null
                ? Mission.Current.Agents.Count
                : 0;
            var deploymentActive = ModInformation.IsClient &&
                                   Mission.Current?.HasMissionBehavior<DeploymentHandler>() == true;

            return Succeeded($"Late-join mode state: controller={args[0]}, mapEvent={mapEventId}, eventType={eventType}, " +
                   $"settlement={settlementName}, opponentParties={opponentParties}, side={side}, mode={mode}, " +
                   $"missionActive={missionActive}, missionAgents={missionAgents}, deploymentActive={deploymentActive}.");
        }
    }

    // coop.debug.map_event.late_join_mode_cleanup
    /// <summary>Removes the fixture field battle and restores each party's movement state.</summary>
    public sealed class LateJoinModeCleanupCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "late_join_mode_cleanup";

        public string Description => "Runs the late join mode cleanup debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return CleanupLateJoinModeFixtureCore(args);
        }
    }

    private static CoopCommandResult CleanupLateJoinModeFixtureCore(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient)
        {
            return Failed("Run this command on the server.");
        }


        if (lateJoinModeFixture == null)
        {
            return Failed("No late-join mode fixture is active.");
        }

        if (!TryGetObjectManager(out var objectManager) ||
            !ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            return Failed("Unable to resolve the late-join mode cleanup services.");
        }

        var mapEventId = lateJoinModeFixture.MapEventId;
        var restored = CleanupLateJoinModeFixture(messageBroker, behaviorSnapshot, objectManager);
        return Succeeded(restored
            ? $"Late-join field-battle fixture {mapEventId} cleaned up and party movement restored."
            : $"Late-join field-battle fixture {mapEventId} cleaned up, but its original state could not be fully restored.");


    }

    private static bool CleanupLateJoinModeFixture(
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IObjectManager objectManager)
    {
        var fixture = lateJoinModeFixture;
        if (fixture == null) return true;

        messageBroker.Publish(typeof(MapEventDebugCommands), new NetworkRequestLeaveBattle(fixture.JoiningPlayerPartyId));
        messageBroker.Publish(typeof(MapEventDebugCommands), new NetworkRequestLeaveBattle(fixture.FirstPlayerPartyId));
        if (objectManager.TryGetObject<MapEvent>(fixture.MapEventId, out var mapEvent) && !mapEvent.IsFinalized)
            mapEvent.FinalizeEvent();
        ServerBattleModeArbiter.Release(fixture.MapEventId);

        var restored = RestorePartyBehavior(
            fixture.FirstPlayerMobilePartyId,
            fixture.FirstPlayerBehavior,
            behaviorSnapshot,
            objectManager);
        restored = RestorePartyBehavior(
            fixture.JoiningPlayerMobilePartyId,
            fixture.JoiningPlayerBehavior,
            behaviorSnapshot,
            objectManager) && restored;
        restored = RestorePartyBehavior(
            fixture.OpponentMobilePartyId,
            fixture.OpponentBehavior,
            behaviorSnapshot,
            objectManager) && restored;

        lateJoinModeFixture = null;
        return restored;
    }

    private static bool RestorePartyBehavior(
        string mobilePartyId,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        IObjectManager objectManager)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(mobilePartyId, out var mobileParty))
            return false;

        return RestorePartyBehavior(mobileParty, behavior, behaviorSnapshot);
    }

    private static bool RestorePartyBehavior(
        MobileParty mobileParty,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        mobileParty.Position = behavior.PartyPosition;
        return behaviorSnapshot.TryApply(mobileParty, behavior, out _);
    }

    // coop.debug.map_event.peace_pursuit_fixture PlayerOne
    /// <summary>
    /// Finds a neutral AI party that can be used without changing its original movement state.
    /// </summary>
    public sealed class PeacePursuitFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "peace_pursuit_fixture";

        public string Description => "Runs the peace pursuit fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Run this command on the server.");
            }


            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
            {
                return Failed(error);
            }

            var neutralParty = FindPeacePursuitFixture(playerParty);
            if (neutralParty == null)
            {
                return Failed("No active neutral AI party already holding on the map.");
            }

            return Succeeded(FormatPeacePursuitState("Peace pursuit fixture", objectManager, neutralParty, playerParty));
        }
    }

    // coop.debug.map_event.peace_pursuit_state PlayerOne mobileParty_1
    /// <summary>
    /// Reports the pursuit-test party state on the current machine.
    /// </summary>
    public sealed class PeacePursuitStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "peace_pursuit_state";

        public string Description => "Reports peace pursuit state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
            new ExpectedArgs("party_string_id", "The party string id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var playerParty, out var error))
            {
                return Failed(error);
            }

            var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
            if (neutralParty == null)
            {
                return Failed($"Party {args[1]} was not found.");
            }

            return Succeeded(FormatPeacePursuitState("Peace pursuit state", objectManager, neutralParty, playerParty));
        }
    }

    // coop.debug.map_event.test_peace_stops_pursuit PlayerOne mobileParty_1
    /// <summary>
    /// Makes a selected neutral AI party pursue a connected player, then makes peace.
    /// </summary>
    public sealed class TestPeaceStopsPursuitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "test_peace_stops_pursuit";

        public string Description => "Runs the test peace stops pursuit debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The controller id.", true),
            new ExpectedArgs("party_string_id", "The party string id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
            {
                return Failed("Run this command on the server.");
            }


            if (!TryGetPlayerParty(args[0], requireReady: true, out var objectManager, out var playerParty, out var error))
            {
                return Failed(error);
            }

            var neutralParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[1]);
            if (neutralParty == null)
            {
                return Failed($"Party {args[1]} was not found.");
            }

            if (!IsPeacePursuitFixture(neutralParty, playerParty))
            {
                return Failed($"Party {args[1]} is not a neutral AI party already holding on the map.");
            }

            DeclareWarAction.ApplyByDefault(neutralParty.MapFaction, playerParty.MapFaction);
            if (!FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction))
            {
                return Failed($"Unable to establish war between {neutralParty.MapFaction.Name} and {playerParty.MapFaction.Name}.");
            }

            neutralParty.SetMoveGoAroundParty(playerParty, MobileParty.NavigationType.Default);
            MakePeaceAction.Apply(neutralParty.MapFaction, playerParty.MapFaction);

            var stopped = neutralParty.DefaultBehavior == AiBehavior.Hold &&
                          neutralParty.PartyMoveMode == MoveModeType.Hold &&
                          neutralParty.TargetParty == null &&
                          !FactionManager.IsAtWarAgainstFaction(neutralParty.MapFaction, playerParty.MapFaction);

            string output = FormatPeacePursuitState(
                $"Peace pursuit test {(stopped ? "passed" : "failed")}",
                objectManager,
                neutralParty,
                playerParty);
            return stopped ? Succeeded(output) : Failed(output);
        }
    }

    private static bool TryGetPlayerParty(
        string controllerId,
        bool requireReady,
        out IObjectManager objectManager,
        out MobileParty playerParty,
        out string error,
        bool allowActiveMapEvent = false)
    {
        objectManager = null;
        playerParty = null;
        error = null;

        if (!TryGetObjectManager(out objectManager))
        {
            error = "Unable to resolve ObjectManager";
            return false;
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "Unable to resolve PlayerManager";
            return false;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            error = $"No registered player has controller id {controllerId}.";
            return false;
        }

        if (requireReady && ModInformation.IsServer && !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}.";
            return false;
        }

        if (requireReady && !allowActiveMapEvent && playerParty.MapEvent != null)
        {
            error = $"Player {controllerId} is already in a map event.";
            return false;
        }

        if (playerParty.MapFaction == null)
        {
            error = $"Player {controllerId} has no map faction.";
            return false;
        }

        return true;
    }

    private static MobileParty FindPeacePursuitFixture(MobileParty playerParty)
    {
        var playerPosition = playerParty.Position.ToVec2();
        return MobileParty.All
            .Where(p => IsPeacePursuitFixture(p, playerParty))
            .OrderBy(p => p.Position.ToVec2().DistanceSquared(playerPosition))
            .FirstOrDefault();
    }

    private static bool IsPeacePursuitFixture(MobileParty party, MobileParty playerParty)
    {
        return party.IsActive &&
               !party.IsBandit &&
               !party.IsPlayerParty() &&
               party != playerParty &&
               party.MapEvent == null &&
               party.CurrentSettlement == null &&
               party.MemberRoster.TotalManCount > 0 &&
               party.MapFaction != null &&
               party.MapFaction != playerParty.MapFaction &&
               !FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction) &&
               party.DefaultBehavior == AiBehavior.Hold &&
               party.PartyMoveMode == MoveModeType.Hold &&
               party.TargetParty == null;
    }

    private static string FormatPeacePursuitState(
        string prefix,
        IObjectManager objectManager,
        MobileParty party,
        MobileParty playerParty)
    {
        var registryId = objectManager.TryGetId(party, out string partyId) ? partyId : "none";
        var target = party.TargetParty == null ? "none" : party.TargetParty.StringId;
        var atWar = FactionManager.IsAtWarAgainstFaction(party.MapFaction, playerParty.MapFaction);
        var mapEvent = party.MapEvent == null ? "none" : party.MapEvent.ToString();

        return $"{prefix}: party={party.StringId}, registryId={registryId}, behavior={party.DefaultBehavior}, " +
               $"moveMode={party.PartyMoveMode}, target={target}, atWar={atWar}, mapEvent={mapEvent}.";
    }

    /// <summary>
    /// Kills a random troop from the enemy side of the current map event.
    /// </summary>
    public sealed class KillRandomTroopCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "kill_random_troop";

        public string Description => "Runs the kill random troop debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mapEvent = MobileParty.MainParty.MapEvent;
            if (mapEvent is null)
            {
                return Failed("Main party is not in a map event");
            }

            var mainPartySide = MobileParty.MainParty.MapEventSide;
            if (mainPartySide is null)
            {
                return Failed("Main party has no map event side");
            }

            var enemySide = mapEvent._sides
                .SingleOrDefault(side => side != mainPartySide);

            if (enemySide is null)
            {
                return Failed("Failed to get enemy map event side");
            }

            var party = enemySide.Parties[MBRandom.RandomInt(enemySide.Parties.Count)];
            if (party is null)
            {
                return Failed("Enemy side has no parties");
            }

            var troops = party.Troops;
            if (troops is null || troops.Count() == 0)
            {
                return Failed("Enemy party has no troops");
            }

            var entries = troops._elementDictionary.ToArray();

            if (entries.Length == 0)
            {
                return Failed("Enemy party has no troops");
            }

            var randomEntry = entries[MBRandom.RandomInt(entries.Length)];

            UniqueTroopDescriptor descriptor = randomEntry.Key;
            FlattenedTroopRosterElement troopElement = randomEntry.Value;

            try
            {
                enemySide.OnTroopKilled(descriptor);
            }
            catch (Exception ex)
            {
                return Failed($"Failed to kill random troop: {ex.Message}");
            }

            return Succeeded($"Killed random troop: {troopElement.Troop?.Name}");
        }
    }

    /// <summary>
    /// Kills all but one troop from the enemy side of the current map event.
    /// </summary>
    public sealed class KillAllButOneCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "kill_all_but_one";

        public string Description => "Runs the kill all but one debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mapEvent = MobileParty.MainParty.MapEvent;
            if (mapEvent is null)
            {
                return Failed("Main party is not in a map event");
            }

            var mainPartySide = MobileParty.MainParty.MapEventSide;
            if (mainPartySide is null)
            {
                return Failed("Main party has no map event side");
            }

            var enemySide = mapEvent._sides
                .SingleOrDefault(side => side != mainPartySide);

            if (enemySide is null)
            {
                return Failed("Failed to get enemy map event side");
            }

            if (enemySide.Parties is null || enemySide.Parties.Count == 0)
            {
                return Failed("Enemy side has no parties");
            }

            var allTroops = new List<(MapEventParty Party, UniqueTroopDescriptor Descriptor, FlattenedTroopRosterElement Element)>();

            foreach (var party in enemySide.Parties)
            {
                if (party?.Troops?._elementDictionary is null)
                    continue;

                foreach (var entry in party.Troops._elementDictionary)
                {
                    var descriptor = entry.Key;
                    var element = entry.Value;

                    allTroops.Add((party, descriptor, element));
                }
            }

            if (allTroops.Count == 0)
            {
                return Failed("Enemy side has no troops");
            }

            if (allTroops.Count == 1)
            {
                return Failed($"Enemy side already has only one troop: {allTroops[0].Element.Troop?.Name}");
            }

            var survivorIndex = MBRandom.RandomInt(allTroops.Count);
            var survivor = allTroops[survivorIndex];

            var killedCount = 0;

            for (var i = 0; i < allTroops.Count; i++)
            {
                if (i == survivorIndex)
                    continue;

                try
                {
                    enemySide.OnTroopKilled(allTroops[i].Descriptor);
                    killedCount++;
                }
                catch (Exception ex)
                {

                }
            }

            return Succeeded($"Killed {killedCount} troops. Survivor: {survivor.Element.Troop?.Name}");
        }
    }

    /// <summary>
    /// Lists the fields and properties of the current PlayerEncounter.
    /// </summary>
    public sealed class ListPlayerEncounterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "list_player_encounter";

        public string Description => "Reports player encounter.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var playerEncounter = PlayerEncounter.Current;
            if (playerEncounter == null)
            {
                return Failed("No current PlayerEncounter");
            }

            var sb = new StringBuilder();

            sb.AppendLine("PlayerEncounter:");
            AppendObjectDetails(sb, playerEncounter, "\t", "PlayerEncounter Details");

            var result = sb.ToString();

            Logger.Debug("{PlayerEncounter}", result);

            return Succeeded(result);
        }
    }

    /// <summary>
    /// Prints a compact, teardown-focused snapshot of the current <see cref="PlayerEncounter"/> and the main
    /// party's map-event state. Run on each client after a battle to spot an encounter that did not tear down —
    /// e.g. PlayerEncounter.Current still PRESENT, or MainParty.MapEvent lingering on an already-finalized event.
    /// Unlike <c>list_player_encounter</c> (full reflection dump) this is short enough to diff across clients.
    /// </summary>
    public sealed class EncounterStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "encounter_state";

        public string Description => "Reports encounter state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            TryGetObjectManager(out var objectManager);

            var sb = new StringBuilder();

            var encounter = PlayerEncounter.Current;
            sb.AppendLine($"PlayerEncounter.Current: {(encounter == null ? "<null> (torn down)" : "PRESENT")}");
            if (encounter != null)
            {
                sb.AppendLine($"\tBattle:           {FormatMapEvent(PlayerEncounter.Battle, objectManager)}");
                sb.AppendLine($"\t_mapEvent:        {FormatMapEvent(encounter._mapEvent, objectManager)}");
                sb.AppendLine($"\tEncounteredParty: {FormatPartyBaseWithId(PlayerEncounter.EncounteredParty, objectManager)}");
                sb.AppendLine($"\t_attackerParty:   {FormatPartyBaseWithId(encounter._attackerParty, objectManager)}");
                sb.AppendLine($"\t_defenderParty:   {FormatPartyBaseWithId(encounter._defenderParty, objectManager)}");
            }

            var mainParty = MobileParty.MainParty;
            sb.AppendLine($"MainParty.MapEvent:      {FormatMapEvent(mainParty?.MapEvent, objectManager)}");

            var side = mainParty?.Party?.MapEventSide;
            if (side == null)
                sb.AppendLine("MainParty.MapEventSide:  <null>");
            else
                sb.AppendLine($"MainParty.MapEventSide:  leader={FormatPartyBaseWithId(side.LeaderParty, objectManager)} mainPartyIsLeader={side.LeaderParty == mainParty?.Party}");

            sb.AppendLine($"CurrentMenu:             {Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>"}");
            sb.AppendLine($"CurrentBattleSimulation: {(PlayerEncounter.CurrentBattleSimulation == null ? "<null>" : "PRESENT")}");
            sb.AppendLine($"MissionState.Current:    {(MissionState.Current == null ? "<null>" : "PRESENT")}");

            var result = sb.ToString();
            Logger.Debug("{EncounterState}", result);
            return Succeeded(result);
        }
    }

    /// <summary>Shows, closes, or reports the live retreat confirmation for automated battle-exit testing.</summary>
    public sealed class RetreatConfirmationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "retreat_confirmation";

        public string Description => "Runs the retreat confirmation debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("action", "The action.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client in a battle mission.");


            var handler = Mission.Current?.GetMissionBehavior<BasicMissionHandler>();
            if (handler == null)
                return Failed("No active battle retreat handler.");

            switch (args[0].ToLowerInvariant())
            {
                case "show":
                    if (handler.IsWarningWidgetOpened)
                        return Failed("Retreat confirmation already open: true");

                    handler.CreateWarningWidgetForResult(BattleEndLogic.ExitResult.NeedsPlayerConfirmation);
                    return Succeeded($"Retreat confirmation open: {handler.IsWarningWidgetOpened}");
                case "accept":
                    if (!handler.IsWarningWidgetOpened)
                        return Failed("Retreat confirmation is not open.");

                    InformationManager.HideInquiry();
                    handler.OnEventAcceptSelectionWidget();
                    return Succeeded("Retreat confirmation accepted.");
                case "cancel":
                    if (!handler.IsWarningWidgetOpened)
                        return Failed("Retreat confirmation is not open.");

                    InformationManager.HideInquiry();
                    handler.OnEventCancelSelectionWidget();
                    return Succeeded($"Retreat confirmation open: {handler.IsWarningWidgetOpened}");
                case "state":
                    return Succeeded($"Retreat confirmation open: {handler.IsWarningWidgetOpened}");
                default:
                    return Failed("Invalid command argument value.");
            }
        }
    }

    /// <summary>Closes the current encounter conversation so vanilla can advance to battle choices.</summary>
    public sealed class CompleteEncounterMeetingCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "complete_encounter_meeting";

        public string Description => "Runs the complete encounter meeting debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client at the encounter meeting.");


            if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "encounter_meeting")
                return Failed("The encounter meeting is not active.");

            var conversationManager = Campaign.Current.ConversationManager;
            if (!conversationManager.IsConversationInProgress)
                return Failed("The encounter conversation is not active.");

            conversationManager.EndConversation();
            return Succeeded("Encounter meeting completed.");
        }
    }

    /// <summary>Runs the encounter menu's mission or simulation consequence for automated battle testing.</summary>
    public sealed class ChooseBattleModeCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "choose_battle_mode";

        public string Description => "Runs the choose battle mode debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "The battle mode.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client at the encounter menu.");


            if (PlayerEncounter.Current == null)
                return Failed("No active player encounter.");

            var behavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
            if (behavior == null)
                return Failed("Encounter menu behavior is unavailable.");

            switch (args[0].ToLowerInvariant())
            {
                case "mission":
                    behavior.game_menu_encounter_attack_on_consequence(null);
                    return Succeeded("Mission battle requested.");
                case "simulation":
                    behavior.game_menu_encounter_order_attack_on_consequence(null);
                    return Succeeded("Battle simulation requested.");
                default:
                    return Failed("Invalid command argument value.");
            }
        }
    }

    private static string FormatMapEvent(MapEvent mapEvent, IObjectManager objectManager)
    {
        if (mapEvent == null) return "<null>";

        var id = "<no id>";
        if (!mapEvent.IsFinalized && objectManager != null && objectManager.TryGetId(mapEvent, out var resolved))
            id = resolved;

        return $"id={id} finalized={mapEvent.IsFinalized} state={mapEvent.BattleState} winner={mapEvent.WinningSide}";
    }

    public sealed class GetEventsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "get_events";

        public string Description => "Reports events.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var sb = new StringBuilder();

            if(!TryGetObjectManager(out var objectManager))
            {
                return Failed("Failed to get object manager");
            }

            foreach(var mapEvent in Campaign.Current.MapEventManager.MapEvents)
            {
                if (objectManager.TryGetIdWithLogging(mapEvent, out var id))
                {
                    sb.AppendLine($"Map event id: {id}");
                }

                var partyNames = mapEvent.AttackerSide.Parties?
                    .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
                    .ToArray() ?? Array.Empty<string>();
                sb.AppendLine($"\tAttacker: {string.Join(",", FormatSideNames(mapEvent.AttackerSide))}");
                sb.AppendLine($"\tDefender: {string.Join(",", FormatSideNames(mapEvent.DefenderSide))}");
            }

            return Succeeded(sb.ToString());
        }
    }

    private static string[] FormatSideNames(MapEventSide side)
    {
        if (side == null)
            return new string[] { "<null>" };

        return side.Parties?
            .Select(party => party?.Party?.Name?.ToString() ?? "<null>")
            .ToArray() ?? Array.Empty<string>();
    }

    public sealed class GetEventCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";

        public string Name => "get_event";

        public string Description => "Reports event.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("map_event_id", "The map event id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Failed to get object manager");
            }

            var mapEventId = args[0];

            if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
            {
                return Failed($"Failed to find MapEvent with id: {mapEventId}");
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Map event id: {mapEventId}");
            sb.AppendLine();

            AppendMapEventSummary(sb, mapEvent);
            sb.AppendLine();

            var result = sb.ToString();

            Logger.Debug("{MapEvent}", result);

            return Succeeded(result);
        }
    }

    private static void AppendMapEventSummary(StringBuilder sb, MapEvent mapEvent)
    {
        sb.AppendLine("Summary:");

        AppendSideSummary(sb, "Attacker", mapEvent.AttackerSide);
        AppendSideSummary(sb, "Defender", mapEvent.DefenderSide);
    }

    private static void AppendSideSummary(StringBuilder sb, string sideName, MapEventSide side)
    {
        if (side == null)
        {
            sb.AppendLine($"\t{sideName}: <null>");
            return;
        }

        sb.AppendLine($"\t{sideName}: {string.Join(", ", FormatSideNames(side))}");

        AppendObjectDetails(sb, side, "\t\t", "Side Details");

        sb.AppendLine("\t\tParties:");

        var parties = side.Parties;
        if (parties == null)
        {
            sb.AppendLine("\t\t\t<null>");
            return;
        }

        var index = 0;
        foreach (var party in parties)
        {
            sb.AppendLine($"\t\t\tParty[{index}]:");

            if (party == null)
            {
                sb.AppendLine("\t\t\t\t<null>");
            }
            else
            {
                AppendMapEventPartyDetails(sb, party, "\t\t\t\t");
            }

            index++;
        }
    }
    private static void AppendMapEventPartyDetails(StringBuilder sb, MapEventParty party, string indent)
    {
        var partyName = party.Party?.Name?.ToString() ?? "<null>";
        sb.AppendLine($"{indent}Party: {partyName}");

        AppendObjectDetails(sb, party, indent, "MapEventParty Details");
    }

    private static void AppendObjectDetails(StringBuilder sb, object obj, string indent, string title)
    {
        if (obj == null)
        {
            sb.AppendLine($"{indent}{title}: <null>");
            return;
        }

        var type = obj.GetType();

        sb.AppendLine($"{indent}{title}: {GetFriendlyTypeName(type)}");

        AppendFields(sb, obj, type, indent + "\t");
        AppendProperties(sb, obj, type, indent + "\t");
    }

    private static void AppendFields(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Fields:");

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (fields.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var field in fields.OrderBy(f => f.Name))
        {
            object value;

            try
            {
                value = field.GetValue(obj);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{field.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{field.Name}: {FormatValue(value)}");
        }
    }

    private static void AppendProperties(StringBuilder sb, object obj, Type type, string indent)
    {
        sb.AppendLine($"{indent}Properties:");

        var properties = type.GetProperties(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);

        if (properties.Length == 0)
        {
            sb.AppendLine($"{indent}\t<none>");
            return;
        }

        foreach (var property in properties.OrderBy(p => p.Name))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <indexed property>");
                continue;
            }

            object value;

            try
            {
                value = property.GetValue(obj, null);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}\t{property.Name}: <failed: {ex.GetType().Name}>");
                continue;
            }

            sb.AppendLine($"{indent}\t{property.Name}: {FormatValue(value)}");
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "<null>";

        if (value is string str)
            return str;

        if (value is TextObject textObject)
            return textObject.ToString();

        if (value is CharacterObject character)
            return FormatCharacter(character);

        if (value is MobileParty mobileParty)
            return FormatMobileParty(mobileParty);

        if (value is PartyBase partyBase)
            return FormatPartyBase(partyBase);

        if (value is IFaction faction)
            return faction.Name?.ToString() ?? faction.StringId ?? "<unnamed faction>";

        if (value is UniqueTroopDescriptor descriptor)
            return descriptor.ToString();

        if (value is IEnumerable enumerable && !(value is string))
            return FormatEnumerable(enumerable);

        return value.ToString();
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var values = new List<string>();
        var count = 0;

        foreach (var item in enumerable)
        {
            if (count >= 20)
            {
                values.Add("...");
                break;
            }

            values.Add(FormatValue(item));
            count++;
        }

        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatCharacter(CharacterObject character)
    {
        if (character == null)
            return "<null>";

        var id = character.StringId ?? "<no id>";
        var name = character.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatMobileParty(MobileParty party)
    {
        if (party == null)
            return "<null>";

        var id = party.StringId ?? "<no id>";
        var name = party.Name?.ToString() ?? "<no name>";

        return $"{name} ({id})";
    }

    private static string FormatPartyBase(PartyBase party)
    {
        if (party == null)
            return "<null>";

        var name = party.Name?.ToString() ?? "<no name>";

        return name;
    }

    private static string FormatPartyBaseWithId(PartyBase party, IObjectManager objectManager)
    {
        if (party == null)
            return "<null>";

        var partyBaseId = objectManager != null && objectManager.TryGetId(party, out string resolvedPartyBaseId)
            ? resolvedPartyBaseId
            : "<unregistered>";

        return $"{FormatPartyBase(party)} (PartyBase id {partyBaseId})";
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "<null>";

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var tickIndex = genericTypeName.IndexOf('`');

        if (tickIndex >= 0)
            genericTypeName = genericTypeName.Substring(0, tickIndex);

        var genericArguments = type.GetGenericArguments()
            .Select(GetFriendlyTypeName)
            .ToArray();

        return genericTypeName + "<" + string.Join(", ", genericArguments) + ">";
    }
}

#if DEBUG
/// <summary>[Server -&gt; Client] Ends a live-test fixture mission without resolving its campaign battle.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEndLateJoinModeFixtureMission : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkEndLateJoinModeFixtureMission(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}

/// <summary>Applies the server's live-test fixture mission-exit request on participating clients.</summary>
internal sealed class LateJoinModeFixtureMissionExitHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public LateJoinModeFixtureMissionExitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkEndLateJoinModeFixtureMission>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkEndLateJoinModeFixtureMission>(Handle);
    }

    private void Handle(MessagePayload<NetworkEndLateJoinModeFixtureMission> payload)
    {
        if (ModInformation.IsServer)
            return;

        var mapEventId = payload.What.MapEventId;
        GameThread.RunSafe(() =>
        {
            var mapEvent = MobileParty.MainParty?.MapEvent;
            if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var localMapEventId) ||
                localMapEventId != mapEventId)
                return;

            var mission = Mission.Current ?? MissionState.Current?.CurrentMission;
            mission?.EndMission();
        }, context: nameof(NetworkEndLateJoinModeFixtureMission));
    }
}
#endif
