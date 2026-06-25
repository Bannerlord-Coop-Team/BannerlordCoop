using Common;
using Common.Logging;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Extensions;

public static class MobilePartyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    /// <summary>
    /// Check to see if the Party is controlled by this client or server.
    /// </summary>
    /// <param name="party">MobileParty to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsControlledByThisInstance(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        if (ModInformation.IsServer)
        {
            // Server controls all non-player objects
            return !PlayerManager.TryGetControlledObjectInfo(party, out var _);
        }

        if (!PlayerManager.TryGetControlledObjectInfo(party, out var controlledObjectInfo))
            return false;

        return controlledObjectInfo.IsControlled;
    }

    /// <summary>
    /// Checks to see if the MobileParty is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayerParty(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        return PlayerManager.TryGetControlledObjectInfo(party, out var _);
    }

    public static void SetCurrentSettlementDirectly(this MobileParty party, Settlement settlement)
    {
        party._currentSettlement = settlement;
    }

    /// <summary>
    /// Clears a party's navigation orders so it stops at its current position (and is no longer classified
    /// as moving), then asks the AI to re-decide at the next hourly tick. This is the navigation half of
    /// how the engine fully stops a party - native pairs the public <c>SetMoveModeHold</c> (which resets
    /// the AI behavior and <c>MoveTargetPoint</c>) with the internal <c>SetNavigationModeHold</c> (which
    /// clears <c>PartyMoveMode</c>/<c>MoveTargetParty</c>). It deliberately leaves <c>DefaultBehavior</c>
    /// alone so the caller, or the AI re-think, picks the next behavior. Needed because <c>SetMoveModeHold</c>
    /// on its own leaves <c>PartyMoveMode</c>/<c>MoveTargetParty</c> intact: a party reactivated after
    /// captivity, or one whose <c>MoveTargetParty</c> deserialized to null after a save/reload (the two
    /// fields are saved independently), otherwise keeps stale Party-mode targeting that
    /// <c>GetTargetCampaignPosition</c> dereferences unguarded. (The engine also clears the private
    /// <c>_pathMode</c>; it self-corrects once the party is holding, so it is not reset here.)
    /// </summary>
    public static void ResetNavigationToHold(this MobileParty party)
    {
        party.PartyMoveMode = MoveModeType.Hold;
        party.MoveTargetParty = null;
        // Match MoveTargetPoint to Position so IsMoving (Position != MoveTargetPoint) goes false and the
        // party leaves the moving tick list instead of being ticked every frame as a held "moving" party.
        party.MoveTargetPoint = party.Position;
        party.NextTargetPosition = party.Position;
        // Ai can be null for a not-yet-fully-synced party (the tick patches guard for it), and this runs
        // outside their try/catch, so only request the re-think when Ai exists. The navigation resets
        // above are what actually clear the stale targeting and they don't need Ai.
        if (party.Ai != null)
        {
            party.Ai.RethinkAtNextHourlyTick = true;
        }
    }
}
