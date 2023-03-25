using Common;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties
{
    internal interface IMobilePartyRegistry : IRegistryBase<MobileParty>
    {
    }

    internal class MobilePartyRegistry : RegistryBase<MobileParty>, IMobilePartyRegistry
    {
    }
}
