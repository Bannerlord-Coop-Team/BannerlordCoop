using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEngineMissileBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new SiegeEvent.SiegeEngineMissile(
                null,
                0,
                SiegeBombardTargets.None,
                0,
                null,
                CampaignTime.Now,
                CampaignTime.Now,
                false
            );
        }
    }
}