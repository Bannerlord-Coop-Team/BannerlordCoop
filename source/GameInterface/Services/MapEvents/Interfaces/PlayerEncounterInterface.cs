using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Interfaces;

public interface IPlayerEncounterInterface : IGameAbstraction
{
    public void UpdateInternalAfterBattle(PlayerEncounter playerEncounter);
    public void ReleaseHeroesWithoutConversation(PlayerEncounter playerEncounter);
}

public class PlayerEncounterInterface : IPlayerEncounterInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerEncounterInterface>();

    private readonly ISiegeEventInterface siegeEventInterface;

    public PlayerEncounterInterface() : this(null)
    {
    }

    public PlayerEncounterInterface(ISiegeEventInterface siegeEventInterface)
    {
        this.siegeEventInterface = siegeEventInterface;
    }

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
        var mapEvent = playerEncounter._mapEvent;
        var continueRaid = mapEvent?.IsRaidHostileAction() == true &&
            mapEvent.WinningSide == BattleSideEnum.Attacker &&
            playerEncounter.PlayerSide == BattleSideEnum.Attacker &&
            playerEncounter._attackerParty == PartyBase.MainParty &&
            (MobileParty.MainParty.Army == null ||
             MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty) &&
            mapEvent.MapEventSettlement?.IsVillage == true &&
            mapEvent.MapEventSettlement.SettlementHitPoints > 0f;
        var settlement = continueRaid ? mapEvent.MapEventSettlement : null;

        playerEncounter._stateHandled = true;

        // a won siege's leader gets the aftermath choice and an army member the leader's pick, like vanilla DoEnd
        // (#3903); this finishes the encounter itself, otherwise the stale assault menu stays up with only Leave
        if (siegeEventInterface?.TryRouteToAftermath(playerEncounter._mapEvent) == true)
            return;

        PlayerEncounter.Finish(true);

        if (!continueRaid || settlement == null)
            return;
        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
            messageBroker = MessageBroker.Instance;

        // The completed event is already finalized. Start a fresh encounter so the existing hostile-action
        // approval creates the next raid through the authoritative server path.
        PlayerEncounter.Start();
        PlayerEncounter.Current.ForceRaid = true;
        messageBroker.Publish(this, new VillageHostileActionAttempted(
            VillageHostileAction.Raid,
            MobileParty.MainParty,
            settlement));
    }
}
