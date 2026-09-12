using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
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
    /// Stops a party at its current position and asks its AI to re-pick a behavior, by clearing the
    /// navigation fields directly. The engine's own <c>MobileParty.SetNavigationModeHold</c> does exactly
    /// this, but it's <c>internal</c> so the mod can't call it; and the public <c>SetMoveModeHold</c> is not
    /// a substitute - it resets the AI behavior but leaves <c>PartyMoveMode</c>/<c>MoveTargetParty</c> set,
    /// which is the stale Party-mode targeting that <c>GetTargetCampaignPosition</c> dereferences (a null
    /// <c>MoveTargetParty</c>) after a save/reload.
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

        if (ModInformation.IsServer &&
            !CallOriginalPolicy.IsOriginalAllowed() &&
            party.IsActive)
            GameThread.RunSafe(() => MessageBroker.Instance.Publish(party, new PartyBehaviorChangeAttempted(party)));
    }
}
