using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

/// <summary>
/// Server-local signal that a <see cref="LordPartyComponent"/>'s <see cref="LordPartyComponent.Owner"/>
/// was set. Published from the transpiled set_Owner call site (the property-setter prefix is unreliable
/// because the auto-property setter is inlined into the ctor). The handler broadcasts
/// <see cref="NetworkLordPartyOwnerChanged"/>.
/// </summary>
internal readonly struct LordPartyOwnerChanged : IEvent
{
    public readonly LordPartyComponent Instance;
    public readonly Hero Owner;

    public LordPartyOwnerChanged(LordPartyComponent instance, Hero owner)
    {
        Instance = instance;
        Owner = owner;
    }
}
