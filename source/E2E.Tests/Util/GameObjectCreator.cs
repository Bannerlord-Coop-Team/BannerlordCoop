using E2E.Tests.Util.ObjectBuilders;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util;
internal class GameObjectCreator
{
    private static Dictionary<Type, IObjectBuilder> ObjectBuilders = new Dictionary<Type, IObjectBuilder>
    {
        { typeof(CharacterObject), new CharacterObjectBuilder() },
        { typeof(Settlement), new SettlementBuilder() },
        { typeof(Kingdom), new KingdomBuilder() },
        { typeof(Clan), new ClanBuilder() },
        { typeof(Hero), new HeroBuilder() },
        { typeof(LordPartyComponent), new LordPartyComponentBuilder() },
        { typeof(Hideout), new HideoutBuilder() },
        { typeof(CultureObject), new CultureBuilder() },
        { typeof(PartyTemplateObject), new PartyTemplateObjectBuilder() },
        { typeof(Town), new TownBuilder() },
        { typeof(Village), new VillageBuilder() },
        { typeof(MobileParty), new MobilePartyBuilder() },
        { typeof(BanditPartyComponent), new BanditPartyComponentBuilder() },
        { typeof(CustomPartyComponent), new CustomPartyComponentBuilder() },
        { typeof(MapEvent), new MapEventBuilder() },
        { typeof(MapEventSide), new MapEventSideBuilder() },
        { typeof(BesiegerCamp), new BesiegerCampBuilder() },
        { typeof(SiegeEvent), new SiegeEventBuilder() },
        { typeof(Workshop), new WorkshopBuilder() },
        { typeof(WorkshopType), new WorkshopTypeBuilder() },
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
