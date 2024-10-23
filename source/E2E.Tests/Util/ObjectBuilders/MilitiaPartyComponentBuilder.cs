using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MilitiaPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

        return new MilitiaPartyComponent(settlement);
    }
}
