using System;
using System.Collections.Generic;
using Missions.Agents.Packets;
using Missions.Battles;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>Naval movement: deck-relative poses for agents on a hull, and no world movement for agents seated at a ship station.</summary>
public partial class AgentMovementHandler
{
    public INavalDeckMovementProbe DeckMovementProbe { get; }

    private IShipDeckSeam shipDeck;
    private IShipStationSeats stationSeats;
    private long deckStamped, deckAccepted, deckRejected, seatedWithheld;

    public void ConfigureShipDeck(IShipDeckSeam seam)
    {
        shipDeck = seam;
        DeckMovementProbe.Reset();
        deckStamped = deckAccepted = deckRejected = seatedWithheld = 0;
        _interpolator.ConfigureNavalDeck(seam == null ? null : seam.TryResolveDeckFrame);
    }

    public void ConfigureStationSeats(IShipStationSeats seats) => stationSeats = seats;

    public void ForgetMovementTarget(Agent agent) => _interpolator.Forget(agent);

    // [Game thread] Captures an owned on-foot agent's hull-local pose once per sample, so the send decision compares it.
    private void CaptureDeck(CoopAgentInfo info, ref AgentData data)
    {
        if (shipDeck == null || data.MountData != null) return;
        if (!shipDeck.TryCaptureDeck(info, data.Position, totalSimulationTime, out Guid ship, out Vec3 local, out float speed)) return;

        data.CaptureDeck(ship, local, speed);
        deckStamped++;
    }

    // The packet's hull table, indexing each captured deck pose into it; null when no pose is deck-relative.
    private static Guid[] IndexDecks(AgentData[] data)
    {
        List<Guid> ships = null;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].DeckShip == Guid.Empty) continue;

            ships ??= new List<Guid>();
            int index = ships.IndexOf(data[i].DeckShip);
            if (index < 0)
            {
                ships.Add(data[i].DeckShip);
                index = ships.Count - 1;
            }

            data[i].IndexDeck(index + 1);
        }

        return ships?.ToArray();
    }

    // A deck pose that moved compares hull-local, since hull bob alone moves the world pose past the threshold every sample.
    private static bool HasPoseMoved(AgentData previous, AgentData current)
    {
        if (previous.DeckShip != current.DeckShip) return true;

        return current.DeckShip == Guid.Empty
            ? (current.Position - previous.Position).LengthSquared > PositionDeltaThresholdSq
            : (current.DeckLocal - previous.DeckLocal).LengthSquared > PositionDeltaThresholdSq;
    }

    // [Game thread] A deck pose never falls back to world; one that cannot resolve clears the buffered target and halts the
    // puppet, as an evicted deck target does.
    private void ApplyDeckMovement(Guid agentId, Agent agent, AgentData data)
    {
#if DEBUG
        bool reject = DeckMovementProbe.ShouldReject(agentId);
#else
        bool reject = false;
#endif
        if (!reject && data.HasValidDeck && data.MountData == null && !agent.HasMount && _interpolator.TrySetRiderDeckTarget(agent, data))
        {
            deckAccepted++;
            return;
        }

        _interpolator.ForgetAndHalt(agent);
        deckRejected++;
    }

    // A seated agent's pose belongs to its station, which peers replay through the station-use stream.
    private bool WithholdSeatedMovement(string controllerId, RecipientMovementState recipient, CapturedMovement captured)
    {
        if (stationSeats == null || captured.IsMount || !stationSeats.IsOwnSeat(captured.AgentInfo.AgentId)) return false;

        // Nothing is sent or deferred, so leaving the station compares against no baseline.
        recipient.LastSentMovement.Remove(captured.AgentInfo.AgentId);
        recipient.MovementPendingSince.Remove(captured.AgentInfo.AgentId);
        DeckMovementProbe.Record(controllerId, captured.AgentInfo.AgentId, default, false, totalSimulationTime);
        seatedWithheld++;
        return true;
    }

    // The deck capture, receive and seat counters with the interpolator's deck diagnostics.
    public object InspectShipDecks() => new
    {
        stamped = deckStamped,
        accepted = deckAccepted,
        rejected = deckRejected,
        seatedWithheld,
        sendProbe = DeckMovementProbe.Inspect(totalSimulationTime),
        interpolator = _interpolator.InspectNavalDeck(),
    };
}
