using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Handlers;


/// <summary>
/// Lifetime handler for <see cref="Kingdom"/> objects.
/// </summary>
internal class KingdomLifetimeHandler: IHandler
{
	private static readonly ILogger Logger = LogManager.GetLogger<KingdomLifetimeHandler>();
	private readonly IMessageBroker messageBroker;
	private readonly INetwork network;
	private readonly IObjectManager objectManager;
	public KingdomLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
	{
		this.messageBroker = messageBroker;
		this.network = network;
		this.objectManager = objectManager;
		messageBroker.Subscribe<KingdomCreated>(HandleCreatedEvent);
		messageBroker.Subscribe<NetworkCreateKingdom>(HandleCreateCommand);
		messageBroker.Subscribe<KingdomDestroyed>(HandleDestroyedEvent);
		messageBroker.Subscribe<NetworkDestroyKingdom>(HandleDestroyCommand);
	}

	public void Dispose()
	{
		messageBroker.Unsubscribe<KingdomCreated>(HandleCreatedEvent);
		messageBroker.Unsubscribe<NetworkCreateKingdom>(HandleCreateCommand);
		messageBroker.Subscribe<KingdomDestroyed>(HandleDestroyedEvent);
		messageBroker.Subscribe<NetworkDestroyKingdom>(HandleDestroyCommand);
	}

	private void HandleCreatedEvent(MessagePayload<KingdomCreated> payload)
	{
		if (!objectManager.AddNewObject(payload.What.Instance, out var id))
		{
			Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(KingdomCreated));
			return;
		}

		network.SendAll(new NetworkCreateKingdom(id));
	}

	// WARNING: This is a default generated implementation that might not work on all services, be sure to test and implement need logic
	private void HandleCreateCommand(MessagePayload<NetworkCreateKingdom> payload)
	{
		GameLoopRunner.RunOnMainThread(() =>
		{
			using (new AllowedThread())
			{
				var newKingdom = ObjectHelper.SkipConstructor<Kingdom>();
				if (!objectManager.AddExisting(payload.What.KingdomId, newKingdom))
				{
					Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(Kingdom), nameof(NetworkCreateKingdom));
				}
			}
		});
	}

	private void HandleDestroyedEvent(MessagePayload<KingdomDestroyed> payload)
	{
		var obj = payload.What.Instance;

		if (!objectManager.TryGetId(obj, out var id)) return;

		if (!objectManager.Remove(obj))
		{
			Logger.Error("Unable to remove {ObjectName} with id: {Id} on {EventHandler}", nameof(Kingdom), id, nameof(KingdomDestroyed));
			return;
		}

		network.SendAll(new NetworkDestroyKingdom(id));
	}

	// WARNING: This is a default generated implementation that might not work on all services, be sure to test and implement need logic
	private void HandleDestroyCommand(MessagePayload<NetworkDestroyKingdom> payload)
	{
		var id = payload.What.KingdomId;

		if (!objectManager.TryGetObject<Kingdom>(id, out var obj)) return;

		if (!objectManager.Remove(obj))
		{
			Logger.Error("Failed to remove {ObjectName} with Id: {Id} on {EventHandler}", nameof(Kingdom), id, nameof(NetworkDestroyKingdom));
			return;
		}
	}
}
