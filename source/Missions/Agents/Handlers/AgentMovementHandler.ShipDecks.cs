using System;
using System.Collections.Generic;
using Missions.Agents.Packets;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>
/// A naval battle's view of agents on hulls, which the movement and action handlers ask while it is configured: owned
/// agents on a hull send hull-local poses, received ones rebase on the local hull, and seated ones leave their pose and
/// actions to the station.
/// </summary>
public interface IShipDeckSeam
{
    /// <summary>[Game thread] An owned on-foot agent's hull-local pose and deck-relative speed; false when it is on no registered hull.</summary>
    bool TryCaptureDeck(CoopAgentInfo info, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed);

    /// <summary>[Game thread] The local hull, owned or copied, that a received deck pose is relative to.</summary>
    bool TryResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame);

    /// <summary>Whether this client seated the agent at a ship station, so it sends no world movement or actions for it.</summary>
    bool IsSeatedAtStation(CoopAgentInfo info);
}

/// <summary>Naval movement: deck-relative poses for agents on a hull, and no world movement for agents seated at a ship station.</summary>
public partial class AgentMovementHandler
{
    private IShipDeckSeam shipDeck;
    private long deckStamped, deckAccepted, deckRejected, seatedWithheld;

    public void ConfigureShipDeck(IShipDeckSeam seam)
    {
        shipDeck = seam;
        deckStamped = deckAccepted = deckRejected = seatedWithheld = 0;
        _interpolator.ConfigureNavalDeck(seam == null ? null : seam.TryResolveDeckFrame);
    }

    public void ForgetMovementTarget(Agent agent) => _interpolator.Forget(agent);

    // [Game thread] Stamped at packet construction; returns the packet's hull table, null when no pose is deck-relative.
    private Guid[] StampDecks(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentData[] data)
    {
        if (shipDeck == null) return null;

        List<Guid> ships = null;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].MountData != null) continue;

            CoopAgentInfo info;
            bool found = scope == null ? agentRegistry.TryGetAgentInfo(canonicalIds[i], out info)
                : agentRegistry.TryGetAgentInfo(scope, compactIds[i], out info);
            if (!found || !shipDeck.TryCaptureDeck(info, data[i].Position, out Guid ship, out Vec3 local, out float speed)) continue;

            ships ??= new List<Guid>();
            int index = ships.IndexOf(ship);
            if (index < 0)
            {
                ships.Add(ship);
                index = ships.Count - 1;
            }

            data[i].StampDeck(ship, index + 1, local, speed);
            deckStamped++;
        }

        return ships?.ToArray();
    }

    // [Game thread] A deck pose never falls back to world; one that cannot resolve clears the buffered target.
    private void ApplyDeckMovement(Agent agent, AgentData data)
    {
        if (data.HasValidDeck && data.MountData == null && !agent.HasMount && _interpolator.TrySetRiderDeckTarget(agent, data))
        {
            deckAccepted++;
            return;
        }

        _interpolator.Forget(agent);
        deckRejected++;
    }

    // A seated agent's pose belongs to its station, which peers replay through the station-use stream.
    private bool WithholdSeatedMovement(RecipientMovementState recipient, CapturedMovement captured)
    {
        if (shipDeck == null || captured.IsMount || !shipDeck.IsSeatedAtStation(captured.AgentInfo)) return false;

        // Nothing is sent or deferred, so leaving the station compares against no baseline.
        recipient.LastSentMovement.Remove(captured.AgentInfo.AgentId);
        recipient.MovementPendingSince.Remove(captured.AgentInfo.AgentId);
        seatedWithheld++;
        return true;
    }

    public object InspectShipDecks() => new
    {
        stamped = deckStamped,
        accepted = deckAccepted,
        rejected = deckRejected,
        seatedWithheld,
        interpolator = _interpolator.InspectNavalDeck(),
    };
}
