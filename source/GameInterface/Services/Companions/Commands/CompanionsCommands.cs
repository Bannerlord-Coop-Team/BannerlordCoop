using Autofac;
using Common;
using Common.Messaging;
using Common.Logging;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Utils.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Helpers;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Companions.Commands;

internal class CompanionsCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<CompanionsCommands>();
    private static CompanionDismissalFixture pendingDismissalFixture;
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
    [CommandLineArgumentFunction("listwanderers", "coop.debug.companions")]
    public static string ListWanderersCommand(List<string> strings)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.IsWanderer)
            {
                stringBuilder.AppendLine(hero.CurrentSettlement + " (" + hero.Name.ToString() + ")");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Hero not found.";
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

    private static Hero CreateFixtureCompanion(Hero template,
        TaleWorlds.CampaignSystem.Settlements.Settlement homeSettlement, string name)
    {
        var hero = HeroCreator.CreateSpecialHero(template.CharacterObject, homeSettlement, age: 30);
        var heroName = new TextObject(name);
        hero.SetName(heroName, heroName);
        hero.SetNewOccupation(Occupation.Wanderer);
        return hero;
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
