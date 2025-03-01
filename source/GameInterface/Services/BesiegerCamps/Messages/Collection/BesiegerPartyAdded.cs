using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Internal event for <see cref="BesiegerCamp._besiegerParties" Add/>
/// </summary>
public record BesiegerPartyAdded : GenericEvent<BesiegerCamp, MobileParty>
{
    public BesiegerPartyAdded(BesiegerCamp instance, MobileParty value) : base(instance, value)
    {
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}