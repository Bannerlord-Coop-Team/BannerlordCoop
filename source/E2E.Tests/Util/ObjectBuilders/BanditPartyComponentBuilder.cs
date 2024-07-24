using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class BanditPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var partyComponent = ObjectHelper.SkipConstructor<BanditPartyComponent>();
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        AccessTools.Constructor(typeof(BanditPartyComponent), new Type[] { typeof(Settlement) })
            .Invoke(partyComponent, new object[] { settlement });

        return partyComponent;
    }
}
