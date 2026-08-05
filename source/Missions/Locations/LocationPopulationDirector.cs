using Common;
using Common.Logging;
using Common.Messaging;
using Missions.Messages;
using SandBox;
using SandBox.Missions.MissionLogics;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Missions.Locations;

/// <summary>
/// Runs the native settlement population pass on the client the server elects NPC host (SR-011/
/// SR-013). Population is native-init-once at each mission controller's <c>AfterStart</c> (V1) and
/// suppressed there on every client (host unknown while loading), so the elected host re-runs it
/// here: the full <c>SpawnLocationCharacters()</c> (its roster event fires exactly once on the host,
/// V1/R1) plus the scene-tag-driven animal helpers (no-ops for scenes without the tags; day-gated
/// exactly as the native town/village controllers gate them).
/// <para>
/// A PROMOTED host must NOT run the pass — its NPCs arrive by adopting the previous host's puppets
/// (SR-014), and its ambient roster event never fired locally. Promotion is detected by state, not
/// message order: a client already holding NPC bindings it received as puppets is being promoted.
/// </para>
/// </summary>
public interface ILocationPopulationDirector : IDisposable
{
}

/// <inheritdoc cref="ILocationPopulationDirector"/>
public class LocationPopulationDirector : ILocationPopulationDirector
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationPopulationDirector>();

    private readonly IMessageBroker messageBroker;
    private readonly ILocationSession session;
    private readonly ILocationAgentBindingMap bindingMap;
    private readonly ILocationPuppetSpawner puppetSpawner;
    private bool populated;

    public LocationPopulationDirector(
        IMessageBroker messageBroker,
        ILocationSession session,
        ILocationAgentBindingMap bindingMap,
        ILocationPuppetSpawner puppetSpawner)
    {
        this.messageBroker = messageBroker;
        this.session = session;
        this.bindingMap = bindingMap;
        this.puppetSpawner = puppetSpawner;

        messageBroker.Subscribe<LocationHostAuthorityAcquired>(Handle_LocationHostAuthorityAcquired);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LocationHostAuthorityAcquired>(Handle_LocationHostAuthorityAcquired);
    }

    private void Handle_LocationHostAuthorityAcquired(MessagePayload<LocationHostAuthorityAcquired> payload)
    {
        if (payload.What.InstanceId != session.InstanceId) return;
        bool wasMigration = payload.What.WasMigration;

        // Non-blocking: the assignment arrives on the network thread while the mission may be loading.
        GameThread.RunSafe(() =>
        {
            if (populated) return;
            if (Mission.Current == null) return;

            var agentHandler = Mission.Current.GetMissionBehavior<MissionAgentHandler>();
            if (agentHandler == null)
            {
                Logger.Warning("[LocationNpc] Host authority acquired but the mission has no MissionAgentHandler — no population to run");
                return;
            }

            // A migration promotion is EXPLICIT on the message (SR-014): adopt-in-place owns the
            // transition whenever any of the previous host's crowd exists here — already-adopted
            // bindings OR retained records still buffered (a promotion before our catch-up applied
            // must not roll a duplicate crowd; those records spawn and late-adopt instead). Only
            // when the departed host provably left NOTHING behind does the promoted host populate —
            // that is a first crowd, not a duplicate.
            if (wasMigration && (bindingMap.Count > 0 || puppetSpawner.HasPendingNpcRecords))
            {
                Logger.Information("[LocationNpc] Promoted to NPC host of {InstanceId} — adopt-in-place, skipping population pass",
                    payload.What.InstanceId);
                populated = true;
                return;
            }

            populated = true;
            Logger.Information("[LocationNpc] Confirmed NPC host of {InstanceId} — running the native population pass",
                payload.What.InstanceId);

            // The suppression gate lifted with the host confirmation, so these run natively; the
            // capture patches replicate everything they spawn. The seeded-RNG and civilian-count
            // patches wrap SpawnLocationCharacters exactly as they wrap the native AfterStart call.
            agentHandler.SpawnLocationCharacters();

            SandBoxHelpers.MissionHelper.SpawnHorses();
            if (!Campaign.Current.IsNight)
            {
                SandBoxHelpers.MissionHelper.SpawnSheeps();
                SandBoxHelpers.MissionHelper.SpawnCows();
                SandBoxHelpers.MissionHelper.SpawnHogs();
                SandBoxHelpers.MissionHelper.SpawnGeese();
                SandBoxHelpers.MissionHelper.SpawnChicken();
            }
        }, context: nameof(Handle_LocationHostAuthorityAcquired));
    }
}
