using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEngineMissileBuilder : IObjectBuilder
    {
        public object Build()
        {

            var target = new SiegeEvent.SiegeEngineConstructionProgress(
                null,
                1f,
                100f
            );

            return new SiegeEvent.SiegeEngineMissile(
                null,
                0,
                SiegeBombardTargets.None,
                0,
                target,
                CampaignTime.Now,
                CampaignTime.Now,
                false
            );
        }
    }
}