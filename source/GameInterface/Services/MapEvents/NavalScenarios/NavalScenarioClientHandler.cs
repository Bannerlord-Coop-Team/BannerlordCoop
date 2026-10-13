#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Handlers;
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
/// DEBUG, client: takes the server's naval scenario and, for the attacked player and a late joiner, enters its battle
/// through the same mission-start request the encounter menu's Attack! option sends.
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

    public NavalScenarioClientHandler(
        IMessageBroker messageBroker,
        INavalScenarioState state,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.state = state;
        this.controllerIdProvider = controllerIdProvider;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkNavalScenarioStarted>(Handle_NetworkNavalScenarioStarted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNavalScenarioStarted>(Handle_NetworkNavalScenarioStarted);
    }

    private void Handle_NetworkNavalScenarioStarted(MessagePayload<NetworkNavalScenarioStarted> payload)
    {
        if (ModInformation.IsServer) return;

        var config = payload.What.Config;
        if (config == null) return;

        GameThread.RunSafe(() =>
        {
            state.Start(config, controllerIdProvider.ControllerId, isServer: false);
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

    private string PendingAttackStep()
    {
        if (state.IsPending(NavalScenarioState.AttackStep)) return NavalScenarioState.AttackStep;
        if (state.IsPending(NavalScenarioState.LateJoinAttackStep)) return NavalScenarioState.LateJoinAttackStep;
        return null;
    }

    private bool TryAttack(NavalScenarioConfig config, string step)
    {
        var mainParty = MobileParty.MainParty;
        var mapEvent = mainParty?.MapEvent;
        if (mapEvent == null || Mission.Current != null || CurrentEncounterBattle() != mapEvent) return false;
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

    // PlayerEncounter.Battle dereferences the current encounter, which can be torn down between frames.
    private static MapEvent CurrentEncounterBattle()
    {
        try
        {
            return PlayerEncounter.Current == null ? null : PlayerEncounter.Battle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }
}
#endif
