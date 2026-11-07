#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>
/// DEBUG, client: takes the server's naval scenario and enters its battle through the encounter menu's Attack! request; for
/// the encounter preset it first answers the encounter conversation, as the player would.
/// </summary>
internal class NavalScenarioClientHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalScenarioClientHandler>();

    private const int PollMilliseconds = 250;
    private const float AttackWaitSeconds = 120f;

    private readonly IMessageBroker messageBroker;
    private readonly INavalScenarioState state;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerEncounterInterface playerEncounterInterface;

    public NavalScenarioClientHandler(
        IMessageBroker messageBroker,
        INavalScenarioState state,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        IPlayerEncounterInterface playerEncounterInterface)
    {
        this.messageBroker = messageBroker;
        this.state = state;
        this.controllerIdProvider = controllerIdProvider;
        this.objectManager = objectManager;
        this.playerEncounterInterface = playerEncounterInterface;

        messageBroker.Subscribe<NetworkNavalScenarioStarted>(Handle_NetworkNavalScenarioStarted);
        messageBroker.Subscribe<NetworkNavalScenarioEngaging>(Handle_NetworkNavalScenarioEngaging);
    }

    // Drops this handler's subscriptions.
    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNavalScenarioStarted>(Handle_NetworkNavalScenarioStarted);
        messageBroker.Unsubscribe<NetworkNavalScenarioEngaging>(Handle_NetworkNavalScenarioEngaging);
    }

    // The encounter conversation opens before the map event (and with it the scenario message) exists, so a listed player
    // watches for it from the engage order on (R42: client2 sat in it until a manual complete_encounter_meeting).
    private void Handle_NetworkNavalScenarioEngaging(MessagePayload<NetworkNavalScenarioEngaging> payload)
    {
        if (ModInformation.IsServer) return;

        var config = payload.What.Config;
        if (config == null) return;

        GameThread.RunSafe(() =>
        {
            if (!config.ControllerIds.Contains(controllerIdProvider.ControllerId)) return;

            Logger.Information("[NavalScenario] {Preset} {ScenarioId}: answering the encounter conversation when it opens",
                config.Preset, config.ScenarioId);
            var wait = new NavalEncounterMeetingWait(config, DateTime.UtcNow.AddSeconds(NavalScenarioStager.EngageWaitSeconds));
            PollEncounterMeeting(wait);
        }, context: nameof(Handle_NetworkNavalScenarioEngaging));
    }

    // Answers each encounter conversation once it settled, until the scenario's map event reached this client or the wait ends.
    private void PollEncounterMeeting(NavalEncounterMeetingWait wait)
    {
        var config = wait.Config;
        if (state.Current?.ScenarioId == config.ScenarioId) return;
        if (DateTime.UtcNow >= wait.Deadline)
        {
            Logger.Warning("[NavalScenario] {ScenarioId}: stopped watching for the encounter conversation after {Seconds:0} s " +
                "({Answers} answered) with no scenario battle", config.ScenarioId, NavalScenarioStager.EngageWaitSeconds, wait.Answers);
            return;
        }

        if (wait.ShouldAnswer(DateTime.UtcNow, playerEncounterInterface.IsEncounterMeetingConversationOpen()))
            AnswerEncounterMeeting(wait);

        Task.Delay(PollMilliseconds).ContinueWith(_ =>
            GameThread.RunSafe(() => PollEncounterMeeting(wait), context: nameof(PollEncounterMeeting)));
    }

    // Ends the open encounter conversation as complete_encounter_meeting does, so vanilla goes on to the battle.
    private void AnswerEncounterMeeting(NavalEncounterMeetingWait wait)
    {
        if (!playerEncounterInterface.TryCompleteEncounterMeeting(out var failure))
        {
            Logger.Warning("[NavalScenario] {ScenarioId}: could not answer the encounter conversation: {Failure}",
                wait.Config.ScenarioId, failure);
            return;
        }

        wait.Answered();
        Logger.Information("[NavalScenario] {ScenarioId}: answered the encounter conversation ({Answers})",
            wait.Config.ScenarioId, wait.Answers);
    }

    // [Poller thread] Starts this client's copy of the scenario on the game thread and polls its Attack.
    private void Handle_NetworkNavalScenarioStarted(MessagePayload<NetworkNavalScenarioStarted> payload)
    {
        if (ModInformation.IsServer) return;

        var config = payload.What.Config;
        bool rejoining = payload.What.Rejoining;
        if (config == null) return;

        GameThread.RunSafe(() =>
        {
            state.Start(config, controllerIdProvider.ControllerId, isServer: false, rejoining);
            Logger.Information("[NavalScenario] {Preset} {ScenarioId} on {MapEventId}: steps {Steps}",
                config.Preset, config.ScenarioId, config.MapEventId, string.Join(",", state.Steps.Select(item => item.Name)));

            float waitSeconds = AttackWaitSeconds + (config.LateJoin ? config.DelaySeconds : 0f);
            PollAttack(config, DateTime.UtcNow.AddSeconds(waitSeconds));
        }, context: nameof(Handle_NetworkNavalScenarioStarted));
    }

    // The encounter reaches this client after the scenario message, so the attack polls until the encounter is up.
    private void PollAttack(NavalScenarioConfig config, DateTime deadline)
    {
        if (state.Current != config) return;

        string step = PendingAttackStep();
        if (step == null || TryAttack(config, step)) return;

        if (DateTime.UtcNow >= deadline)
        {
            state.Complete(step, false, $"the encounter of {config.MapEventId} never reached this client");
            return;
        }

        Task.Delay(PollMilliseconds).ContinueWith(_ =>
            GameThread.RunSafe(() => PollAttack(config, deadline), context: nameof(PollAttack)));
    }

    // The Attack step this client still has to run (first entry, late join or rejoin), or null.
    private string PendingAttackStep()
    {
        if (state.IsPending(NavalScenarioState.AttackStep)) return NavalScenarioState.AttackStep;
        if (state.IsPending(NavalScenarioState.LateJoinAttackStep)) return NavalScenarioState.LateJoinAttackStep;
        if (state.IsPending(NavalScenarioState.RejoinAttackStep)) return NavalScenarioState.RejoinAttackStep;
        return null;
    }

    // Asks the server to start the scenario's mission once its encounter is up here; false while it isn't yet.
    private bool TryAttack(NavalScenarioConfig config, string step)
    {
        var mainParty = MobileParty.MainParty;
        var mapEvent = mainParty?.MapEvent;
        if (mapEvent == null || Mission.Current != null || PlayerEncounter.Battle != mapEvent) return false;
        if (!objectManager.TryGetId(mapEvent, out string mapEventId) || mapEventId != config.MapEventId) return false;
        if (!objectManager.TryGetId(mainParty, out string partyId)) return false;

        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null) return false;

        bool accepted = coordinator.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId);
        state.Complete(step, accepted, accepted
            ? "the server accepted the mission start"
            : "the server refused the mission start");
        Logger.Information("[NavalScenario] {Step} for {MapEventId}: accepted={Accepted}", step, mapEventId, accepted);
        return true;
    }
}

/// <summary>
/// DEBUG, client: one encounter preset's wait for its encounter conversation, which is answered once it stayed open
/// <see cref="SettleTime"/>, so the conversation mission has finished opening.
/// </summary>
internal sealed class NavalEncounterMeetingWait
{
    internal static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    private DateTime? openSince;

    public NavalEncounterMeetingWait(NavalScenarioConfig config, DateTime deadline)
    {
        Config = config;
        Deadline = deadline;
    }

    public NavalScenarioConfig Config { get; }

    public DateTime Deadline { get; }

    /// <summary>How many conversations this wait answered.</summary>
    public int Answers { get; private set; }

    /// <summary>Whether to answer now: the conversation has been open <see cref="SettleTime"/>; closing it restarts the clock.</summary>
    public bool ShouldAnswer(DateTime now, bool conversationOpen)
    {
        if (!conversationOpen)
        {
            openSince = null;
            return false;
        }

        if (openSince == null) openSince = now;
        return now - openSince.Value >= SettleTime;
    }

    /// <summary>Counts an answered conversation; a later one settles again before it is answered.</summary>
    public void Answered()
    {
        Answers++;
        openSince = null;
    }
}
#endif
