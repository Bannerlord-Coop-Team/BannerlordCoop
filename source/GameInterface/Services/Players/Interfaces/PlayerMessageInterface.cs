using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Players.Interfaces;

/// <summary>
/// Server-only: shows messages on the client of the player who controls a controlled object.
/// </summary>
public interface IPlayerMessageInterface : IGameAbstraction
{
    /// <summary>
    /// Shows an information message to the player controlling <paramref name="playerHero"/>.
    /// </summary>
    void SendInformationMessageToHero(Hero playerHero, string message);
}

public class PlayerMessageInterface : IPlayerMessageInterface
{
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public PlayerMessageInterface(INetwork network, IPlayerManager playerManager)
    {
        this.network = network;
        this.playerManager = playerManager;
    }

    public void SendInformationMessageToHero(Hero playerHero, string message)
    {
        if (!PlayerManager.TryGetControlledObjectInfo(playerHero, out var controller)) return;
        if (!playerManager.TryGetPeer(controller.ObjectControllerId, out var peer)) return;

        network.Send(peer, new SendInformationMessage(message));
    }
}
