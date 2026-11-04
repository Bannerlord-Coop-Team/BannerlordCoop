using Common.Commands;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Utils.Commands;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Commands;

internal sealed class ScoutEnemyGarrisonsGotoCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;
    private readonly ICommandObjectResolver resolver;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly IMobilePartyBehaviorSnapshot behaviorSnapshot;

    public ScoutEnemyGarrisonsGotoCoopCommand(
        IObjectManager objectManager,
        ICommandObjectResolver resolver,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        this.objectManager = objectManager;
        this.resolver = resolver;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.behaviorSnapshot = behaviorSnapshot;
    }

    public string Prefix => "coop.debug.issues.scout_enemy_garrisons";

    public string Name => "goto";

    public string Description => "Teleports a player's party to a settlement's gate and holds it there, close enough to scout it.";

    public CoopCommandSide Side => CoopCommandSide.Server;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("controller_id", "The controller id of the player to teleport, see coop.debug.players.list."),
        new ExpectedArgs("settlement_id", "Name or id of the settlement to scout, setup lists the targets, for example town_ES1 or Danustica; quote names with spaces."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return Failed($"The party of player '{args[0]}' was not found");
        }

        if (!resolver.TryResolveSettlement(args[1], out var settlement, out var error))
        {
            return Failed(error);
        }

        if (playerParty.CurrentSettlement != null || playerParty.MapEvent != null || playerParty.SiegeEvent != null || playerParty.Army != null)
        {
            return Failed($"{playerParty.Name} must leave the settlement, battle, siege or army before teleporting");
        }

        if (!behaviorSnapshot.TryCreate(playerParty, out var data))
        {
            return Failed($"Unable to read the movement state of {playerParty.Name}");
        }

        data.PartyPosition = settlement.GatePosition;
        data.ForcePosition = true;
        data.IsCurrentlyAtSea = false;
        data.ResetMovementToHold = true;
        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));

        return new CoopCommandResult(true, $"{playerParty.Name} was teleported to the gate of {settlement.Name}");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
