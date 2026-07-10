using Common;
using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.MapEvents.Interfaces;

public interface IPlayerEncounterInterface : IGameAbstraction
{
    public void UpdateInternalAfterBattle(PlayerEncounter playerEncounter);
}

public class PlayerEncounterInterface : IPlayerEncounterInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerEncounterInterface>();

    public void UpdateInternalAfterBattle(PlayerEncounter playerEncounter)
    {
        GameThread.RunSafe(() =>
        {
            playerEncounter._stateHandled = false;
            while (!playerEncounter._stateHandled)
            {
                if (PlayerEncounter.Current._leaveEncounter)
                {
                    playerEncounter._stateHandled = true;
                    break;
                }

                switch (playerEncounter.EncounterState)
                {
                    case PlayerEncounterState.PlayerVictory:
                        playerEncounter.DoPlayerVictory();
                        break;
                    case PlayerEncounterState.PlayerTotalDefeat: // Player defeats handled elsewhere
                        playerEncounter.EncounterState = PlayerEncounterState.End;
                        break;
                    case PlayerEncounterState.CaptureHeroes:
                        playerEncounter.DoCaptureHeroes();
                        break;
                    case PlayerEncounterState.FreeHeroes:
                        playerEncounter.DoFreeOrCapturePrisonerHeroes();
                        break;
                    case PlayerEncounterState.LootParty:
                        playerEncounter.DoLootMembersAndPrisonersOfParty();
                        break;
                    case PlayerEncounterState.LootInventory:
                        playerEncounter.DoLootInventory();
                        break;
                    case PlayerEncounterState.LootShips:
                        playerEncounter.DoLootShips();
                        break;
                    case PlayerEncounterState.End:
                        EndPlayerEncounter(playerEncounter);
                        break;
                    default:
                        break;
                }
            }
        });
    }

    private void EndPlayerEncounter(PlayerEncounter playerEncounter)
    {
        //playerEncounter.DoEnd(); // Default implementation's function. Might have some logic we need later
        playerEncounter._stateHandled = true;
        PlayerEncounter.Finish(true);
    }
}