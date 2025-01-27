using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.StanceLinks.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using Common;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.StanceLinks.Handlers;


/// <summary>
/// Lifetime handler for <see cref="StanceLink"/> objects.
/// </summary>
internal class StanceLinkLifetimeHandler: IHandler
{
	private static readonly ILogger Logger = LogManager.GetLogger<StanceLinkLifetimeHandler>();
	private readonly IMessageBroker messageBroker;
	private readonly INetwork network;
	private readonly IObjectManager objectManager;
	public StanceLinkLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
	{
		this.messageBroker = messageBroker;
		this.network = network;
		this.objectManager = objectManager;
		messageBroker.Subscribe<StanceLinkCreated>(HandleCreatedEvent);
		messageBroker.Subscribe<NetworkCreateStanceLink>(HandleCreateCommand);
		messageBroker.Subscribe<StanceLinkDestroyed>(HandleDestroyedEvent);
		messageBroker.Subscribe<NetworkDestroyStanceLink>(HandleDestroyCommand);
	}

	public void Dispose()
	{
		messageBroker.Unsubscribe<StanceLinkCreated>(HandleCreatedEvent);
		messageBroker.Unsubscribe<NetworkCreateStanceLink>(HandleCreateCommand);
		messageBroker.Subscribe<StanceLinkDestroyed>(HandleDestroyedEvent);
		messageBroker.Subscribe<NetworkDestroyStanceLink>(HandleDestroyCommand);
	}

	private void HandleCreatedEvent(MessagePayload<StanceLinkCreated> payload)
	{
		if (!objectManager.AddNewObject(payload.What.Instance, out var id))
		{
			Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(StanceLinkCreated));
			return;
		}

		network.SendAll(new NetworkCreateStanceLink(id));
	}

	// WARNING: This is a default generated implementation that might not work on all services, be sure to test and implement any needed logic
	private void HandleCreateCommand(MessagePayload<NetworkCreateStanceLink> payload)
	{
		GameLoopRunner.RunOnMainThread(() =>
		{
			using (new AllowedThread())
			{
				var newStanceLink = ObjectHelper.SkipConstructor<StanceLink>();
				if (!objectManager.AddExisting(payload.What.StanceLinkId, newStanceLink))
				{
					Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(StanceLink), nameof(NetworkCreateStanceLink));
				}
			}
		});
	}

	private void HandleDestroyedEvent(MessagePayload<StanceLinkDestroyed> payload)
	{
		var obj = payload.What.Instance;

		if (!objectManager.TryGetId(obj, out var id)) return;

		if (!objectManager.Remove(obj))
		{
			Logger.Error("Unable to remove {ObjectName} with id: {Id} on {EventHandler}", nameof(StanceLink), id, nameof(StanceLinkDestroyed));
			return;
		}

		network.SendAll(new NetworkDestroyStanceLink(id));
	}

	// WARNING: This is a default generated implementation that might not work on all services, be sure to test and implement any needed logic
	private void HandleDestroyCommand(MessagePayload<NetworkDestroyStanceLink> payload)
	{
		var id = payload.What.StanceLinkId;

		if (!objectManager.TryGetObject<StanceLink>(id, out var obj)) return;

		if (!objectManager.Remove(obj))
		{
			Logger.Error("Failed to remove {ObjectName} with Id: {Id} on {EventHandler}", nameof(StanceLink), id, nameof(NetworkDestroyStanceLink));
			return;
		}
	}
}
