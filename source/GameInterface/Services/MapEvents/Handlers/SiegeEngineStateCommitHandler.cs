using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] Applies the mission host's final siege engine states through the vanilla write-back with
/// patches live, so the HP writes and broken-engine removals replicate through the container sync.
/// </summary>
internal class SiegeEngineStateCommitHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineStateCommitHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SiegeEngineStateCommitHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkSiegeEngineStatesReport>(HandleReport);
    }

    private void HandleReport(MessagePayload<NetworkSiegeEngineStatesReport> payload)
    {
        if (ModInformation.IsClient) return;

        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(obj.MapEventId, out var mapEvent)) return;

            var siegeEvent = mapEvent.MapEventSettlement?.SiegeEvent;
            if (siegeEvent == null)
            {
                // The siege ended with the battle (capture or broken siege); there is nothing to write back to.
                return;
            }

            siegeEvent.SetSiegeEngineStatesAfterSiegeMission(
                SiegeEngineStateConverter.ToMissionWeapons(obj.AttackerEngines),
                SiegeEngineStateConverter.ToMissionWeapons(obj.DefenderEngines));
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSiegeEngineStatesReport>(HandleReport);
    }
}
