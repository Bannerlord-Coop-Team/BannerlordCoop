using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class VillagerPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var village = GameObjectCreator.CreateInitializedObject<Village>();

        return new VillagerPartyComponent(village, new VillagerPartyComponent.InitializationArgs());
    }
}
