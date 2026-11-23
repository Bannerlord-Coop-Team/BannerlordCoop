using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Interfaces;

public interface IBattleWreckageInterface : IGameAbstraction
{
    void StartWreckageEncounter(BattleWreckage battleWreckage, Hero mainHero);
    void OpenWreckageEncounter(BattleWreckage battleWreckage);
}

public class BattleWreckageInterface : IBattleWreckageInterface
{
    private readonly IBattleWreckageEncounterTracker encounterTracker;

    public BattleWreckageInterface(IBattleWreckageEncounterTracker encounterTracker)
    {
        this.encounterTracker = encounterTracker;
    }

    public void StartWreckageEncounter(BattleWreckage battleWreckage, Hero mainHero)
    {
        // TODO: Add the player's encounter to the tracker and publish WreckageEncounterStarted
    }

    public void OpenWreckageEncounter(BattleWreckage battleWreckage)
    {
        // TODO: Client side, open the wreckage menu like vanilla SetCurrentEncounteredBattleWreckage
    }
}
