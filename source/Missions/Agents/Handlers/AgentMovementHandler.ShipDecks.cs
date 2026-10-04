using System;
using System.Collections.Generic;
using Missions.Agents.Packets;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>Captures an owned agent's hull-local pose and deck-relative speed from its captured world position.</summary>
public delegate bool NavalDeckPoseCapture(CoopAgentInfo info, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal,
    out float deckSpeed);

/// <summary>Naval movement: deck-relative poses for agents on a hull, and no world movement for agents seated at a ship station.</summary>
public partial class AgentMovementHandler
{
    private NavalDeckPoseCapture deckCapture;
    private Func<CoopAgentInfo, bool> isSeatedAtShipStation;
    private long deckStamped, deckAccepted, deckRejected, seatedWithheld;

    public void ConfigureShipDecks(NavalDeckPoseCapture capture, NavalDeckFrameResolver resolver)
    {
        deckCapture = capture;
        deckStamped = deckAccepted = deckRejected = seatedWithheld = 0;
        _interpolator.ConfigureNavalDeck(resolver);
    }

    public void ConfigureSeatedMovement(Func<CoopAgentInfo, bool> isSeated) => isSeatedAtShipStation = isSeated;

    public void ForgetMovementTarget(Agent agent) => _interpolator.Forget(agent);

    // [Game thread] Stamped at packet construction; returns the packet's hull table, null when no pose is deck-relative.
    private Guid[] StampDecks(string scope, ushort[] compactIds, Guid[] canonicalIds, AgentData[] data)
    {
        if (deckCapture == null) return null;

        List<Guid> ships = null;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].MountData != null) continue;

            CoopAgentInfo info;
            bool found = scope == null ? agentRegistry.TryGetAgentInfo(canonicalIds[i], out info)
                : agentRegistry.TryGetAgentInfo(scope, compactIds[i], out info);
            if (!found || !deckCapture(info, data[i].Position, out Guid ship, out Vec3 local, out float speed)) continue;

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
        if (isSeatedAtShipStation == null || captured.IsMount || !isSeatedAtShipStation(captured.AgentInfo)) return false;

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
