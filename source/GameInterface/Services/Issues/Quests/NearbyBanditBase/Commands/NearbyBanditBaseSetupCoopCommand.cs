using Common.Commands;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Commands;

internal sealed class NearbyBanditBaseSetupCoopCommand : ICoopCommand
{
    private const string DanusticaId = "town_ES1";

    private const float MaxSecurityForTheIssue = 50f;

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;
    private readonly NearbyBanditBaseCreationCaptureStrategy creationStrategy;

    public NearbyBanditBaseSetupCoopCommand(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        NearbyBanditBaseCreationCaptureStrategy creationStrategy)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
        this.creationStrategy = creationStrategy;
    }

    public string Prefix => "coop.debug.issues.nearby_bandit_base";

    public string Name => "setup";

    public string Description => "Lets a player take Nearby Bandit Base from a headman of a village bound to Danustica: lowers the town's security, gives the headman the issue with the nearest infested hideout and teleports the player to the village.";

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
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return Failed($"The hero or party of player '{args[0]}' was not found");
        }

        if (playerParty.CurrentSettlement != null || playerParty.MapEvent != null || playerParty.SiegeEvent != null || playerParty.Army != null)
        {
            return Failed($"{playerHero.Name} must leave the settlement, battle, siege or army before setup");
        }

        if (!objectManager.TryGetObject<Settlement>(DanusticaId, out var danustica) || danustica.Town == null)
        {
            return Failed($"Danustica ({DanusticaId}) not found");
        }

        var village = danustica.BoundVillages
            .Select(boundVillage => boundVillage.Settlement)
            .FirstOrDefault(settlement => settlement.Notables.Any(notable => notable.IsHeadman));
        if (village == null)
        {
            return Failed($"No village bound to {danustica.Name} has a headman");
        }

        var headman = village.Notables.First(notable => notable.IsHeadman);
        if (headman.Issue != null && !(headman.Issue is Issue))
        {
            return Failed($"{headman.Name} already has the issue {headman.Issue.GetType().Name}");
        }

        if (danustica.Town.Security > MaxSecurityForTheIssue)
        {
            danustica.Town.Security = MaxSecurityForTheIssue;
        }

        if (!(headman.Issue is Issue) && !TryGiveIssue(headman, out var error))
        {
            return Failed(error);
        }

        if (!TryTeleport(playerParty, village))
        {
            return Failed($"Unable to read the movement state of {playerHero.Name}'s party, it was not teleported");
        }

        return new CoopCommandResult(true, Report(playerHero, playerParty, headman, village, (Issue)headman.Issue));
    }

    private bool TryGiveIssue(Hero headman, out string error)
    {
        error = null;

        if (!creationStrategy.TryBuildDebugCapture(headman, out var captured))
        {
            error = $"No infested hideout was found for {headman.Name}";
            return false;
        }

        var potentialIssue = new PotentialIssueData(
            (in PotentialIssueData _, Hero owner) => creationStrategy.CreateIssue(owner, captured),
            typeof(Issue),
            IssueBase.IssueFrequency.VeryCommon);

        try
        {
            Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, headman);
        }
        catch (Exception e)
        {
            headman.Issue?.IssueFinalized();

            error = $"Creating {nameof(Issue)} failed: {e.Message}";
            return false;
        }

        if (!(headman.Issue is Issue))
        {
            error = $"{headman.Name} was not given {nameof(Issue)}";
            return false;
        }

        return true;
    }

    private bool TryTeleport(MobileParty playerParty, Settlement village)
    {
        if (!behaviorSnapshot.TryCreate(playerParty, out var data))
        {
            return false;
        }

        data.PartyPosition = village.GatePosition;
        data.ForcePosition = true;
        data.IsCurrentlyAtSea = false;
        data.ResetMovementToHold = true;
        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));

        return true;
    }

    private string Report(Hero playerHero, MobileParty playerParty, Hero headman, Settlement village, Issue issue)
    {
        objectManager.TryGetId(headman, out var headmanId);
        objectManager.TryGetId(village, out var villageId);
        objectManager.TryGetId(issue.TargetHideout, out var hideoutId);

        var report = new StringBuilder();
        report.AppendLine($"{headman.Name} ({headmanId}) in {village.Name} ({villageId}) offers {nameof(Issue)} to {playerHero.Name} for the hideout {hideoutId}. {playerHero.Name} was teleported to the village.");

        var relation = headman.GetRelation(playerHero);
        if (relation < -10)
        {
            report.AppendLine($"Warning: {playerHero.Name} and {headman.Name} have relation {relation}, the quest needs -10 or more");
        }

        if (FactionManager.IsAtWarAgainstFaction(headman.MapFaction, playerHero.MapFaction))
        {
            report.AppendLine($"Warning: {playerHero.Name} is at war with {headman.MapFaction.Name}, the quest cannot be taken");
        }

        var healthyMen = playerParty.MemberRoster.TotalHealthyCount - 1;
        if (healthyMen < 25)
        {
            report.AppendLine($"Warning: {playerHero.Name} has {healthyMen} healthy men besides themselves, the quest needs 25");
        }

        return report.ToString();
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
