using Autofac;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Instances.Messages;
using GameInterface.Registry.Auto;
using HarmonyLib;
using IntroServer.Config;
using Missions.Services.Network.Surrogates;
using Missions.Services.Taverns;
using ProtoBuf.Meta;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network
{
    /// <summary>
    /// EXPERIMENTAL live-play bridge connecting the campaign-side instance assignment to the Missions
    /// P2P stack. Lives in the Missions assembly (which has the Autofac/ProtoBuf/P2P dependencies); the
    /// live <c>Coop</c> module activates it with a single <see cref="Activate"/> call.
    ///
    /// When the server assigns this client a P2P instance (<see cref="InstanceAssigned"/>) it builds the
    /// Missions container, points the NAT-punch rendezvous at the co-hosting Coop server, connects,
    /// attaches the P2P mission behaviors to the current interior mission, and punches. Every step logs
    /// under "[LocationSync]" and is guarded so a failure at one step is visible without aborting the
    /// rest — run it in-game and report the log back to see what works and what needs fixing.
    /// </summary>
    public class LiveInstanceLauncher
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LiveInstanceLauncher>();

        private static LiveInstanceLauncher _instance;

        /// <summary>Create the singleton launcher once. Safe to call multiple times.</summary>
        public static void Activate()
        {
            if (_instance != null) return;
            _instance = new LiveInstanceLauncher(MessageBroker.Instance);
        }

        private readonly IMessageBroker broker;
        private readonly Harmony harmony = new Harmony("Coop.LocationSync.LiveInstance");

        private IContainer missionContainer;
        private LiteNetP2PClient p2pClient;
        private bool surrogatesRegistered;
        private bool patchesApplied;
        private string activeInstanceId;

        private LiveInstanceLauncher(IMessageBroker broker)
        {
            this.broker = broker;
            broker.Subscribe<InstanceAssigned>(Handle_InstanceAssigned);
            broker.Subscribe<InstanceCleared>(Handle_InstanceCleared);
            Logger.Information("[LocationSync] LiveInstanceLauncher active — waiting for instance assignments");
        }

        private void Handle_InstanceAssigned(MessagePayload<InstanceAssigned> payload)
        {
            var data = payload.What;
            Logger.Information(
                "[LocationSync] >>> InstanceAssigned id={Id} host={Host} settlement={Stl} location={Loc}",
                data.InstanceId, data.IsHost, data.SettlementId, data.LocationId);

            // OpenIndoorMission fires more than once per entry, so the same assignment arrives multiple
            // times. Set up P2P only once per instance id.
            if (data.InstanceId == activeInstanceId)
            {
                Logger.Information("[LocationSync] Instance {Id} already active — ignoring duplicate assignment.", data.InstanceId);
                return;
            }

            try
            {
                EnsureSurrogates();
                EnsureContainer();
                EnsurePatches();

                if (missionContainer == null)
                {
                    Logger.Error("[LocationSync] ABORT: mission container unavailable — P2P cannot start. (Step: container)");
                    return;
                }

                activeInstanceId = data.InstanceId;

                // The agent registry is a PROCESS-GLOBAL singleton reused across tavern visits and is not
                // cleared in the live path. Stale entries from a prior visit make IsAgentRegistered return
                // true and silently skip spawns. Clear it at the start of each new instance.
                ClearAgentRegistry();

                if (TryConfigureRendezvous() == false)
                {
                    Logger.Warning("[LocationSync] Could not resolve the campaign server address; the P2P rendezvous is " +
                        "using compiled-in defaults and will almost certainly NOT reach the server. (Step: rendezvous) REPORT THIS.");
                }

                StartP2PSocket();

                AttachBehaviorsToCurrentMission();

                Logger.Information("[LocationSync] Sending NAT punch for instance {Id}", data.InstanceId);
                p2pClient?.NatPunch(data.InstanceId);
                Logger.Information("[LocationSync] <<< InstanceAssigned handling complete for {Id}", data.InstanceId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[LocationSync] Unhandled exception while starting P2P instance {Id} — REPORT THIS STACK", data.InstanceId);
            }
        }

        private void Handle_InstanceCleared(MessagePayload<InstanceCleared> payload)
        {
            Logger.Information("[LocationSync] InstanceCleared — tearing down P2P client");
            try
            {
                p2pClient?.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[LocationSync] Error stopping P2P client on clear");
            }
            p2pClient = null;
            activeInstanceId = null;
            ClearAgentRegistry();
        }

        private void ClearAgentRegistry()
        {
            try
            {
                if (missionContainer == null) return;
                missionContainer.Resolve<INetworkAgentRegistry>().Clear();
                Logger.Information("[LocationSync] Cleared agent registry for new instance");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[LocationSync] Failed to clear agent registry");
            }
        }

        // ProtoBuf surrogates for the game types carried by P2P messages (positions, characters, etc.).
        // Without these, NetworkMissionJoinInfo serialization throws. Guarded because the campaign side
        // may already have registered some surrogates on the process-wide RuntimeTypeModel.
        private void EnsureSurrogates()
        {
            if (surrogatesRegistered) return;
            surrogatesRegistered = true;

            Logger.Information("[LocationSync] Registering Missions ProtoBuf surrogates (idempotent, guarded)");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>(), "Vec3");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>(), "Vec2");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<Mat3, Mat3Surrogate>(), "Mat3");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<Blow, BlowSurrogate>(), "Blow");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<AttackCollisionData, AttackCollisionDataSurrogate>(), "AttackCollisionData");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<CharacterObject, CharacterObjectSurrogate>(), "CharacterObject");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<Banner, BannerSurrogate>(), "Banner");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<ItemObject, ItemObjectSurrogate>(), "ItemObject");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<ItemModifier, ItemModifierSurrogate>(), "ItemModifier");
            TrySetSurrogate(() => RuntimeTypeModel.Default.SetSurrogate<Equipment, EquipmentSurrogate>(), "Equipment");
        }

        private static void TrySetSurrogate(Action set, string name)
        {
            try
            {
                set();
                Logger.Debug("[LocationSync] Surrogate registered: {Name}", name);
            }
            catch (Exception ex)
            {
                // Most likely already registered by the campaign side — fine.
                Logger.Debug("[LocationSync] Surrogate {Name} not (re)registered: {Msg}", name, ex.Message);
            }
        }

        private void EnsureContainer()
        {
            if (missionContainer != null) return;
            try
            {
                Logger.Information("[LocationSync] Building MissionModule container for live P2P...");
                var builder = new ContainerBuilder();
                builder.RegisterModule<MissionModule>();
                missionContainer = builder.Build();
                ContainerProvider.SetContainer(missionContainer);
                Logger.Information("[LocationSync] MissionModule container built");

                // The P2P serializer's surrogates (CharacterObject etc.) resolve object ids through THIS
                // container's ObjectManager. It starts empty, so without populating its registry,
                // serializing a CharacterObject NREs (ResolveId returns null for e.g. CharacterAttribute).
                // The test harness does this via RegisterAllGameObjects(); replicate it here. RegisterAll()
                // is used directly to avoid re-broadcasting AllGameObjectsRegistered to the live container.
                Logger.Information("[LocationSync] Populating P2P container object registry...");
                missionContainer.Resolve<IAutoRegistryFactory>().RegisterAll();
                Logger.Information("[LocationSync] P2P container object registry populated");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[LocationSync] Failed to build/populate MissionModule container (Step: container) — REPORT THIS");
            }
        }

        // The Missions assembly has its own Harmony patches (agent movement/damage sync). They are not
        // applied by the live module's normal patching, so apply them here. Movement sync depends on it;
        // simply seeing each other's spawned agent may work even if this fails.
        private void EnsurePatches()
        {
            if (patchesApplied) return;
            patchesApplied = true;
            try
            {
                Logger.Information("[LocationSync] Applying Missions Harmony patches...");
                harmony.PatchAll(typeof(MissionModule).Assembly);
                Logger.Information("[LocationSync] Missions Harmony patches applied");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[LocationSync] Failed to apply Missions Harmony patches (Step: patches) — movement sync may not work. REPORT THIS");
            }
        }

        private bool TryConfigureRendezvous()
        {
            if (GameInterface.ContainerProvider.TryResolve<INetworkConfiguration>(out var campaignCfg) == false || campaignCfg == null)
            {
                return false;
            }

            var p2pCfg = missionContainer.Resolve<NetworkConfiguration>();
            Logger.Information("[LocationSync] Rendezvous: pointing P2P at campaign server {Addr}:{Port} (defaults were {OldAddr}:{OldPort}, NAT={Nat})",
                campaignCfg.Address, campaignCfg.Port, p2pCfg.WanAddress, p2pCfg.WanPort, p2pCfg.NATType);
            p2pCfg.SetRendezvous(campaignCfg.Address, campaignCfg.Port);
            return true;
        }

        // Start the P2P socket WITHOUT connecting to the campaign server. NAT punch
        // (SendNatIntroduceRequest) is an unconnected message the server's NatPunchModule answers, so
        // we must not open a second LiteNetLib connection to the campaign server — doing so registers a
        // bogus extra peer there and tears down the player's real campaign session.
        private void StartP2PSocket()
        {
            try
            {
                p2pClient = missionContainer.Resolve<LiteNetP2PClient>();
                Logger.Information("[LocationSync] Starting P2P socket (no campaign-server connection)...");
                p2pClient.Start();
                Logger.Information("[LocationSync] P2P socket started");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[LocationSync] Exception starting P2P socket (Step: start) — REPORT THIS");
            }
        }

        private void AttachBehaviorsToCurrentMission()
        {
            var mission = Mission.Current;
            if (mission == null)
            {
                Logger.Warning("[LocationSync] Mission.Current is null when attaching P2P behaviors — the interior mission " +
                    "may not be open yet, or the assignment arrived too late. (Step: attach) REPORT THIS timing.");
                return;
            }

            // The double OpenIndoorMission can drive this twice for the same mission. CoopTavernsController
            // is InstancePerDependency, so a second attach would add a second controller with its own
            // identity and a duplicate broker subscription. Skip if one is already present.
            if (mission.GetMissionBehavior<CoopTavernsController>() != null)
            {
                Logger.Information("[LocationSync] CoopTavernsController already attached to mission '{Scene}' — skipping duplicate attach.", mission.SceneName);
                return;
            }

            try
            {
                var netBehavior = missionContainer.Resolve<CoopMissionNetworkBehavior>();
                var tavernController = missionContainer.Resolve<CoopTavernsController>();

                // Live campaign: leaving the tavern must return to the settlement, not EndGame() to the
                // main menu (that behaviour is only for the standalone Missions test harness).
                netBehavior.IsLiveInstance = true;

                Common.GameLoopRunner.RunOnMainThread(() =>
                {
                    mission.AddMissionBehavior(netBehavior);
                    mission.AddMissionBehavior(tavernController);
                }, true);

                Logger.Information("[LocationSync] Attached CoopMissionNetworkBehavior + CoopTavernsController to mission '{Scene}'. " +
                    "NOTE: behaviors were added AFTER mission start, so AfterStart/OnRenderingStarted will NOT have fired for them " +
                    "(local player agent may not be registered). This is a known ordering gap — REPORT whether agents appear.",
                    mission.SceneName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[LocationSync] Failed to attach P2P behaviors (Step: attach) — REPORT THIS");
            }
        }
    }
}
