using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Conversations.Handlers;

/// <summary>
/// [NPC host] Applies the server's conversation-lock hold to the live agent (SR-040): while a remote
/// player holds the lock on a location NPC, the host pauses that NPC's AI so the conversation on the
/// other client anchors to a stationary agent (its puppet follows the host's now-stationary
/// movement), and un-pauses it on release. Every other client no-ops — only the confirmed location
/// host simulates NPCs. The agent is found by roster-origin identity, the same reference rule native
/// bookkeeping uses, which works because host-side NPCs spawn from their roster entries natively.
/// </summary>
internal class LocationNpcHoldHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationNpcHoldHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public LocationNpcHoldHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkLocationNpcHold>(Handle_NetworkLocationNpcHold);
        messageBroker.Subscribe<NetworkLocationNpcReleased>(Handle_NetworkLocationNpcReleased);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkLocationNpcHold>(Handle_NetworkLocationNpcHold);
        messageBroker.Unsubscribe<NetworkLocationNpcReleased>(Handle_NetworkLocationNpcReleased);
    }

    private void Handle_NetworkLocationNpcHold(MessagePayload<NetworkLocationNpcHold> payload)
    {
        SetNpcPaused(payload.What.LocationId, payload.What.CharacterId, paused: true);
    }

    private void Handle_NetworkLocationNpcReleased(MessagePayload<NetworkLocationNpcReleased> payload)
    {
        SetNpcPaused(payload.What.LocationId, payload.What.CharacterId, paused: false);
    }

    private void SetNpcPaused(string locationId, string characterId, bool paused)
    {
        if (ModInformation.IsServer) return;
        if (!LocationNpcGate.IsCoopLocationMissionActive || !LocationNpcGate.IsLocalHostConfirmed) return;

        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            if (mission == null) return;

            if (!objectManager.TryGetObject<Location>(locationId, out var location) || location == null) return;
            if (!objectManager.TryGetObject<CharacterObject>(characterId, out var character) || character == null) return;

            foreach (var entry in location.GetCharacterList())
            {
                if (entry?.Character != character) continue;

                foreach (var agent in mission.Agents)
                {
                    if (agent.Origin != entry.AgentOrigin) continue;

                    agent.SetIsAIPaused(paused);
                    Logger.Information("[LocationNpc] {Action} NPC {Character} for a remote conversation",
                        paused ? "Held" : "Released", characterId);
                    return;
                }
            }
        }, context: nameof(SetNpcPaused));
    }
}
