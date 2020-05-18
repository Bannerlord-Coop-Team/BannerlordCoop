using Coop.Mod.Persistence.Party;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentServer
    {
        SyncFieldGroup<MobileParty, MovementData> TargetPosition { get; }
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
    }
}
