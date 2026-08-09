using Autofac;
using Common;
using Common.Messaging;
using Common.Logging;
using GameInterface.Serialization.External;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.MobileParties.Messages.Roles;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Helpers;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Companions.Commands;

internal class CompanionsCommands
{
    private const string RoleFixtureName = "Issue 2583 Role Companion";
    private const string RescueJoinFixtureName = "Issue 2762 Join Rescue Companion";
    private const string RescuePartyFixtureName = "Issue 2762 Party Rescue Companion";
    private const string RescueCaptorSettlementId = "town_ES1";
    private static readonly ILogger Logger = LogManager.GetLogger<CompanionsCommands>();
    private static CompanionRoleFixture pendingRoleFixture;
    private static CompanionDismissalFixture pendingDismissalFixture;
    private static CompanionRescueFixture pendingRescueFixture;
    private static int rescueJoinRequestCount;
    private static int rescuePartyRequestCount;
    private static Action<MessagePayload<DoCompanionJoinedPartyByRescue>> rescueJoinRequestObserver;
    private static Action<MessagePayload<DoPartyScreenClosedFromRescuing>> rescuePartyRequestObserver;
    private static PartyScreenClosedFromRescuing? lastRescuePartyScreenCompletion;
    private static Action<MessagePayload<PartyScreenClosedFromRescuing>> rescuePartyScreenObserver;
    private static CompanionDismissalCompleted? lastDismissalCompletion;
    private static DismissalEncounterObservation lastDismissalEncounterObservation;
    private static Action<MessagePayload<CompanionDismissalCompleted>> dismissalCompletionHandler;

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// View a list of all wanderers in the game
    /// </summary>
    [CommandLineArgumentFunction("list_wanderers", "coop.debug.companions")]
    public static string ListWanderersCommand(List<string> strings)
    {
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.IsWanderer)
            {
                if (!objectManager.TryGetIdWithLogging(hero, out var heroId))
                {
                    stringBuilder.AppendLine($"Failed to resolve hero id for wanderer with name {hero.Name}");
                    continue;
                }

                stringBuilder.AppendLine($"{hero.Name} (ID: {heroId}) Current Settlement: {hero.CurrentSettlement}");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "No wanderers found.";
    }

    /// <summary>
    /// Clear the wanderers from the map to roll new ones
    /// </summary>
    [CommandLineArgumentFunction("clear_wanderers", "coop.debug.companions")]
    public static string ClearWanderersCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "This command can only be run on the server.";

        foreach (var hero in Hero.AllAliveHeroes.ToList())
        {
            if (hero.IsWanderer && hero.CompanionOf == null)
            {
                KillCharacterAction.ApplyByRemove(hero, false, true);
            }
        }

        return "All wanderers removed.";
    }

    [CommandLineArgumentFunction("role_fixture_setup", "coop.debug.companions")]
    public static string RoleFixtureSetupCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_setup <controllerId>";
        var context = new CommandContext("role_fixture_setup", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(1, out error)) return error;
        if (pendingRoleFixture != null) return "A companion-role fixture is already active.";

        if (!TryResolvePlayer(args[0], out _, out var objectManager, out var player,
            out var playerHero, out var playerClan, out var playerParty, out error))
            return "Failed to set up companion-role fixture: " + error;

        var template = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.IsWanderer && hero != playerHero);
        if (template == null)
            return "Failed to set up companion-role fixture: no living wanderer template is available.";

        int originalMemberCount = playerParty.MemberRoster.TotalManCount;
        int originalCompanionCount = playerClan.Companions.Count();
        var originalScout = playerParty.GetRoleHolder(PartyRole.Scout);
        var roleCompanion = CreateFixtureCompanion(
            template, playerHero.HomeSettlement, RoleFixtureName);
        if (!objectManager.TryGetIdWithLogging(roleCompanion, out var roleCompanionId))
            return "Failed to set up companion-role fixture: generated hero was not registered.";

        AddCompanionAction.Apply(playerClan, roleCompanion);
        AddHeroToPartyAction.Apply(roleCompanion, playerParty, true);

        pendingRoleFixture = new CompanionRoleFixture(
            player.ControllerId,
            player.MobilePartyId,
            roleCompanion,
            roleCompanionId,
            originalScout,
            originalMemberCount,
            originalCompanionCount);

        string originalScoutId = "none";
        if (originalScout != null && objectManager.TryGetIdWithLogging(originalScout, out var resolvedScoutId))
            originalScoutId = resolvedScoutId;

        return $"ROLE_FIXTURE_READY controller={player.ControllerId} party={player.MobilePartyId} " +
            $"companion={roleCompanionId} originalScout={originalScoutId} " +
            $"members={playerParty.MemberRoster.TotalManCount} companions={playerClan.Companions.Count()}";
    }

    [CommandLineArgumentFunction("role_fixture_open_conversation", "coop.debug.companions")]
    public static string RoleFixtureOpenConversationCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_open_conversation";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return usage;
        var companion = FindRoleFixtureCompanion();
        if (companion == null) return "The companion-role fixture hero was not found.";
        if (companion.Clan != Clan.PlayerClan || companion.PartyBelongedTo != MobileParty.MainParty)
            return "The fixture companion is not in the local player's clan and main party.";
        if (PlayerEncounter.Current != null) return "A player encounter is already active.";
        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            return "A conversation is already active.";
        if (!Campaign.Current.Models.ClanMemberPartyRoleModel.IsHeroAssignableForPartyRoleInParty(
            PartyRole.Scout, companion, MobileParty.MainParty))
            return "The fixture companion is not eligible for the Scout role.";

        try
        {
            Campaign.Current.CurrentConversationContext = ConversationContext.PartyEncounter;
            CampaignMapConversation.OpenConversation(
                new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
                new ConversationCharacterData(companion.CharacterObject, PartyBase.MainParty, noHorse: true));
            if (Hero.OneToOneConversationHero != companion)
                throw new InvalidOperationException("The live conversation did not select the fixture companion.");

            return $"ROLE_CONVERSATION_OPEN companion={companion.StringId} conversationHeroMatched=True";
        }
        catch (Exception exception)
        {
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            return "Failed to open the live role conversation: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("role_fixture_conversation_state", "coop.debug.companions")]
    public static string RoleFixtureConversationStateCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_conversation_state";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return usage;

        var companion = FindRoleFixtureCompanion();
        var mapState = Game.Current?.GameStateManager?.ActiveState as MapState;
        bool conversationActive = Campaign.Current?.ConversationManager?.IsConversationInProgress == true;
        bool mapConversationActive = mapState?.MapConversationActive == true;
        bool conversationHeroMatched = companion != null && Hero.OneToOneConversationHero == companion;
        return $"ROLE_CONVERSATION_STATE active={conversationActive} mapActive={mapConversationActive} " +
            $"companion={companion?.StringId ?? "none"} conversationHeroMatched={conversationHeroMatched}";
    }

    [CommandLineArgumentFunction("role_fixture_prepare_client", "coop.debug.companions")]
    public static string RoleFixturePrepareClientCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_prepare_client";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return usage;
        var companion = FindRoleFixtureCompanion();
        if (companion == null) return "The companion-role fixture hero was not found.";
        if (Hero.MainHero == null) return "The local main hero is unavailable.";

        bool initializedExSpouses = companion.ExSpouses == null;
        if (initializedExSpouses)
        {
            HeroBinaryPackage.Hero_ExSpouses.SetValue(companion, new MBList<Hero>());
        }

        string relation = ConversationHelper.GetHeroRelationToHeroTextShort(
            companion, Hero.MainHero, uppercaseFirst: true);
        return $"ROLE_FIXTURE_CLIENT_READY companion={companion.StringId} " +
            $"mainHero={Hero.MainHero.StringId} initializedExSpouses={initializedExSpouses} " +
            $"relation={relation}";
    }

    [CommandLineArgumentFunction("role_fixture_assign_scout", "coop.debug.companions")]
    public static string RoleFixtureAssignScoutCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_assign_scout";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return usage;
        var companion = Hero.OneToOneConversationHero;
        if (!Campaign.Current.ConversationManager.IsConversationInProgress ||
            companion?.Name?.ToString() != RoleFixtureName)
            return "The fixture companion conversation is not active.";

        var behavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();
        if (behavior == null) return "CompanionRolesCampaignBehavior is unavailable.";

        try
        {
            behavior.companion_becomes_scout_on_consequence();
            Campaign.Current.ConversationManager.EndConversation();
            return $"ROLE_CONVERSATION_ASSIGNED companion={companion.StringId} role=Scout conversationHeroMatched=True";
        }
        catch (Exception exception)
        {
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            return "Failed to assign the live conversation role: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("role_fixture_state", "coop.debug.companions")]
    public static string RoleFixtureStateCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_state <partyId>";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty party))
            return $"Party '{args[0]}' not found.";
        var companion = FindRoleFixtureCompanion();
        if (companion == null) return "The companion-role fixture hero was not found.";

        var scout = party.GetRoleHolder(PartyRole.Scout);
        string scoutId = "none";
        if (scout != null && objectManager.TryGetIdWithLogging(scout, out var resolvedScoutId))
            scoutId = resolvedScoutId;
        string roles = string.Join(",", party.GetHeroPartyRoles(companion));

        return $"ROLE_FIXTURE_STATE party={args[0]} companion={companion.StringId} " +
            $"roster={party.MemberRoster.GetTroopCount(companion.CharacterObject)} " +
            $"scout={scoutId} assigned={scout == companion} roles={roles}";
    }

    [CommandLineArgumentFunction("scout_role_state", "coop.debug.companions")]
    public static string ScoutRoleStateCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.scout_role_state <partyId>";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty party))
            return $"Party '{args[0]}' not found.";

        var scout = party.GetRoleHolder(PartyRole.Scout);
        string scoutId = "none";
        if (scout != null && objectManager.TryGetIdWithLogging(scout, out var resolvedScoutId))
            scoutId = resolvedScoutId;

        return $"SCOUT_ROLE_STATE party={args[0]} scout={scoutId}";
    }

    [CommandLineArgumentFunction("role_fixture_restore", "coop.debug.companions")]
    public static string RoleFixtureRestoreCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.role_fixture_restore <controllerId>";
        var context = new CommandContext("role_fixture_restore", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(1, out error)) return error;
        if (pendingRoleFixture == null) return "No companion-role fixture is active.";
        if (pendingRoleFixture.ControllerId != args[0])
            return $"The active companion-role fixture belongs to '{pendingRoleFixture.ControllerId}'.";
        if (!TryResolvePlayer(args[0], out var playerManager, out var objectManager,
            out _, out _, out var clan, out var party, out error))
            return "Failed to restore companion-role fixture: " + error;
        if (!playerManager.TryGetPeer(args[0], out var peer))
            return $"Failed to restore companion-role fixture: player '{args[0]}' is not connected.";

        var fixture = pendingRoleFixture;
        if (fixture.RoleCompanion.CompanionOf != null)
        {
            MessageBroker.Instance.Publish(peer, CreateCleanupDismissalRequest(
                objectManager, fixture.RoleCompanion, fixture.RoleCompanionId));
        }

        if (party.GetRoleHolder(PartyRole.Scout) != fixture.OriginalScout)
        {
            party.SetPartyScout(fixture.OriginalScout);
            MessageBroker.Instance.Publish(party, new SetPartyScout(fixture.OriginalScout, party));
        }

        int roleCompanionCount = party.MemberRoster.GetTroopCount(fixture.RoleCompanion.CharacterObject);
        int memberCount = party.MemberRoster.TotalManCount;
        int companionCount = clan.Companions.Count();
        if (roleCompanionCount != 0 || memberCount != fixture.OriginalMemberCount ||
            companionCount != fixture.OriginalCompanionCount ||
            party.GetRoleHolder(PartyRole.Scout) != fixture.OriginalScout)
        {
            return $"ROLE_FIXTURE_RESTORE_FAILED roleCompanion={roleCompanionCount} " +
                $"members={memberCount}/{fixture.OriginalMemberCount} " +
                $"companions={companionCount}/{fixture.OriginalCompanionCount}";
        }

        pendingRoleFixture = null;
        return $"ROLE_FIXTURE_RESTORED party={fixture.PlayerPartyId} roleCompanion=0 " +
            $"members={memberCount} companions={companionCount}";
    }

    [CommandLineArgumentFunction("dismissal_fixture_setup", "coop.debug.companions")]
    public static string DismissalFixtureSetupCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_setup <controllerId>";
        var context = new CommandContext("dismissal_fixture_setup", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(1, out error)) return error;
        if (pendingDismissalFixture != null) return "A companion-dismissal fixture is already active.";

        if (!TryResolvePlayer(args[0], out var playerManager, out var objectManager, out var player,
            out var playerHero, out var playerClan, out var playerParty, out error))
            return "Failed to set up companion-dismissal fixture: " + error;

        var template = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.IsWanderer && hero != playerHero);
        if (template == null)
            return "Failed to set up companion-dismissal fixture: no living wanderer template is available.";

        int originalMemberCount = playerParty.MemberRoster.TotalManCount;
        int originalCompanionCount = playerClan.Companions.Count();
        var dismissed = CreateFixtureCompanion(template, playerHero.HomeSettlement, "Issue 2280 Dismissed");
        var replacement = CreateFixtureCompanion(template, playerHero.HomeSettlement, "Issue 2280 Replacement");

        if (!objectManager.TryGetIdWithLogging(dismissed, out var dismissedHeroId) ||
            !objectManager.TryGetIdWithLogging(replacement, out var replacementHeroId))
            return "Failed to set up companion-dismissal fixture: generated heroes were not registered.";

        AddCompanionAction.Apply(playerClan, dismissed);
        AddHeroToPartyAction.Apply(dismissed, playerParty, true);

        pendingDismissalFixture = new CompanionDismissalFixture(
            player.ControllerId,
            player.HeroId,
            player.ClanId,
            player.MobilePartyId,
            dismissed,
            dismissedHeroId,
            replacement,
            replacementHeroId,
            originalMemberCount,
            originalCompanionCount);

        return $"FIXTURE_READY controller={player.ControllerId} hero={player.HeroId} " +
            $"clan={player.ClanId} party={player.MobilePartyId} " +
            $"dismissedHero={dismissedHeroId} replacementHero={replacementHeroId} " +
            $"dismissedCount={playerParty.MemberRoster.GetTroopCount(dismissed.CharacterObject)}";
    }

    [CommandLineArgumentFunction("dismissal_fixture_prepare_dismiss", "coop.debug.companions")]
    public static string DismissalFixturePrepareDismissCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_prepare_dismiss <controllerId> <initialCopies>";
        var context = new CommandContext("dismissal_fixture_prepare_dismiss", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(2, out error)) return error;
        if (!int.TryParse(args[1], out var initialCopies) || initialCopies < 1)
            return usage;
        if (!TryGetFixture(args[0], out var fixture, out error)) return error;
        if (!TryResolvePlayer(args[0], out _, out _, out _, out _, out _, out var party, out error))
            return "Failed to prepare fixture dismissal: " + error;

        int current = party.MemberRoster.GetTroopCount(fixture.Dismissed.CharacterObject);
        if (current < 1)
            return "Failed to prepare fixture dismissal: the companion is not in the player party.";
        if (current != initialCopies)
        {
            party.MemberRoster.AddToCounts(fixture.Dismissed.CharacterObject, initialCopies - current);
        }

        return $"DISMISSAL_PREPARED hero={fixture.DismissedHeroId} requestedCopies={initialCopies} " +
            $"count={party.MemberRoster.GetTroopCount(fixture.Dismissed.CharacterObject)}";
    }

    [CommandLineArgumentFunction("dismissal_fixture_trigger_consequence", "coop.debug.companions")]
    public static string DismissalFixtureTriggerConsequenceCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_trigger_consequence <dismissedHeroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero dismissed)) return $"Hero '{args[0]}' not found.";
        if (PlayerEncounter.Current != null) return "A player encounter is already active.";
        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            return "A conversation is already active.";

        var behavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();
        if (behavior == null) return "CompanionRolesCampaignBehavior is unavailable.";

        lastDismissalCompletion = null;
        lastDismissalEncounterObservation = new DismissalEncounterObservation();
        if (dismissalCompletionHandler != null)
        {
            MessageBroker.Instance.Unsubscribe(dismissalCompletionHandler);
        }
        dismissalCompletionHandler = payload =>
        {
            if (payload.What.OneToOneConversationHeroId != args[0]) return;
            lastDismissalCompletion = payload.What;
            lastDismissalEncounterObservation.EncounterActiveAtCompletion = PlayerEncounter.Current != null;
            lastDismissalEncounterObservation.LeaveAtCompletion =
                PlayerEncounter.Current != null && PlayerEncounter.LeaveEncounter;
            MessageBroker.Instance.Unsubscribe(dismissalCompletionHandler);
            dismissalCompletionHandler = null;
        };
        MessageBroker.Instance.Subscribe(dismissalCompletionHandler);

        try
        {
            PlayerEncounter.Start();
            Campaign.Current.CurrentConversationContext = ConversationContext.PartyEncounter;
            CampaignMapConversation.OpenConversation(
                new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
                new ConversationCharacterData(dismissed.CharacterObject, PartyBase.MainParty, noHorse: true));

            lastDismissalEncounterObservation.EncounterActiveAtTrigger = PlayerEncounter.Current != null;
            lastDismissalEncounterObservation.LeaveBeforeConsequence = PlayerEncounter.LeaveEncounter;
            lastDismissalEncounterObservation.ConversationHeroMatched = Hero.OneToOneConversationHero == dismissed;
            if (!lastDismissalEncounterObservation.ConversationHeroMatched)
                throw new InvalidOperationException("The live conversation did not select the dismissed companion.");

            behavior.companion_fire_on_consequence();
            lastDismissalEncounterObservation.LeaveAfterConsequence =
                PlayerEncounter.Current != null && PlayerEncounter.LeaveEncounter;

            // The real farewell line ends at close_window after running this consequence. Close the synthetic
            // map conversation too, while leaving its encounter held until the correlated acknowledgement.
            Campaign.Current.ConversationManager.EndConversation();

            return $"DISMISSAL_CONSEQUENCE_TRIGGERED hero={args[0]} " +
                $"encounterActive={lastDismissalEncounterObservation.EncounterActiveAtTrigger} " +
                $"conversationHeroMatched={lastDismissalEncounterObservation.ConversationHeroMatched} " +
                $"leaveBefore={lastDismissalEncounterObservation.LeaveBeforeConsequence} " +
                $"leaveAfter={lastDismissalEncounterObservation.LeaveAfterConsequence}";
        }
        catch (Exception exception)
        {
            MessageBroker.Instance.Unsubscribe(dismissalCompletionHandler);
            dismissalCompletionHandler = null;
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            Campaign.Current.PlayerEncounter = null;
            return "Failed to trigger the live dismissal consequence: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("dismissal_fixture_completion", "coop.debug.companions")]
    public static string DismissalFixtureCompletionCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_completion <dismissedHeroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (lastDismissalCompletion == null ||
            lastDismissalCompletion.Value.OneToOneConversationHeroId != args[0])
            return $"DISMISSAL_PENDING hero={args[0]}";

        var completion = lastDismissalCompletion.Value;
        var observation = lastDismissalEncounterObservation;
        return $"DISMISSAL_COMPLETED hero={args[0]} request={completion.RequestId} " +
            $"success={completion.Success} error={completion.Error ?? "none"} " +
            $"encounterAtTrigger={observation?.EncounterActiveAtTrigger} " +
            $"conversationHeroMatched={observation?.ConversationHeroMatched} " +
            $"leaveBefore={observation?.LeaveBeforeConsequence} " +
            $"leaveAfterConsequence={observation?.LeaveAfterConsequence} " +
            $"encounterAtCompletion={observation?.EncounterActiveAtCompletion} " +
            $"leaveAtCompletion={observation?.LeaveAtCompletion}";
    }

    [CommandLineArgumentFunction("dismissal_fixture_release_encounter", "coop.debug.companions")]
    public static string DismissalFixtureReleaseEncounterCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_release_encounter <dismissedHeroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (lastDismissalCompletion == null ||
            lastDismissalCompletion.Value.OneToOneConversationHeroId != args[0])
            return $"Dismissal completion for hero '{args[0]}' has not arrived.";
        if (lastDismissalEncounterObservation?.LeaveAtCompletion != true)
            return "The dismissal encounter was not released by the correlated completion.";

        bool wasActive = PlayerEncounter.Current != null;
        Campaign.Current.PlayerEncounter = null;
        return $"DISMISSAL_ENCOUNTER_RELEASED hero={args[0]} wasActive={wasActive} leaveAcknowledged=True";
    }

    [CommandLineArgumentFunction("dismissal_fixture_request_replacement", "coop.debug.companions")]
    public static string DismissalFixtureRequestReplacementCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_request_replacement <replacementHeroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero replacement)) return $"Hero '{args[0]}' not found.";
        if (Hero.MainHero?.Clan == null || MobileParty.MainParty == null)
            return "The local player hero, clan, or main party is unavailable.";

        MessageBroker.Instance.Publish(null,
            new CompanionHired(Hero.MainHero, replacement, 0, Hero.MainHero.Clan, MobileParty.MainParty));
        return $"REPLACEMENT_REQUESTED hero={args[0]}";
    }

    [CommandLineArgumentFunction("dismissal_fixture_state", "coop.debug.companions")]
    public static string DismissalFixtureStateCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_state <partyId> <dismissedHeroId> <replacementHeroId>";
        if (args.Count != 3) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty party)) return $"Party '{args[0]}' not found.";
        if (!objectManager.TryGetObject(args[1], out Hero dismissed)) return $"Hero '{args[1]}' not found.";
        if (!objectManager.TryGetObject(args[2], out Hero replacement)) return $"Hero '{args[2]}' not found.";

        return "COMPANION_STATE " + FormatHeroState("dismissed", party, dismissed) + " " +
            FormatHeroState("replacement", party, replacement);
    }

    [CommandLineArgumentFunction("dismissal_fixture_restore", "coop.debug.companions")]
    public static string DismissalFixtureRestoreCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.dismissal_fixture_restore <controllerId>";
        var context = new CommandContext("dismissal_fixture_restore", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(1, out error)) return error;
        if (!TryGetFixture(args[0], out var fixture, out error)) return error;
        if (!TryResolvePlayer(args[0], out var playerManager, out var objectManager,
            out _, out _, out var clan, out var party, out error))
            return "Failed to restore companion-dismissal fixture: " + error;
        if (!playerManager.TryGetPeer(args[0], out var peer))
            return $"Failed to restore companion-dismissal fixture: player '{args[0]}' is not connected.";

        if (fixture.Dismissed.CompanionOf != null)
        {
            MessageBroker.Instance.Publish(peer, CreateCleanupDismissalRequest(
                objectManager, fixture.Dismissed, fixture.DismissedHeroId));
        }
        if (fixture.Replacement.CompanionOf != null)
        {
            MessageBroker.Instance.Publish(peer, CreateCleanupDismissalRequest(
                objectManager, fixture.Replacement, fixture.ReplacementHeroId));
        }

        int dismissedCount = party.MemberRoster.GetTroopCount(fixture.Dismissed.CharacterObject);
        int replacementCount = party.MemberRoster.GetTroopCount(fixture.Replacement.CharacterObject);
        int memberCount = party.MemberRoster.TotalManCount;
        int companionCount = clan.Companions.Count();
        if (dismissedCount != 0 || replacementCount != 0 ||
            memberCount != fixture.OriginalMemberCount || companionCount != fixture.OriginalCompanionCount)
        {
            return $"RESTORE_FAILED dismissed={dismissedCount} replacement={replacementCount} " +
                $"members={memberCount}/{fixture.OriginalMemberCount} companions={companionCount}/{fixture.OriginalCompanionCount}";
        }

        pendingDismissalFixture = null;
        return $"FIXTURE_RESTORED party={fixture.PlayerPartyId} dismissed=0 replacement=0 " +
            $"members={memberCount} companions={companionCount}";
    }

    [CommandLineArgumentFunction("rescue_fixture_setup", "coop.debug.companions")]
    public static string RescueFixtureSetupCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_setup <controllerId>";
        var context = new CommandContext("rescue_fixture_setup", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(1, out error)) return error;
        if (pendingRescueFixture != null) return "A companion-rescue fixture is already active.";
        if (FindRescueFixtureHero(RescueJoinFixtureName) != null ||
            FindRescueFixtureHero(RescuePartyFixtureName) != null)
            return "A companion-rescue fixture hero already exists; restore it before setup.";

        if (!TryResolvePlayer(args[0], out _, out var objectManager, out var player,
            out var playerHero, out var playerClan, out var playerParty, out error))
            return "Failed to set up companion-rescue fixture: " + error;

        var captorSettlement = Settlement.Find(RescueCaptorSettlementId);
        if (captorSettlement?.Party == null)
            return $"Failed to set up companion-rescue fixture: settlement '{RescueCaptorSettlementId}' has no party.";
        if (!objectManager.TryGetIdWithLogging(captorSettlement.Party, out var captorPartyId))
            return "Failed to set up companion-rescue fixture: Danustica's party is not registered.";

        var template = Hero.AllAliveHeroes.FirstOrDefault(hero => hero.IsWanderer && hero != playerHero);
        if (template == null)
            return "Failed to set up companion-rescue fixture: no living wanderer template is available.";

        int originalMemberCount = playerParty.MemberRoster.TotalManCount;
        int originalCompanionCount = playerClan.Companions.Count();
        int originalWarPartyCount = playerClan.WarPartyComponents.Count;
        int originalPlayerGold = playerHero.Gold;
        var joinCompanion = CreateFixtureCompanion(template, captorSettlement, RescueJoinFixtureName);
        var partyCompanion = CreateFixtureCompanion(template, captorSettlement, RescuePartyFixtureName);

        try
        {
            if (!objectManager.TryGetIdWithLogging(joinCompanion, out var joinCompanionId) ||
                !objectManager.TryGetIdWithLogging(partyCompanion, out var partyCompanionId))
                throw new InvalidOperationException("generated heroes were not registered.");

            AddCompanionAction.Apply(playerClan, joinCompanion);
            AddHeroToPartyAction.Apply(joinCompanion, playerParty, true);
            TakePrisonerAction.Apply(captorSettlement.Party, joinCompanion);

            AddCompanionAction.Apply(playerClan, partyCompanion);
            AddHeroToPartyAction.Apply(partyCompanion, playerParty, true);
            int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
            if (partyCompanion.Gold < partyGoldLowerThreshold)
                GiveGoldAction.ApplyBetweenCharacters(
                    null,
                    partyCompanion,
                    partyGoldLowerThreshold - partyCompanion.Gold,
                    false);
            TakePrisonerAction.Apply(captorSettlement.Party, partyCompanion);

            if (!joinCompanion.IsPrisoner || !partyCompanion.IsPrisoner ||
                joinCompanion.PartyBelongedToAsPrisoner != captorSettlement.Party ||
                partyCompanion.PartyBelongedToAsPrisoner != captorSettlement.Party)
                throw new InvalidOperationException("TakePrisonerAction did not establish the expected captivity state.");

            pendingRescueFixture = new CompanionRescueFixture(
                player.ControllerId,
                player.MobilePartyId,
                playerHero,
                joinCompanion,
                joinCompanionId,
                partyCompanion,
                partyCompanionId,
                originalMemberCount,
                originalCompanionCount,
                originalWarPartyCount,
                originalPlayerGold);
            StartRescueRequestObservation();

            return $"RESCUE_FIXTURE_READY controller={player.ControllerId} party={player.MobilePartyId} " +
                $"join={joinCompanionId} lead={partyCompanionId} captor={captorPartyId} " +
                $"members={originalMemberCount} companions={originalCompanionCount} warParties={originalWarPartyCount}";
        }
        catch (Exception exception)
        {
            StopRescueRequestObservation();
            CleanupRescueFixtureHero(playerParty, joinCompanion);
            CleanupRescueFixtureHero(playerParty, partyCompanion);
            return "Failed to set up companion-rescue fixture: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("rescue_fixture_open_conversation", "coop.debug.companions")]
    public static string RescueFixtureOpenConversationCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_open_conversation <heroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero companion)) return $"Hero '{args[0]}' not found.";
        if (!companion.IsPrisoner || companion.PartyBelongedToAsPrisoner == null)
            return $"Hero '{args[0]}' is not held as a prisoner.";
        if (companion.Clan != Clan.PlayerClan)
            return $"Hero '{args[0]}' is not a companion of the local player's clan.";
        if (PlayerEncounter.Current != null) return "A player encounter is already active.";
        if (Campaign.Current.ConversationManager.IsConversationInProgress)
            return "A conversation is already active.";

        try
        {
            Campaign.Current.CurrentConversationContext = ConversationContext.FreeOrCapturePrisonerHero;
            CampaignMapConversation.OpenConversation(
                new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true),
                new ConversationCharacterData(companion.CharacterObject,
                    companion.PartyBelongedToAsPrisoner, noHorse: true));
            if (Hero.OneToOneConversationHero != companion)
                throw new InvalidOperationException("the live conversation did not select the fixture companion.");

            return $"RESCUE_CONVERSATION_OPEN hero={args[0]} context={Campaign.Current.CurrentConversationContext} " +
                "conversationHeroMatched=True";
        }
        catch (Exception exception)
        {
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            return "Failed to open the live rescue conversation: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("rescue_fixture_join_twice", "coop.debug.companions")]
    public static string RescueFixtureJoinTwiceCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_join_twice <heroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero companion)) return $"Hero '{args[0]}' not found.";
        if (!Campaign.Current.ConversationManager.IsConversationInProgress ||
            Hero.OneToOneConversationHero != companion)
            return "The requested fixture companion rescue conversation is not active.";

        var behavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();
        if (behavior == null) return "CompanionRolesCampaignBehavior is unavailable.";

        try
        {
            behavior.companion_rescue_answer_options_join_party_consequence();
            behavior.companion_rescue_answer_options_join_party_consequence();
            Campaign.Current.ConversationManager.EndConversation();
            return $"RESCUE_JOIN_REQUESTS_SENT hero={args[0]} count=2";
        }
        catch (Exception exception)
        {
            if (Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            return "Failed to invoke the live join-party rescue consequence twice: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("rescue_fixture_open_lead_party", "coop.debug.companions")]
    public static string RescueFixtureOpenLeadPartyCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_open_lead_party <heroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero companion)) return $"Hero '{args[0]}' not found.";
        if (!Campaign.Current.ConversationManager.IsConversationInProgress ||
            Hero.OneToOneConversationHero != companion)
            return "The requested fixture companion rescue conversation is not active.";

        var behavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();
        if (behavior == null) return "CompanionRolesCampaignBehavior is unavailable.";

        try
        {
            StartRescuePartyScreenObservation(companion);
            behavior.companion_rescue_answer_options_lead_party_consequence();
            if (Game.Current?.GameStateManager?.ActiveState is PartyState)
                return $"RESCUE_LEAD_PARTY_SCREEN_OPEN hero={args[0]}";

            StopRescuePartyScreenObservation();
            return "The rescue party screen did not open.";
        }
        catch (Exception exception)
        {
            StopRescuePartyScreenObservation();
            return "Failed to open the live rescue party screen: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("rescue_fixture_replay_lead_party", "coop.debug.companions")]
    public static string RescueFixtureReplayLeadPartyCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_replay_lead_party <heroId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero companion)) return $"Hero '{args[0]}' not found.";
        if (PartyBase.MainParty == null) return "The local player party is unavailable.";
        if (!lastRescuePartyScreenCompletion.HasValue)
            return "No completed rescue party screen is available to replay.";

        var completion = lastRescuePartyScreenCompletion.Value;
        if (completion.RightOwnerParty != PartyBase.MainParty ||
            completion.LeftMemberRoster.GetTroopCount(companion.CharacterObject) != 1)
            return "The captured rescue party completion does not match the requested companion.";

        StopRescuePartyScreenObservation();
        lastRescuePartyScreenCompletion = null;
        MessageBroker.Instance.Publish(typeof(CompanionsCommands), completion);
        return $"RESCUE_LEAD_PARTY_REQUEST_REPLAYED hero={args[0]}";
    }

    [CommandLineArgumentFunction("rescue_fixture_state", "coop.debug.companions")]
    public static string RescueFixtureStateCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_state <partyId> <joinHeroId> <leadHeroId>";
        if (args.Count != 3) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty playerParty))
            return $"Party '{args[0]}' not found.";
        if (!objectManager.TryGetObject(args[1], out Hero joinCompanion))
            return $"Hero '{args[1]}' not found.";
        if (!objectManager.TryGetObject(args[2], out Hero partyCompanion))
            return $"Hero '{args[2]}' not found.";

        var clan = joinCompanion.CompanionOf ?? partyCompanion.CompanionOf;
        int fixturePartyCount = Campaign.Current.MobileParties.Count(party =>
            party != playerParty &&
            (party.LeaderHero == partyCompanion ||
             party.StringId == partyCompanion.CharacterObject.StringId));

        return $"RESCUE_FIXTURE_STATE party={args[0]} " +
            $"join.id={args[1]} join.count={playerParty.MemberRoster.GetTroopCount(joinCompanion.CharacterObject)} " +
            $"join.state={joinCompanion.HeroState} join.prisoner={joinCompanion.IsPrisoner} " +
            $"join.party={joinCompanion.PartyBelongedTo?.StringId ?? "none"} " +
            $"join.captive={GetPartyBaseId(objectManager, joinCompanion.PartyBelongedToAsPrisoner)} " +
            $"lead.id={args[2]} lead.count={playerParty.MemberRoster.GetTroopCount(partyCompanion.CharacterObject)} " +
            $"lead.state={partyCompanion.HeroState} lead.prisoner={partyCompanion.IsPrisoner} " +
            $"lead.party={partyCompanion.PartyBelongedTo?.StringId ?? "none"} " +
            $"lead.captive={GetPartyBaseId(objectManager, partyCompanion.PartyBelongedToAsPrisoner)} " +
            $"fixtureParties={fixturePartyCount} clanCompanions={clan?.Companions.Count() ?? -1} " +
            $"clanWarParties={clan?.WarPartyComponents.Count ?? -1} " +
            $"joinRequests={Volatile.Read(ref rescueJoinRequestCount)} " +
            $"leadRequests={Volatile.Read(ref rescuePartyRequestCount)}";
    }

    [CommandLineArgumentFunction("rescue_fixture_restore", "coop.debug.companions")]
    public static string RescueFixtureRestoreCommand(List<string> args)
    {
        const string usage = "Usage: coop.debug.companions.rescue_fixture_restore <controllerId>";
        var context = new CommandContext("rescue_fixture_restore", usage, args);
        if (!context.RequireServer(out var error)) return error;
        if (!context.RequireArgCount(1, out error)) return error;
        if (!TryResolvePlayer(args[0], out _, out _, out _, out _, out var playerClan,
            out var playerParty, out error))
            return "Failed to restore companion-rescue fixture: " + error;

        var joinCompanion = pendingRescueFixture?.JoinCompanion ??
            FindRescueFixtureHero(RescueJoinFixtureName);
        var partyCompanion = pendingRescueFixture?.PartyCompanion ??
            FindRescueFixtureHero(RescuePartyFixtureName);
        if (joinCompanion == null && partyCompanion == null)
            return "No companion-rescue fixture is active.";

        try
        {
            StopRescueRequestObservation();
            CleanupRescueFixtureHero(playerParty, joinCompanion);
            CleanupRescueFixtureHero(playerParty, partyCompanion);
            if (pendingRescueFixture != null)
                RestoreHeroGold(pendingRescueFixture.PlayerHero, pendingRescueFixture.OriginalPlayerGold);

            int fixturePartyCount = Campaign.Current.MobileParties.Count(party =>
                party != playerParty &&
                partyCompanion != null &&
                (party.LeaderHero == partyCompanion ||
                 party.StringId == partyCompanion.CharacterObject.StringId));
            int joinCount = joinCompanion == null
                ? 0
                : playerParty.MemberRoster.GetTroopCount(joinCompanion.CharacterObject);
            int partyCount = partyCompanion == null
                ? 0
                : playerParty.MemberRoster.GetTroopCount(partyCompanion.CharacterObject);
            int memberCount = playerParty.MemberRoster.TotalManCount;
            int companionCount = playerClan.Companions.Count();
            int warPartyCount = playerClan.WarPartyComponents.Count;
            int playerGold = pendingRescueFixture?.PlayerHero.Gold ?? -1;

            if (joinCount != 0 || partyCount != 0 || fixturePartyCount != 0 ||
                pendingRescueFixture != null &&
                (memberCount != pendingRescueFixture.OriginalMemberCount ||
                 companionCount != pendingRescueFixture.OriginalCompanionCount ||
                 warPartyCount != pendingRescueFixture.OriginalWarPartyCount ||
                 pendingRescueFixture.PlayerHero.Gold != pendingRescueFixture.OriginalPlayerGold))
            {
                return $"RESCUE_FIXTURE_RESTORE_FAILED join={joinCount} lead={partyCount} " +
                    $"fixtureParties={fixturePartyCount} members={memberCount} " +
                    $"companions={companionCount} warParties={warPartyCount} " +
                    $"playerGold={playerGold}";
            }

            pendingRescueFixture = null;
            return $"RESCUE_FIXTURE_RESTORED party={playerParty.StringId} join=0 lead=0 " +
                $"fixtureParties=0 members={memberCount} companions={companionCount} " +
                $"warParties={warPartyCount} playerGold={playerGold}";
        }
        catch (Exception exception)
        {
            return "Failed to restore companion-rescue fixture: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("open_party_screen", "coop.debug.companions")]
    public static string OpenPartyScreenCommand(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.companions.open_party_screen";
        if (Hero.MainHero?.PartyBelongedTo == null) return "The local player hero has no party.";

        PartyScreenHelper.OpenScreenAsNormal();
        return "PARTY_SCREEN_OPENED";
    }

    [CommandLineArgumentFunction("close_party_screen", "coop.debug.companions")]
    public static string ClosePartyScreenCommand(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.companions.close_party_screen";

        PartyScreenHelper.CloseScreen(true, true);
        return "PARTY_SCREEN_CLOSED";
    }

    [CommandLineArgumentFunction("commit_party_screen", "coop.debug.companions")]
    public static string CommitPartyScreenCommand(List<string> args)
    {
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return "Usage: coop.debug.companions.commit_party_screen";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState))
            return "No active party screen.";

        PartyScreenHelper.CloseScreen(false);
        return Game.Current?.GameStateManager?.ActiveState is PartyState
            ? "PARTY_SCREEN_COMMIT_REJECTED"
            : "PARTY_SCREEN_COMMITTED";
    }

    private static Hero CreateFixtureCompanion(Hero template,
        TaleWorlds.CampaignSystem.Settlements.Settlement homeSettlement, string name)
    {
        var hero = HeroCreator.CreateSpecialHero(template.CharacterObject, homeSettlement, age: 30);
        var heroName = new TextObject(name);
        hero.SetName(heroName, heroName);
        hero.SetNewOccupation(Occupation.Wanderer);
        return hero;
    }

    private static Hero FindRoleFixtureCompanion()
    {
        return Hero.AllAliveHeroes.FirstOrDefault(hero =>
            hero.Name?.ToString() == RoleFixtureName && hero.CompanionOf != null);
    }

    private static Hero FindRescueFixtureHero(string name)
    {
        return Hero.AllAliveHeroes.FirstOrDefault(hero => hero.Name?.ToString() == name);
    }

    private static string GetPartyBaseId(IObjectManager objectManager, PartyBase party)
    {
        if (party == null) return "none";
        return objectManager.TryGetId(party, out var partyId) ? partyId : "unregistered";
    }

    private static void StartRescueRequestObservation()
    {
        rescueJoinRequestCount = 0;
        rescuePartyRequestCount = 0;
        rescueJoinRequestObserver = _ => Interlocked.Increment(ref rescueJoinRequestCount);
        rescuePartyRequestObserver = _ => Interlocked.Increment(ref rescuePartyRequestCount);
        MessageBroker.Instance.Subscribe(rescueJoinRequestObserver);
        MessageBroker.Instance.Subscribe(rescuePartyRequestObserver);
    }

    private static void StopRescueRequestObservation()
    {
        if (rescueJoinRequestObserver != null)
        {
            MessageBroker.Instance.Unsubscribe(rescueJoinRequestObserver);
            rescueJoinRequestObserver = null;
        }
        if (rescuePartyRequestObserver != null)
        {
            MessageBroker.Instance.Unsubscribe(rescuePartyRequestObserver);
            rescuePartyRequestObserver = null;
        }
    }

    private static void StartRescuePartyScreenObservation(Hero companion)
    {
        StopRescuePartyScreenObservation();
        lastRescuePartyScreenCompletion = null;
        rescuePartyScreenObserver = payload =>
        {
            if (payload.What.RightOwnerParty == PartyBase.MainParty &&
                payload.What.LeftMemberRoster.GetTroopCount(companion.CharacterObject) == 1)
                lastRescuePartyScreenCompletion = payload.What;
        };
        MessageBroker.Instance.Subscribe(rescuePartyScreenObserver);
    }

    private static void StopRescuePartyScreenObservation()
    {
        if (rescuePartyScreenObserver == null) return;

        MessageBroker.Instance.Unsubscribe(rescuePartyScreenObserver);
        rescuePartyScreenObserver = null;
    }

    private static void RestoreHeroGold(Hero hero, int originalGold)
    {
        int difference = hero.Gold - originalGold;
        if (difference > 0)
            GiveGoldAction.ApplyBetweenCharacters(hero, null, difference, false);
        else if (difference < 0)
            GiveGoldAction.ApplyBetweenCharacters(null, hero, -difference, false);
    }

    private static void CleanupRescueFixtureHero(MobileParty playerParty, Hero companion)
    {
        if (companion == null) return;

        if (companion.IsPrisoner)
            EndCaptivityAction.ApplyByReleasedAfterBattle(companion);

        foreach (var fixtureParty in Campaign.Current.MobileParties
                     .Where(party => party != playerParty &&
                         (party.LeaderHero == companion ||
                          party.StringId == companion.CharacterObject.StringId))
                     .ToArray())
        {
            DestroyPartyAction.Apply(null, fixtureParty);
        }

        int count = playerParty.MemberRoster.GetTroopCount(companion.CharacterObject);
        if (count != 0)
            playerParty.MemberRoster.AddToCounts(companion.CharacterObject, -count, false, 0, 0, true);

        if (companion.CompanionOf != null)
            RemoveCompanionAction.ApplyByFire(companion.CompanionOf, companion);
        if (companion.HeroState != Hero.CharacterStates.Dead)
            KillCharacterAction.ApplyByRemove(companion, false, true);
    }

    private static FireCompanion CreateCleanupDismissalRequest(
        IObjectManager objectManager, Hero companion, string heroId)
    {
        if (!objectManager.TryGetIdWithLogging(companion.CompanionOf, out var clanId))
            throw new InvalidOperationException($"Could not resolve the owning clan for fixture hero '{heroId}'.");

        string partyId = null;
        if (companion.PartyBelongedTo != null &&
            !objectManager.TryGetIdWithLogging(companion.PartyBelongedTo, out partyId))
            throw new InvalidOperationException($"Could not resolve the party for fixture hero '{heroId}'.");

        return new FireCompanion(Guid.NewGuid().ToString("N"), heroId, clanId, partyId);
    }

    private static bool TryResolvePlayer(
        string controllerId,
        out IPlayerManager playerManager,
        out IObjectManager objectManager,
        out Player player,
        out Hero hero,
        out Clan clan,
        out MobileParty party,
        out string error)
    {
        playerManager = null;
        objectManager = null;
        player = null;
        hero = null;
        clan = null;
        party = null;
        error = null;

        if (!ContainerProvider.TryResolve(out playerManager) || !ContainerProvider.TryResolve(out objectManager))
        {
            error = "could not resolve player services.";
            return false;
        }
        if (!playerManager.TryGetPlayer(controllerId, out player))
        {
            error = $"no registered player has controller id '{controllerId}'.";
            return false;
        }
        if (!objectManager.TryGetObject(player.HeroId, out hero) ||
            !objectManager.TryGetObject(player.ClanId, out clan) ||
            !objectManager.TryGetObject(player.MobilePartyId, out party))
        {
            error = $"player '{controllerId}' has unresolved hero, clan, or party objects.";
            return false;
        }
        return true;
    }

    private static bool TryGetFixture(string controllerId, out CompanionDismissalFixture fixture, out string error)
    {
        fixture = pendingDismissalFixture;
        if (fixture == null)
        {
            error = "No companion-dismissal fixture is active.";
            return false;
        }
        if (fixture.ControllerId != controllerId)
        {
            error = $"The active companion-dismissal fixture belongs to '{fixture.ControllerId}'.";
            return false;
        }
        error = null;
        return true;
    }

    private static string FormatHeroState(string label, MobileParty party, Hero hero)
    {
        return $"{label}.id={hero.StringId} {label}.count={party.MemberRoster.GetTroopCount(hero.CharacterObject)} " +
            $"{label}.state={hero.HeroState} {label}.companion={(hero.CompanionOf?.StringId ?? "none")} " +
            $"{label}.party={(hero.PartyBelongedTo?.StringId ?? "none")}";
    }

    private sealed class CompanionRoleFixture
    {
        public string ControllerId { get; }
        public string PlayerPartyId { get; }
        public Hero RoleCompanion { get; }
        public string RoleCompanionId { get; }
        public Hero OriginalScout { get; }
        public int OriginalMemberCount { get; }
        public int OriginalCompanionCount { get; }

        public CompanionRoleFixture(string controllerId, string playerPartyId,
            Hero roleCompanion, string roleCompanionId, Hero originalScout,
            int originalMemberCount, int originalCompanionCount)
        {
            ControllerId = controllerId;
            PlayerPartyId = playerPartyId;
            RoleCompanion = roleCompanion;
            RoleCompanionId = roleCompanionId;
            OriginalScout = originalScout;
            OriginalMemberCount = originalMemberCount;
            OriginalCompanionCount = originalCompanionCount;
        }
    }

    private sealed class CompanionDismissalFixture
    {
        public string ControllerId { get; }
        public string PlayerHeroId { get; }
        public string PlayerClanId { get; }
        public string PlayerPartyId { get; }
        public Hero Dismissed { get; }
        public string DismissedHeroId { get; }
        public Hero Replacement { get; }
        public string ReplacementHeroId { get; }
        public int OriginalMemberCount { get; }
        public int OriginalCompanionCount { get; }

        public CompanionDismissalFixture(string controllerId, string playerHeroId, string playerClanId,
            string playerPartyId, Hero dismissed, string dismissedHeroId, Hero replacement,
            string replacementHeroId, int originalMemberCount, int originalCompanionCount)
        {
            ControllerId = controllerId;
            PlayerHeroId = playerHeroId;
            PlayerClanId = playerClanId;
            PlayerPartyId = playerPartyId;
            Dismissed = dismissed;
            DismissedHeroId = dismissedHeroId;
            Replacement = replacement;
            ReplacementHeroId = replacementHeroId;
            OriginalMemberCount = originalMemberCount;
            OriginalCompanionCount = originalCompanionCount;
        }
    }

    private sealed class CompanionRescueFixture
    {
        public string ControllerId { get; }
        public string PlayerPartyId { get; }
        public Hero PlayerHero { get; }
        public Hero JoinCompanion { get; }
        public string JoinCompanionId { get; }
        public Hero PartyCompanion { get; }
        public string PartyCompanionId { get; }
        public int OriginalMemberCount { get; }
        public int OriginalCompanionCount { get; }
        public int OriginalWarPartyCount { get; }
        public int OriginalPlayerGold { get; }

        public CompanionRescueFixture(
            string controllerId,
            string playerPartyId,
            Hero playerHero,
            Hero joinCompanion,
            string joinCompanionId,
            Hero partyCompanion,
            string partyCompanionId,
            int originalMemberCount,
            int originalCompanionCount,
            int originalWarPartyCount,
            int originalPlayerGold)
        {
            ControllerId = controllerId;
            PlayerPartyId = playerPartyId;
            PlayerHero = playerHero;
            JoinCompanion = joinCompanion;
            JoinCompanionId = joinCompanionId;
            PartyCompanion = partyCompanion;
            PartyCompanionId = partyCompanionId;
            OriginalMemberCount = originalMemberCount;
            OriginalCompanionCount = originalCompanionCount;
            OriginalWarPartyCount = originalWarPartyCount;
            OriginalPlayerGold = originalPlayerGold;
        }
    }

    private sealed class DismissalEncounterObservation
    {
        public bool EncounterActiveAtTrigger { get; set; }
        public bool ConversationHeroMatched { get; set; }
        public bool LeaveBeforeConsequence { get; set; }
        public bool LeaveAfterConsequence { get; set; }
        public bool EncounterActiveAtCompletion { get; set; }
        public bool LeaveAtCompletion { get; set; }
    }
}
