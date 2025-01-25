using Common.Messaging;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Messages;


/// <summary>
/// An event that is published internally when a Kingdom is destroyed.
/// </summary>
internal class KingdomDestroyed : IEvent
{
	public KingdomDestroyed(Kingdom instance)
	{
		Instance = instance;
	}
	public Kingdom Instance { get; }
}