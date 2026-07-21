using Common;
using Common.Logging;
using GameInterface.Services.Clans.Extensions;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

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
                        if (!TryReleaseForeignPlayerCompanion(playerEncounter))
                        {
                            playerEncounter.DoFreeOrCapturePrisonerHeroes();
                        }
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

    private static bool TryReleaseForeignPlayerCompanion(PlayerEncounter playerEncounter)
    {
        if (playerEncounter._capturedAlreadyPrisonerHeroes == null)
        {
            playerEncounter._capturedAlreadyPrisonerHeroes = playerEncounter.RosterToReceiveLootMembers
                .RemoveIf(element => element.Character.IsHero &&
                                     element.Character.HeroObject.PartyBelongedToAsPrisoner != PartyBase.MainParty)
                .ToList();
        }

        var element = playerEncounter._capturedAlreadyPrisonerHeroes.LastOrDefault(candidate =>
            candidate.Character?.HeroObject is Hero hero &&
            hero.IsPrisoner &&
            hero.PartyBelongedToAsPrisoner != PartyBase.MainParty &&
            ShouldReleaseWithoutConversation(hero, Clan.PlayerClan));

        var companion = element.Character?.HeroObject;
        if (companion == null) return false;

        playerEncounter._capturedAlreadyPrisonerHeroes.Remove(element);
        EndCaptivityAction.ApplyByReleasedAfterBattle(companion);
        return true;
    }

    internal static bool ShouldReleaseWithoutConversation(Hero hero, Clan localPlayerClan) =>
        hero?.CompanionOf != null &&
        hero.CompanionOf != localPlayerClan &&
        hero.CompanionOf.IsPlayerClan();

    private void EndPlayerEncounter(PlayerEncounter playerEncounter)
    {
        //playerEncounter.DoEnd(); // Default implementation's function. Might have some logic we need later
        playerEncounter._stateHandled = true;
        PlayerEncounter.Finish(true);
    }
}
