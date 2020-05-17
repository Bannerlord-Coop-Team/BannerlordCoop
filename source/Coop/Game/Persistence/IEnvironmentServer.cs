using Coop.Game.Persistence.Party;
using Coop.Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public interface IEnvironmentServer
    {
        SyncFieldGroup<MobileParty, MovementData> TargetPosition { get; }
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
    }
}
