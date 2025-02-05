using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.SiegeEvents.Messages;


/// <summary>
/// An event that is published internally when a SiegeEvent is created.
/// </summary>
internal class SiegeEventCreated : IEvent
{
	public SiegeEventCreated(SiegeEvent instance)
	{
		Instance = instance;
	}
	public SiegeEvent Instance { get; }
}