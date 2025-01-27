using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.StanceLinks.Messages;


/// <summary>
/// An event published to clients, commanding them to destroy a <see cref="StanceLink"/>
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyStanceLink : ICommand
{
	[ProtoMember(1)]
	public string StanceLinkId { get; }

	public NetworkDestroyStanceLink(string stanceLinkId)
	{
		StanceLinkId = stanceLinkId;
	}
}
