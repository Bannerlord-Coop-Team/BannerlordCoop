using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.SiegeEvents.Messages;


/// <summary>
/// An event published to clients, commanding them to create a SiegeEvent.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeEvent : ICommand
{
	[ProtoMember(1)]
	public string SiegeEventId { get; }

	public NetworkCreateSiegeEvent(string siegeEventId)
	{
		SiegeEventId = siegeEventId;
	}
}
