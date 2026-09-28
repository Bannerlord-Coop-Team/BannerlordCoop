using Common.Logging;
using Common.Util;
using GameInterface.Services.MapEvents.Initialization;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Keeps a hideout encounter that holds staged battle results open until its loot flow finishes it.
/// </summary>
public interface IHideoutResultEncounter
{
    /// <summary>
    /// Keeps the main party's encounter through a replicated settlement leave while it holds staged hideout
    /// battle results. Returns false when the leave should close the encounter as usual.
    /// </summary>
    bool TryKeepThroughLeave(MobileParty party);
}

/// <inheritdoc cref="IHideoutResultEncounter"/>
internal class HideoutResultEncounter : IHideoutResultEncounter
{
    private static readonly ILogger Logger = LogManager.GetLogger<HideoutResultEncounter>();

    public bool TryKeepThroughLeave(MobileParty party)
    {
        var mainParty = MobileParty.MainParty;
        var encounter = PlayerEncounter.Current;
        var settlement = encounter?.EncounterSettlementAux;
        if (party != mainParty || settlement == null || !IsHoldingResults(encounter))
            return false;

        // Same gate and hold as the settlement close, without finishing the encounter that holds the loot.
        using (new AllowedThread())
        {
            mainParty.Position = settlement.GatePosition;
            mainParty.SetMoveModeHold();
        }

        Logger.Debug("Kept hideout encounter {MapEventId} with staged results through replicated settlement leave",
            encounter._mapEvent.StringId);
        return true;
    }

    internal static bool IsHoldingResults(PlayerEncounter encounter) =>
        encounter?._mapEvent?.IsHideoutBattle == true &&
        MapEventInitializationBarrier.IsBattleResultEncounter(encounter);
}
