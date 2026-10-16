using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Commands;

internal sealed class LandLordCompanyOfTroubleSetupCoopCommand : ICoopCommand
{
    internal const string RhagaeaId = "lord_1_14";

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;
    private readonly LandLordCompanyOfTroubleCreationCaptureStrategy creationStrategy;

    public LandLordCompanyOfTroubleSetupCoopCommand(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        LandLordCompanyOfTroubleCreationCaptureStrategy creationStrategy)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
        this.creationStrategy = creationStrategy;
    }

    public string Prefix => "coop.debug.issues.company_of_trouble";

    public string Name => "setup";

    public string Description => "Lets a player take Company of Trouble from Rhagaea: reaches clan tier 1, gives her the issue and teleports the player to her.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("controller_id", "The controller id of the player who will take the quest, see coop.debug.players.list."),
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

        if (!objectManager.TryGetObject<Hero>(RhagaeaId, out var lord))
        {
            return Failed($"Rhagaea ({RhagaeaId}) not found");
        }

        if (!TryCheckLord(lord, out var error))
        {
            return Failed(error);
        }

        if (playerClan.Tier < 1)
        {
            playerClan.AddRenown(Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(1) - playerClan.Renown);
        }

        if (!(lord.Issue is Issue) && !TryGiveIssue(lord, out error))
        {
            return Failed(error);
        }

        if (!TryTeleport(playerParty, lord.PartyBelongedTo))
        {
            return Failed($"Unable to read the movement state of {playerHero.Name}'s party, it was not teleported");
        }

        return new CoopCommandResult(true, Report(playerHero, playerParty, lord, (Issue)lord.Issue));
    }

    private static bool TryCheckLord(Hero lord, out string error)
    {
        error = null;

        if (!lord.IsLord || !lord.IsActive)
        {
            error = $"{lord.Name} is not an active lord";
        }
        else if (lord.IsPlayerHero() || lord.Clan.IsPlayerClan())
        {
            error = $"{lord.Name} is a player or in a player's clan, players cannot offer issues";
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
            error = $"No default values could be found for {nameof(Issue)}";
            return false;
        }

        var potentialIssue = new PotentialIssueData(
            (in PotentialIssueData _, Hero owner) => creationStrategy.CreateIssue(owner, captured),
            typeof(Issue),
            IssueBase.IssueFrequency.Rare);

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

    private static string Report(Hero playerHero, MobileParty playerParty, Hero lord, Issue issue)
    {
        var report = new StringBuilder();
        report.AppendLine($"{lord.Name} offers {nameof(Issue)} to {playerHero.Name}. {playerHero.Name} was teleported to her.");

        var relation = lord.GetRelation(playerHero);
        if (relation < -10)
        {
            report.AppendLine($"Warning: {playerHero.Name} and {lord.Name} have relation {relation}, the quest needs -10 or more");
        }

        if (lord.MapFaction.IsAtWarWith(playerHero.MapFaction))
        {
            report.AppendLine($"Warning: {playerHero.Name} is at war with {lord.Name}");
        }

        var troopCount = issue.CompanyTroopCount;
        var partySize = playerParty.MemberRoster.TotalManCount;
        if (partySize < troopCount)
        {
            report.AppendLine($"Warning: {playerHero.Name}'s party has {partySize} men, the quest needs about {troopCount}");
        }

        if (partySize + troopCount > playerParty.Party.PartySizeLimit)
        {
            report.AppendLine($"Warning: {playerHero.Name}'s party has no room for about {troopCount} mercenaries, its limit is {playerParty.Party.PartySizeLimit}");
        }

        return report.ToString();
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
