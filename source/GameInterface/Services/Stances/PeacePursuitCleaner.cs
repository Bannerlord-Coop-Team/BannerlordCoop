using GameInterface.Services.MobileParties.Extensions;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Stances;

public interface IPeacePursuitCleaner
{
    void HoldAiPartiesPursuingEachOther(IFaction faction1, IFaction faction2);
}

public sealed class PeacePursuitCleaner : IPeacePursuitCleaner
{
    private readonly ILogger logger;

    public PeacePursuitCleaner(ILogger logger)
    {
        this.logger = logger;
    }

    public void HoldAiPartiesPursuingEachOther(IFaction faction1, IFaction faction2)
    {
        if (faction1 == null || faction2 == null)
            return;

        foreach (var party in MobileParty.All.ToArray())
        {
            if (party?.IsActive != true || party.IsPlayerParty())
                continue;

            var targetFaction = party.MapFaction == faction1
                ? faction2
                : party.MapFaction == faction2
                    ? faction1
                    : null;

            if (targetFaction == null || !IsPursuingFaction(party, targetFaction))
                continue;

            logger.Information(
                "Holding AI party {PartyId} after peace ended its pursuit of {TargetFaction}",
                party.StringId,
                targetFaction.Name);
            party.SetMoveModeHold();
        }
    }

    private static bool IsPursuingFaction(MobileParty party, IFaction targetFaction)
    {
        if ((party.DefaultBehavior == AiBehavior.GoAroundParty ||
             party.DefaultBehavior == AiBehavior.EngageParty) &&
            party.TargetParty?.MapFaction == targetFaction)
            return true;

        return (party.ShortTermBehavior == AiBehavior.EngageParty ||
                party.ShortTermBehavior == AiBehavior.GoAroundParty) &&
               party.ShortTermTargetParty?.MapFaction == targetFaction;
    }
}
