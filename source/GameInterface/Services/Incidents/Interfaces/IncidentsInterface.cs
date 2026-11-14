using GameInterface.Services.Heroes.Patches;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Incidents.Interfaces;

public interface IIncidentsInterface : IGameAbstraction
{
    bool TryInvokeOption(Hero mainHero, MobileParty mainParty, Incident.IncidentOption selectedOption, long activeIncidentSeed, out List<TextObject> incidentOutcomes);
    bool TryPrepareIncidentText(Incident incident, long activeIncidentSeed);
}

public class IncidentsInterface : IIncidentsInterface
{
    public bool TryPrepareIncidentText(Incident incident, long activeIncidentSeed)
    {
        var incidentsBehavior = Campaign.Current?.GetCampaignBehavior<IncidentsCampaignBehaviour>();
        if (incidentsBehavior == null) return false;

        // Same seed the server rolled with, so the text names the same targets the server applies effects to
        Campaign.Current.IncidentManager._activeIncidentSeed = activeIncidentSeed;

        return incident.CanIncidentBeInvoked();
    }

    public bool TryInvokeOption(Hero mainHero, MobileParty mainParty, Incident.IncidentOption selectedOption, long activeIncidentSeed, out List<TextObject> incidentOutcomes)
    {
        incidentOutcomes = new List<TextObject>();

        var incidentsBehavior = Campaign.Current?.GetCampaignBehavior<IncidentsCampaignBehaviour>();
        if (incidentsBehavior == null) return false;

        // Vanilla conditions and effects pick their targets with the behaviour's seed, so use the one this player's incident was rolled with
        Campaign.Current.IncidentManager._activeIncidentSeed = activeIncidentSeed;

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

        return true;
    }
}
