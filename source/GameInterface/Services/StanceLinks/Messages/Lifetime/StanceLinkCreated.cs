using Common.Messaging;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.StanceLinks.Messages;


/// <summary>
/// An event that is published internally when a StanceLink is created.
/// </summary>
internal class StanceLinkCreated : IEvent
{
	public StanceLinkCreated(StanceLink instance)
	{
		Instance = instance;
	}
	public StanceLink Instance { get; }
}