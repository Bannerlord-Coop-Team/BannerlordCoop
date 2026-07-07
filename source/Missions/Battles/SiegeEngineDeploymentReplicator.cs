using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates the mission host's siege engine placement to every other client's mission. The host is
/// the single engine deployer (its auto-deploys and UI edits are authoritative for both sides); peers
/// apply the same Deploy/Disband on their scene-matched deployment points, and a joiner gets the whole
/// current placement replayed.
/// </summary>
public interface ISiegeEngineDeploymentReplicator : IDisposable
{
    /// <summary>[Host] Replay every placement so far to a joining controller.</summary>
    void CatchUpJoiner(string controllerId);
}

/// <inheritdoc cref="ISiegeEngineDeploymentReplicator"/>
public class SiegeEngineDeploymentReplicator : ISiegeEngineDeploymentReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineDeploymentReplicator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;

    // The current placement per point, in first-touch order so a replay applies in the original order
    // and the receivers' HP-by-type mapping matches. Recorded on every client (not just the deployer)
    // so a successor promoted after host migration can still catch joiners up. Game-thread only.
    private readonly List<KeyValuePair<int, string>> placements = new List<KeyValuePair<int, string>>();

    public SiegeEngineDeploymentReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;

        messageBroker.Subscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Subscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEnginePlacementChanged>(Handle_PlacementChanged);
        messageBroker.Unsubscribe<NetworkSiegeEnginePlacement>(Handle_NetworkPlacement);
        // SuppressCapture is toggled around this replicator's own applies, so it resets here; the
        // authority flags are owned by CoopBattleController (which sets them each tick) and reset there.
        SiegeMissionAuthorityGate.SuppressCapture = false;
    }

    private void Handle_PlacementChanged(MessagePayload<SiegeEnginePlacementChanged> payload)
    {
        if (!session.IsLocalHost) return;

        int pointId = payload.What.Point.Id.Id;
        Record(pointId, payload.What.WeaponTypeName);
        network.SendAll(new NetworkSiegeEnginePlacement(pointId, payload.What.WeaponTypeName));
    }

    private void Record(int pointId, string weaponTypeName)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i].Key == pointId)
            {
                placements[i] = new KeyValuePair<int, string>(pointId, weaponTypeName);
                return;
            }
        }

        placements.Add(new KeyValuePair<int, string>(pointId, weaponTypeName));
    }

    private void Handle_NetworkPlacement(MessagePayload<NetworkSiegeEnginePlacement> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null)
            {
                Logger.Warning("[BattleSync] Dropping siege engine placement for point {PointId}: no mission", obj.PointId);
                return;
            }

            var point = Mission.Current.MissionObjects
                .OfType<DeploymentPoint>()
                .FirstOrDefault(candidate => candidate.Id.Id == obj.PointId);
            if (point == null)
            {
                Logger.Error("[BattleSync] No deployment point with id {PointId} in this mission", obj.PointId);
                return;
            }

            Record(obj.PointId, obj.WeaponTypeName);

            SiegeMissionAuthorityGate.SuppressCapture = true;
            try
            {
                if (string.IsNullOrEmpty(obj.WeaponTypeName))
                {
                    if (point.DeployedWeapon != null) point.Disband();
                }
                else
                {
                    var weaponType = point.DeployableWeaponTypes.FirstOrDefault(type => type.Name == obj.WeaponTypeName);
                    if (weaponType == null)
                    {
                        Logger.Error("[BattleSync] Point {PointId} has no deployable weapon of type {Type}", obj.PointId, obj.WeaponTypeName);
                        return;
                    }

                    if (point.DeployedWeapon != null) point.Disband();
                    point.Deploy(weaponType);
                }
            }
            finally
            {
                SiegeMissionAuthorityGate.SuppressCapture = false;
            }
        });
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (!session.IsLocalHost) return;

        GameThread.RunSafe(() =>
        {
            foreach (var placement in placements)
            {
                network.Send(controllerId, new NetworkSiegeEnginePlacement(placement.Key, placement.Value));
            }

            if (placements.Count > 0)
                Logger.Information("[BattleSync] Replayed {Count} siege engine placement(s) to joining {Controller}", placements.Count, controllerId);
        });
    }
}
