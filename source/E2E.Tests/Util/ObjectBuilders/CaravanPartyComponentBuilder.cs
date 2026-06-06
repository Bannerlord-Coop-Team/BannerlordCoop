using Common.Util;
using HarmonyLib;
using Scriban;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class CaravanPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var hero = GameObjectCreator.CreateInitializedObject<Hero>();
        var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();

        return new CaravanPartyComponent(settlement, hero, hero, false, new CaravanPartyComponent.InitializationArgs(template));
    }

    public CaravanPartyComponent BuildWithHero(Hero hero)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var template = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
        var caravan = new CaravanPartyComponent(settlement, hero, hero, false, new CaravanPartyComponent.InitializationArgs(template));
        caravan._cachedName = new TaleWorlds.Localization.TextObject("testCaravan");
        return caravan;
    }
}
