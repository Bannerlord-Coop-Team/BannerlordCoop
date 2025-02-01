using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.SiegeEvents.Messages;


/// <summary>
/// An event published to clients, commanding them to destroy a SiegeEvent.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroySiegeEvent : ICommand
{
	[ProtoMember(1)]
	public string SiegeEventId { get; }

	public NetworkDestroySiegeEvent(string siegeEventId)
	{
		SiegeEventId = siegeEventId;
	}
}
