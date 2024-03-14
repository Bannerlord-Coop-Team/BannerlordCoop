using Autofac;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct PartyBaseSurrogate
{
    [ProtoMember(1)]
    public string StringId { get; }

    [ProtoMember(2)]
    public bool IsParty { get; }

    public PartyBaseSurrogate(PartyBase partyBase)
    {
        StringId = partyBase.MobileParty.StringId;
        IsParty = partyBase.IsMobile;
    }

    public static implicit operator PartyBaseSurrogate(PartyBase partyBase)
    {
        return new PartyBaseSurrogate(partyBase);
    }

    public static implicit operator PartyBase(PartyBaseSurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (surrogate.IsParty && objectManager.TryGetObject<MobileParty>(surrogate.StringId, out var mobileParty))
        {
            return mobileParty.Party;
        }

        if (objectManager.TryGetObject<Settlement>(surrogate.StringId, out var settlement))
        {
            return settlement.Party;
        }

        return null;
    }
}
