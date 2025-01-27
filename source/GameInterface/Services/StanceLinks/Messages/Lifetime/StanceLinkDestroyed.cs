using Common.Messaging;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.StanceLinks.Messages;


/// <summary>
/// An event that is published internally when a StanceLink is destroyed.
/// </summary>
internal class StanceLinkDestroyed : IEvent
{
	public StanceLinkDestroyed(StanceLink instance)
	{
		Instance = instance;
	}
	public StanceLink Instance { get; }
}