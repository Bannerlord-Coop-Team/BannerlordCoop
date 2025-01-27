using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.StanceLinks.Messages;


/// <summary>
/// An event published to clients, commanding them to create a <see cref="StanceLink"/>
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateStanceLink : ICommand
{
	[ProtoMember(1)]
	public string StanceLinkId { get; }

	public NetworkCreateStanceLink(string stanceLinkId)
	{
		StanceLinkId = stanceLinkId;
	}
}
