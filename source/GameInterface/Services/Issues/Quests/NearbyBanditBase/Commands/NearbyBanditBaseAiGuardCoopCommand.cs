using Common.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Commands;

internal sealed class NearbyBanditBaseAiGuardCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public NearbyBanditBaseAiGuardCoopCommand(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public string Prefix => "coop.debug.issues.nearby_bandit_base";

    public string Name => "ai_guard";

    public string Description => "Prints whether an AI lord may solve a headman's Nearby Bandit Base issue. With a controller id it first takes that player prisoner into the issue's hideout.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("headman_id", "The headman offering the issue, printed by coop.debug.issues.nearby_bandit_base.setup."),
        new ExpectedArgs("controller_id", "Optional controller id of a player to take prisoner into the hideout, see coop.debug.players.list.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(args[0], out var headman))
        {
            return Failed($"Hero '{args[0]}' not found");
        }

        if (!(headman.Issue is Issue issue))
        {
            return Failed($"{headman.Name} has no Nearby Bandit Base issue");
        }

        var hideout = issue.TargetHideout;
        var taken = string.Empty;
        if (args.Count > 1)
        {
            if (!playerManager.TryGetPlayer(args[1], out var player) ||
                !objectManager.TryGetObject<Hero>(player.HeroId, out var playerHero))
            {
                return Failed($"The hero of player '{args[1]}' was not found");
            }

            if (playerHero.IsPrisoner)
            {
                return Failed($"{playerHero.Name} is already a prisoner");
            }

            TakePrisonerAction.Apply(hideout.Party, playerHero);
            taken = $"{playerHero.Name} was taken prisoner into {hideout.Name}. ";
        }

        return new CoopCommandResult(true, $"{taken}CanBeCompletedByAI for {headman.Name}'s issue at {hideout.Name}: {issue.CanBeCompletedByAI()}");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
