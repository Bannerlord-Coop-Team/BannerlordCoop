using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
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
    public void ReleaseHeroesWithoutConversation(PlayerEncounter playerEncounter);

    /// <summary>[Game thread] Whether this client sits in the encounter meeting menu with its conversation open.</summary>
    public bool IsEncounterMeetingConversationOpen();

    /// <summary>
    /// [Game thread] Ends the open encounter meeting conversation as the player's answer does, so vanilla moves on to the battle;
    /// false with the reason when no such conversation is open.
    /// </summary>
    public bool TryCompleteEncounterMeeting(out string failure);
}

/// <summary>Runs vanilla <see cref="PlayerEncounter"/> steps on this machine for the co-op encounter flow.</summary>
public class PlayerEncounterInterface : IPlayerEncounterInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerEncounterInterface>();

    // The menu vanilla's encounter shows while its pre-battle conversation runs.
    private const string EncounterMeetingMenuId = "encounter_meeting";

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
                        // Begin/Wait and any future states are not after-battle work. Yield until the
                        // next campaign tick instead of spinning forever when no player map event exists.
                        playerEncounter._stateHandled = true;
                        break;
                }
            }
        });
    }

    public void ReleaseHeroesWithoutConversation(PlayerEncounter playerEncounter)
    {
        if (playerEncounter._capturedAlreadyPrisonerHeroes == null)
        {
            playerEncounter._capturedAlreadyPrisonerHeroes = playerEncounter.RosterToReceiveLootMembers
                .RemoveIf(element => element.Character.IsHero &&
                                     element.Character.HeroObject.PartyBelongedToAsPrisoner != PartyBase.MainParty)
                .ToList();
        }

        var releasable = playerEncounter._capturedAlreadyPrisonerHeroes
            .Where(candidate =>
                candidate.Character?.HeroObject is Hero hero &&
                hero.IsPrisoner &&
                hero.PartyBelongedToAsPrisoner != PartyBase.MainParty &&
                ShouldReleaseWithoutConversation(hero, Clan.PlayerClan))
            .ToList();

        foreach (var element in releasable)
        {
            var hero = element.Character.HeroObject;

            // Dequeue first so a failed release cannot reopen the conversation either.
            playerEncounter._capturedAlreadyPrisonerHeroes.Remove(element);
            Logger.Information("Releasing {HeroId} of clan {ClanId} after battle without a conversation",
                hero.StringId, hero.Clan?.StringId);
            EndCaptivityAction.ApplyByReleasedAfterBattle(hero);
        }
    }

    public bool IsEncounterMeetingConversationOpen() =>
        Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId == EncounterMeetingMenuId
        && Campaign.Current.ConversationManager.IsConversationInProgress;

    public bool TryCompleteEncounterMeeting(out string failure)
    {
        failure = null;
        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != EncounterMeetingMenuId)
        {
            failure = "The encounter meeting is not active.";
            return false;
        }

        if (!Campaign.Current.ConversationManager.IsConversationInProgress)
        {
            failure = "The encounter conversation is not active.";
            return false;
        }

        Campaign.Current.ConversationManager.EndConversation();
        return true;
    }

    internal static bool ShouldReleaseWithoutConversation(Hero hero, Clan localPlayerClan)
    {
        if (hero == null) return false;

        if (hero.Clan != null && hero.Clan != localPlayerClan && hero.IsPlayerHero())
            return true;

        // Native only has a free prisoner line for the local clan's companions.
        return hero.CompanionOf != null && hero.CompanionOf != localPlayerClan;
    }

    private void EndPlayerEncounter(PlayerEncounter playerEncounter)
    {
        //playerEncounter.DoEnd(); // Default implementation's function. Might have some logic we need later
        playerEncounter._stateHandled = true;
        PlayerEncounter.Finish(true);
    }
}
