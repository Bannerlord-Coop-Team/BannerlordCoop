using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is removed from the attached party list
/// </summary>
public record AttachedPartyRemoved : GenericEvent<MobileParty, MobileParty>
{
    public AttachedPartyData AttachedPartyData { get; }

    /// <summary>
    /// Default ctor used for testing
    /// </summary>
    public AttachedPartyRemoved()
    {
    }

    public AttachedPartyRemoved(MobileParty instance, MobileParty value) : base(instance, value)
    {
        AttachedPartyData = new AttachedPartyData(instance.StringId, value.StringId);
    }

    public override void HandleEvent(IObjectManager objectManager, INetwork network)
    {
        throw new System.NotImplementedException();
    }
}
