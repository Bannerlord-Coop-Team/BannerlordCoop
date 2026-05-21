using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEngineMissileBuilder : IObjectBuilder
    {
        public object Build()
        {
            var type =
                Game.Current.ObjectManager.GetObject<SiegeEngineType>("catapult");

            var target = new SiegeEvent.SiegeEngineConstructionProgress(
                type,
                1f,
                100f
            );

            return new SiegeEvent.SiegeEngineMissile(
                type,
                0,
                SiegeBombardTargets.RangedEngines,
                1,
                target,
                CampaignTime.Now,
                CampaignTime.Now,
                true
            );
        }
    }
}