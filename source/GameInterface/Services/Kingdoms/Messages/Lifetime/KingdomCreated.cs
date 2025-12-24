using Common.Messaging;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Messages.Lifetime;


/// <summary>
/// An event that is published internally when a Kingdom is created.
/// </summary>
internal class KingdomCreated : IEvent
{
	public KingdomCreated(Kingdom instance)
	{
		Instance = instance;
	}
	public Kingdom Instance { get; }
}