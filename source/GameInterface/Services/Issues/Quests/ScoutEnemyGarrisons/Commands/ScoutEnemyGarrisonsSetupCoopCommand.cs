using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Commands;

internal sealed class ScoutEnemyGarrisonsSetupCoopCommand : ICoopCommand
{
    internal const string RhagaeaId = "lord_1_14";
    private const int RequiredClanTier = 2;
    private const int RequiredScouting = 30;

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomMembershipState kingdomMembershipState;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;
    private readonly ScoutEnemyGarrisonsCreationCaptureStrategy creationStrategy;

    public ScoutEnemyGarrisonsSetupCoopCommand(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomMembershipState kingdomMembershipState,
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ScoutEnemyGarrisonsCreationCaptureStrategy creationStrategy)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.kingdomMembershipState = kingdomMembershipState;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
        this.creationStrategy = creationStrategy;
    }

    public string Prefix => "coop.debug.issues.scout_enemy_garrisons";

    public string Name => "setup";

    public string Description => "Lets a player take Scout Enemy Garrisons from a lord (Rhagaea by default): joins the lord's kingdom, reaches clan tier 2 and Scouting 30, gives the issue and teleports the player to the lord.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("controller_id", "The controller id of the player who will take the quest, see coop.debug.players.list."),
        new ExpectedArgs("hero_id", "The lord who offers the issue, Rhagaea (lord_1_14) by default.", isRequired: false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!playerManager.TryGetPlayer(args[0], out var player))
        {
            return Failed($"Player with controller id '{args[0]}' not found");
        }

        if (!objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero) ||
            !objectManager.TryGetObject<Clan>(player.ClanId, out var playerClan) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return Failed($"The hero, clan or party of player '{args[0]}' was not found");
        }

        if (playerParty.CurrentSettlement != null || playerParty.MapEvent != null || playerParty.SiegeEvent != null || playerParty.Army != null)
        {
            return Failed($"{playerHero.Name} must leave the settlement, battle, siege or army before setup");
        }

        var lordId = args.Count > 1 ? args[1] : RhagaeaId;
        if (!objectManager.TryGetObject<Hero>(lordId, out var lord))
        {
            return Failed($"Hero '{lordId}' not found");
        }

        if (!TryCheckLord(lord, out var kingdom, out var error))
        {
            return Failed(error);
        }

        if (playerClan.Kingdom != kingdom)
        {
            kingdomMembershipState.MoveClanToKingdom(playerClan.Kingdom, kingdom, playerClan, publishCollectionChanges: true);
        }

        if (playerClan.Kingdom != kingdom)
        {
            return Failed($"{playerClan.Name} could not join {kingdom.Name}");
        }

        if (playerClan.Tier < RequiredClanTier)
        {
            playerClan.AddRenown(Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(RequiredClanTier) - playerClan.Renown);
        }

        var scouting = playerHero.GetSkillValue(DefaultSkills.Scouting);
        if (scouting < RequiredScouting)
        {
            playerHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Scouting, RequiredScouting - scouting, false);
        }

        if (!(lord.Issue is Issue) && !TryGiveIssue(lord, out error))
        {
            return Failed(error);
        }

        if (!TryTeleport(playerParty, lord.PartyBelongedTo))
        {
            return Failed($"Unable to read the movement state of {playerHero.Name}'s party, it was not teleported");
        }

        return new CoopCommandResult(true, Report(playerHero, lord, (Issue)lord.Issue));
    }

    private static bool TryCheckLord(Hero lord, out Kingdom kingdom, out string error)
    {
        kingdom = lord.MapFaction as Kingdom;
        error = null;

        if (!lord.IsLord || !lord.IsActive)
        {
            error = $"{lord.Name} is not an active lord";
        }
        else if (lord.IsPlayerHero() || lord.Clan.IsPlayerClan())
        {
            error = $"{lord.Name} is a player or in a player's clan, players cannot offer issues";
        }
        else if (kingdom == null)
        {
            error = $"{lord.Name} is not in a kingdom";
        }
        else if (lord.PartyBelongedTo?.LeaderHero != lord)
        {
            error = $"{lord.Name} does not lead a party";
        }
        else if (lord.Issue != null && !(lord.Issue is Issue))
        {
            error = $"{lord.Name} already has the issue {lord.Issue.GetType().Name}";
        }

        return error == null;
    }

    private bool TryGiveIssue(Hero lord, out string error)
    {
        error = null;

        if (!creationStrategy.TryBuildDebugCapture(lord, out var captured))
        {
            error = $"{lord.MapFaction.Name} is not at war with anyone who holds three fortifications";
            return false;
        }

        var potentialIssue = new PotentialIssueData(
            (in PotentialIssueData _, Hero owner) => creationStrategy.CreateIssue(owner, captured),
            typeof(Issue),
            IssueBase.IssueFrequency.Common);

        try
        {
            Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, lord);
        }
        catch (Exception e)
        {
            lord.Issue?.IssueFinalized();

            error = $"Creating {nameof(Issue)} failed: {e.Message}";
            return false;
        }

        if (!(lord.Issue is Issue))
        {
            error = $"{lord.Name} was not given {nameof(Issue)}";
            return false;
        }

        return true;
    }

    private bool TryTeleport(MobileParty playerParty, MobileParty lordParty)
    {
        if (!behaviorSnapshot.TryCreate(playerParty, out var data))
        {
            return false;
        }

        data.PartyPosition = lordParty.Position;
        data.ForcePosition = true;
        data.IsCurrentlyAtSea = lordParty.IsCurrentlyAtSea;
        data.ResetMovementToHold = true;
        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));

        return true;
    }

    private static string Report(Hero playerHero, Hero lord, Issue issue)
    {
        var report = new StringBuilder();
        report.AppendLine($"{lord.Name} offers {nameof(Issue)} to {playerHero.Name}, who was teleported to {lord.Name}.");
        report.AppendLine($"Targets: {issue._settlement1.StringId} ({issue._settlement1.Name}), {issue._settlement2.StringId} ({issue._settlement2.Name}), {issue._settlement3.StringId} ({issue._settlement3.Name})");

        var relation = lord.GetRelation(playerHero);
        if (relation < -10)
        {
            report.AppendLine($"Warning: {playerHero.Name} and {lord.Name} have relation {relation}, the quest needs -10 or more");
        }

        if (playerHero.IsKingdomLeader)
        {
            report.AppendLine($"Warning: {playerHero.Name} rules a kingdom, rulers cannot take the quest");
        }

        return report.ToString();
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
