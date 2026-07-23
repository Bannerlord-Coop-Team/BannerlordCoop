using Common.Util;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Barters;

internal interface IBarterClientPresentation
{
    void SynchronizeMainHeroGold(int gold);
}

internal sealed class BarterClientPresentation : IBarterClientPresentation
{
    public void SynchronizeMainHeroGold(int gold)
    {
        if (Hero.MainHero == null) return;

        using (new AllowedThread())
            Hero.MainHero.Gold = gold;

        MapScreen.Instance?
            .GetMapView<GauntletMapBarView>()?
            ._mapBarGlobalLayer?
            .Refresh();
    }
}
