using E2E.Tests.Util.ObjectBuilders;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util;
internal class GameObjectCreator
{
    private static Dictionary<Type, IObjectBuilder> ObjectBuilders = new Dictionary<Type, IObjectBuilder>
    {
        { typeof(CharacterObject), new CharacterObjectBuilder() },
        { typeof(Settlement), new SettlementBuilder() },
        { typeof(Clan), new ClanBuilder() },
        { typeof(Hero), new HeroBuilder() },
        { typeof(LordPartyComponent), new LordPartyComponentBuilder() },
        { typeof(Hideout), new HideoutBuilder() },
        { typeof(CultureObject), new CultureBuilder() },
        { typeof(PartyTemplateObject), new PartyTemplateObjectBuilder() },
        { typeof(Town), new TownBuilder() },
        { typeof(Village), new VillageBuilder() },
        { typeof(MobileParty), new MobilePartyBuilder() },
        { typeof(MapEvent), new MapEventBuilder() },
    };

    public static T CreateInitializedObject<T>()
    {
        if (ObjectBuilders.ContainsKey(typeof(T)) == false)
        {
            throw new KeyNotFoundException(
                $"{typeof(T)} does not have a builder assigned, please create a builder " +
                $"and add it to {nameof(GameObjectCreator)}.{nameof(ObjectBuilders)}");
        }

        return (T)ObjectBuilders[typeof(T)].Build();
    }
}
