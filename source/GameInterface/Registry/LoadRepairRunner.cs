using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.Clans;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Registry;

public interface ILoadRepairRunner
{
    /// <summary>
    /// Runs the repairs a loaded save needs before the server becomes joinable, then brings the server
    /// registry in line with the objects they created, destroyed or ended. Each step is caught on
    /// its own, so one failing repair cannot stop the server from starting.
    /// </summary>
    void RunLoadRepairs();
}

/// <summary>
/// The repairs run after the first registration and before ServerRunningState, while the sync policy
/// still allows originals, so lifetime patches publish no InstanceCreated or InstanceDestroyed for what
/// they do. A clan repair can create a lord party and the empty party reset destroys parties and
/// disbands armies; without this pass the party stays unregistered (the join baseline then fails for
/// it) or the destroyed object keeps its id. The pass ends with one check line, so a boot can be judged
/// from the server log alone.
/// </summary>
internal sealed class LoadRepairRunner : ILoadRepairRunner
{
    private readonly ILogger logger;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IRegistryManager registryManager;
    private readonly ILoadRepairRegistryReconciler registryReconciler;
    private readonly IMapEventLoadCleaner mapEventLoadCleaner;
    private readonly ILeaderlessClanResolver leaderlessClanResolver;
    private readonly IEmptyLordPartyResetter emptyLordPartyResetter;

    public LoadRepairRunner(
        ILogger logger,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IRegistryManager registryManager,
        ILoadRepairRegistryReconciler registryReconciler,
        IMapEventLoadCleaner mapEventLoadCleaner,
        ILeaderlessClanResolver leaderlessClanResolver,
        IEmptyLordPartyResetter emptyLordPartyResetter)
    {
        this.logger = logger;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.registryManager = registryManager;
        this.registryReconciler = registryReconciler;
        this.mapEventLoadCleaner = mapEventLoadCleaner;
        this.leaderlessClanResolver = leaderlessClanResolver;
        this.emptyLordPartyResetter = emptyLordPartyResetter;
    }

    public void RunLoadRepairs()
    {
        var stagesFailed = 0;
        void RunStep(string step, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                stagesFailed++;
                logger.Error(e, "Load repair step '{Step}' failed; the server starts without it", step);
            }
        }

        using var tally = new LoadRepairTally(messageBroker);

        LoadRepairSnapshot before = null;
        RunStep("capture campaign objects", () => before = registryReconciler.Capture());

        RunStep("clean up loaded map events", mapEventLoadCleaner.CleanUpLoadedMapEvents);
        RunStep("repair leaderless clans", () => leaderlessClanResolver.RepairLoadedClans());
        RunStep("reset empty lord parties", () => emptyLordPartyResetter.ResetEmptyLordParties());

        // Unregister first: a replacement can take the owner-derived id of what it replaced (a fired wanderer
        // companion's reset equipment), and registration skips an id that is still held.
        if (before != null)
            RunStep("unregister removed objects", () => registryReconciler.UnregisterRemoved(before));

        var registeredBefore = objectManager.Count;
        RunStep("register created objects", registryManager.RegisterNewGameObjects);
        var registeredNew = objectManager.Count - registeredBefore;

        logger.Information(
            "[Check] id=coop.boot.load-repairs result={Result:l} stages_failed={StagesFailed} " +
            "unregistered_parties={UnregisteredParties} unregistered_party_ai={UnregisteredPartyAi} " +
            "unregistered_armies={UnregisteredArmies} unregistered_equipment={UnregisteredEquipment} " +
            "unregistered_map_events={UnregisteredMapEvents} unregistered_siege_events={UnregisteredSiegeEvents} " +
            "unregistered_besieger_camps={UnregisteredBesiegerCamps} unregistered_ships={UnregisteredShips} " +
            "announced_map_events={AnnouncedMapEvents} registered_new={RegisteredNew}",
            stagesFailed == 0 ? "pass" : "fail",
            stagesFailed,
            tally.Parties,
            tally.PartyAi,
            tally.Armies,
            tally.Equipment,
            tally.MapEvents,
            tally.SiegeEvents,
            tally.BesiegerCamps,
            tally.Ships,
            tally.AnnouncedMapEvents,
            registeredNew);
    }

    /// <summary>
    /// Counts the destroy and MapEventFinalized messages published during the pass, whichever repair or reconcile
    /// published them. Every publisher in the pass checks registration first, so a destroy count is an unregister
    /// count. The announcement count includes a battle announced again after a late release.
    /// </summary>
    private sealed class LoadRepairTally : IDisposable
    {
        private readonly IMessageBroker messageBroker;

        public LoadRepairTally(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<InstanceDestroyed<MobileParty>>(OnParty);
            messageBroker.Subscribe<InstanceDestroyed<MobilePartyAi>>(OnPartyAi);
            messageBroker.Subscribe<InstanceDestroyed<Army>>(OnArmy);
            messageBroker.Subscribe<InstanceDestroyed<Equipment>>(OnEquipment);
            messageBroker.Subscribe<InstanceDestroyed<MapEvent>>(OnMapEvent);
            messageBroker.Subscribe<InstanceDestroyed<SiegeEvent>>(OnSiegeEvent);
            messageBroker.Subscribe<InstanceDestroyed<BesiegerCamp>>(OnBesiegerCamp);
            messageBroker.Subscribe<InstanceDestroyed<Ship>>(OnShip);
            messageBroker.Subscribe<MapEventFinalized>(OnMapEventFinalized);
        }

        public int Parties { get; private set; }
        public int PartyAi { get; private set; }
        public int Armies { get; private set; }
        public int Equipment { get; private set; }
        public int MapEvents { get; private set; }
        public int SiegeEvents { get; private set; }
        public int BesiegerCamps { get; private set; }
        public int Ships { get; private set; }
        public int AnnouncedMapEvents { get; private set; }

        public void Dispose()
        {
            messageBroker.Unsubscribe<InstanceDestroyed<MobileParty>>(OnParty);
            messageBroker.Unsubscribe<InstanceDestroyed<MobilePartyAi>>(OnPartyAi);
            messageBroker.Unsubscribe<InstanceDestroyed<Army>>(OnArmy);
            messageBroker.Unsubscribe<InstanceDestroyed<Equipment>>(OnEquipment);
            messageBroker.Unsubscribe<InstanceDestroyed<MapEvent>>(OnMapEvent);
            messageBroker.Unsubscribe<InstanceDestroyed<SiegeEvent>>(OnSiegeEvent);
            messageBroker.Unsubscribe<InstanceDestroyed<BesiegerCamp>>(OnBesiegerCamp);
            messageBroker.Unsubscribe<InstanceDestroyed<Ship>>(OnShip);
            messageBroker.Unsubscribe<MapEventFinalized>(OnMapEventFinalized);
        }

        private void OnParty(MessagePayload<InstanceDestroyed<MobileParty>> _) => Parties++;
        private void OnPartyAi(MessagePayload<InstanceDestroyed<MobilePartyAi>> _) => PartyAi++;
        private void OnArmy(MessagePayload<InstanceDestroyed<Army>> _) => Armies++;
        private void OnEquipment(MessagePayload<InstanceDestroyed<Equipment>> _) => Equipment++;
        private void OnMapEvent(MessagePayload<InstanceDestroyed<MapEvent>> _) => MapEvents++;
        private void OnSiegeEvent(MessagePayload<InstanceDestroyed<SiegeEvent>> _) => SiegeEvents++;
        private void OnBesiegerCamp(MessagePayload<InstanceDestroyed<BesiegerCamp>> _) => BesiegerCamps++;
        private void OnShip(MessagePayload<InstanceDestroyed<Ship>> _) => Ships++;
        private void OnMapEventFinalized(MessagePayload<MapEventFinalized> _) => AnnouncedMapEvents++;
    }
}
