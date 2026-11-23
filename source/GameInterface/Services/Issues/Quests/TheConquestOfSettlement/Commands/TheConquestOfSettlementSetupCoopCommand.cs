using Common.Commands;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement.Commands;

internal sealed class TheConquestOfSettlementSetupCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomMembershipState kingdomMembershipState;
    private readonly IIssueConversationTracker conversationTracker;
    private readonly TheConquestOfSettlementCreationCaptureStrategy creationStrategy;

    public TheConquestOfSettlementSetupCoopCommand(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomMembershipState kingdomMembershipState,
        IIssueConversationTracker conversationTracker,
        TheConquestOfSettlementCreationCaptureStrategy creationStrategy)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.kingdomMembershipState = kingdomMembershipState;
        this.conversationTracker = conversationTracker;
        this.creationStrategy = creationStrategy;
    }

    public string Prefix => "coop.debug.issues.the_conquest_of_settlement";

    public string Name => "setup";

    public string Description => "Lets a player take The Conquest of Settlement without meeting the ruler: joins a ruler's kingdom, gives the ruler the issue and counts the player as having talked to them. Then run accept on that player's client.";

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
            !objectManager.TryGetObject<Clan>(player.ClanId, out var playerClan))
        {
            return Failed($"The hero or clan of player '{args[0]}' was not found");
        }

        if (playerHero.IsKingdomLeader)
        {
            return Failed($"{playerHero.Name} rules a kingdom and cannot join another one");
        }

        var ruler = FindRuler(playerClan);
        if (ruler == null)
        {
            return Failed("No kingdom ruler can offer the quest, a ruler needs a fortification and a war with a fief holder");
        }

        var kingdom = (Kingdom)ruler.MapFaction;
        if (playerClan.Kingdom != kingdom)
        {
            kingdomMembershipState.MoveClanToKingdom(playerClan.Kingdom, kingdom, playerClan, publishCollectionChanges: true);
        }

        if (playerClan.Kingdom != kingdom)
        {
            return Failed($"{playerClan.Name} could not join {kingdom.Name}");
        }

        if (!(ruler.Issue is Issue) && !TryGiveIssue(ruler, out var error))
        {
            return Failed(error);
        }

        if (!objectManager.TryGetId(ruler, out var rulerId))
        {
            return Failed($"{ruler.Name} has no registered id");
        }

        // Stands in for meeting the ruler, which the server checks before it lets a player take this quest
        conversationTracker.Register(rulerId, player.ControllerId, ruler.Issue.StringId);

        var output = $"{ruler.Name} ({rulerId}) of {kingdom.Name} offers The Conquest of Settlement to {playerHero.Name}. " +
            $"On that player's client run coop.debug.issues.the_conquest_of_settlement.accept {rulerId}";

        var relation = ruler.GetRelation(playerHero);
        if (relation < -10)
        {
            output += $"{Environment.NewLine}Warning: {playerHero.Name} and {ruler.Name} have relation {relation}, the quest needs -10 or more";
        }

        return new CoopCommandResult(true, output);
    }

    private static Hero FindRuler(Clan playerClan)
    {
        var current = playerClan.Kingdom?.Leader;
        if (CanOffer(current))
        {
            return current;
        }

        return Kingdom.All.Select(kingdom => kingdom.Leader).FirstOrDefault(CanOffer);
    }

    private static bool CanOffer(Hero ruler)
    {
        return ruler != null &&
            ruler.IsLord &&
            ruler.IsActive &&
            !ruler.IsPrisoner &&
            !ruler.IsPlayerHero() &&
            !ruler.Clan.IsPlayerClan() &&
            ruler.MapFaction is Kingdom kingdom &&
            ruler.Clan.Settlements.Any(settlement => settlement.IsFortification) &&
            kingdom.FactionsAtWarWith.Any(faction => faction.Fiefs.Any()) &&
            (ruler.Issue == null || ruler.Issue is Issue);
    }

    private bool TryGiveIssue(Hero ruler, out string error)
    {
        error = null;

        if (!creationStrategy.TryBuildDebugCapture(ruler, out var captured))
        {
            error = $"No default values could be found for The Conquest of Settlement";
            return false;
        }

        var potentialIssue = new PotentialIssueData(
            (in PotentialIssueData _, Hero owner) => creationStrategy.CreateIssue(owner, captured),
            typeof(Issue),
            IssueBase.IssueFrequency.Common);

        try
        {
            Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, ruler);
        }
        catch (Exception e)
        {
            ruler.Issue?.IssueFinalized();

            error = $"Creating The Conquest of Settlement failed: {e.Message}";
            return false;
        }

        if (!(ruler.Issue is Issue))
        {
            error = $"{ruler.Name} was not given The Conquest of Settlement";
            return false;
        }

        return true;
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
