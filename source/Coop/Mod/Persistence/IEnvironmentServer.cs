using JetBrains.Annotations;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentServer
    {
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        bool CanChangeTimeControlMode { get; }

        [CanBeNull]
        MobileParty GetMobilePartyByIndex(int iPartyIndex);

        Campaign GetCurrentCampaign();
    }
}
