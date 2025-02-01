using Common.Messaging;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.SiegeEvents.Messages;


/// <summary>
/// An event that is published internally when a SiegeEvent is destroyed.
/// </summary>
internal class SiegeEventDestroyed : IEvent
{
	public SiegeEventDestroyed(SiegeEvent instance)
	{
		Instance = instance;
	}
	public SiegeEvent Instance { get; }
}