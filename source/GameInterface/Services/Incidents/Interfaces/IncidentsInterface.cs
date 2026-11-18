using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Incidents.Interfaces;

public interface IIncidentsInterface : IGameAbstraction
{
    bool TryInvokeOption(Hero mainHero, MobileParty mainParty, Incident.IncidentOption selectedOption, long activeIncidentSeed, out List<TextObject> incidentOutcomes);
    bool TryPrepareIncidentText(Incident incident, long activeIncidentSeed);
}

public class IncidentsInterface : IIncidentsInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;

    public IncidentsInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
    }

    public bool TryPrepareIncidentText(Incident incident, long activeIncidentSeed)
    {
        if (Campaign.Current?.GetCampaignBehavior<IncidentsCampaignBehaviour>() == null) return false;

        // Same seed the server rolled with, so the text names the same targets the server applies effects to
        Campaign.Current.IncidentManager._activeIncidentSeed = activeIncidentSeed;

        return incident.CanIncidentBeInvoked();
    }

    public bool TryInvokeOption(Hero mainHero, MobileParty mainParty, Incident.IncidentOption selectedOption, long activeIncidentSeed, out List<TextObject> incidentOutcomes)
    {
        incidentOutcomes = new List<TextObject>();

        if (Campaign.Current?.GetCampaignBehavior<IncidentsCampaignBehaviour>() == null) return false;

        // Vanilla conditions and effects pick their targets with the incident manager's seed, so use the one this player's incident was rolled with
        Campaign.Current.IncidentManager._activeIncidentSeed = activeIncidentSeed;

        // Naval incident effects check the main hero's unlocked figureheads, which on the server aren't this player's
        var serverFigureheads = Campaign.Current.UnlockedFigureheadsByMainHero;
        Campaign.Current.UnlockedFigureheadsByMainHero = GetUnlockedFigureheads(mainHero);
        try
        {
            using (new MainHeroSubstitutionScope(mainHero, mainParty))
            {
                // The player's state can change between the incident being sent and an option being picked
                if (selectedOption.Condition != null && !selectedOption.Condition(selectedOption.Text)) return false;
                if (selectedOption.Effects.Any(effect => !effect.Condition())) return false;

                foreach (IncidentEffect incidentEffect in selectedOption.Effects)
                {
                    incidentOutcomes.AddRange(incidentEffect.Consequence());
                }

                selectedOption.Consequence?.Invoke();
            }
        }
        finally
        {
            Campaign.Current.UnlockedFigureheadsByMainHero = serverFigureheads;
        }

        return true;
    }

    private List<Figurehead> GetUnlockedFigureheads(Hero playerHero)
    {
        var figureheads = new List<Figurehead>();
        if (!objectManager.TryGetId(playerHero, out var playerHeroId)) return figureheads;
        if (coopSessionProvider.CoopSession?.NavalPlayerData?.PlayerUnlockedFigureHeads.TryGetValue(playerHeroId, out var figureheadIds) != true) return figureheads;

        foreach (var figureheadId in figureheadIds)
        {
            var figurehead = MBObjectManager.Instance.GetObject<Figurehead>(figureheadId);
            if (figurehead != null) figureheads.Add(figurehead);
        }

        return figureheads;
    }
}
