using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.SiegeEvents.Handlers;


/// <summary>
/// Lifetime handler for <see cref="SiegeEvent"/> objects.
/// </summary>
internal class SiegeEventLifetimeHandler: IHandler
{
	private static readonly ILogger Logger = LogManager.GetLogger<SiegeEventLifetimeHandler>();
	private readonly IMessageBroker messageBroker;
	private readonly INetwork network;
	private readonly IObjectManager objectManager;
	public SiegeEventLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
	{
		this.messageBroker = messageBroker;
		this.network = network;
		this.objectManager = objectManager;
		messageBroker.Subscribe<SiegeEventCreated>(HandleCreatedEvent);
		messageBroker.Subscribe<NetworkCreateSiegeEvent>(HandleCreateCommand);
		messageBroker.Subscribe<SiegeEventDestroyed>(HandleDestroyedEvent);
		messageBroker.Subscribe<NetworkDestroySiegeEvent>(HandleDestroyCommand);
	}

	public void Dispose()
	{
		messageBroker.Unsubscribe<SiegeEventCreated>(HandleCreatedEvent);
		messageBroker.Unsubscribe<NetworkCreateSiegeEvent>(HandleCreateCommand);
	}

	private void HandleCreatedEvent(MessagePayload<SiegeEventCreated> payload)
	{
		if (!objectManager.AddNewObject(payload.What.Instance, out var id))
		{
			Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(SiegeEventCreated));
			return;
		}

		network.SendAll(new NetworkCreateSiegeEvent(id));
	}

	private void HandleCreateCommand(MessagePayload<NetworkCreateSiegeEvent> payload)
	{
		var newSiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();

	// TODO:Initialize null lists!

		if (!objectManager.AddExisting(payload.What.SiegeEventId, newSiegeEvent))
		{
			Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(SiegeEvent), nameof(NetworkCreateSiegeEvent));
			return;
		}
	}

	private void HandleDestroyedEvent(MessagePayload<SiegeEventDestroyed> payload)
	{
		var obj = payload.What.Instance;

		if (!objectManager.TryGetId(obj, out var id)) return;

		if (!objectManager.Remove(obj))
		{
			Logger.Error("Unable to remove {ObjectName} with id: {Id} on {EventHandler}", nameof(SiegeEvent), id, nameof(SiegeEventDestroyed));
			return;
		}

		network.SendAll(new NetworkDestroySiegeEvent(id));
	}

	private void HandleDestroyCommand(MessagePayload<NetworkDestroySiegeEvent> payload)
	{
		var id = payload.What.SiegeEventId;

		if (!objectManager.TryGetObject<SiegeEvent>(id, out var obj)) return;

		if (!objectManager.Remove(obj))
		{
			Logger.Error("Failed to remove {ObjectName} with Id: {Id} on {EventHandler}", nameof(SiegeEvent), id, nameof(NetworkDestroySiegeEvent));
			return;
		}
	}
}
