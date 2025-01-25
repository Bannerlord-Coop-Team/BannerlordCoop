using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Messages;


/// <summary>
/// An event published to clients, commanding them to create a <see cref="Kingdom"/>
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateKingdom : ICommand
{
	[ProtoMember(1)]
	public string KingdomId { get; }

	public NetworkCreateKingdom(string kingdomId)
	{
		KingdomId = kingdomId;
	}
}
