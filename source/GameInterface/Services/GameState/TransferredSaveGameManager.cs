using GameInterface.Services.GameState.Interfaces;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.GameState;

/// <summary>
/// Starts a save the server sent over the network and clears its forced party visibility once the save is loaded.
/// </summary>
internal sealed class TransferredSaveGameManager : SandBoxGameManager
{
    public TransferredSaveGameManager(LoadResult loadResult) : base(loadResult)
    {
    }

    public override void OnGameLoaded(Game game, object initializerObject)
    {
        base.OnGameLoaded(game, initializerObject);
        GameStateInterface.ClearTransferredPartyVisibility((Campaign)game.GameType);
    }
}
