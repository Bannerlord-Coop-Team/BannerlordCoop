using E2E.Tests.Util.ObjectBuilders;
using TaleWorlds.CampaignSystem;
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
    };

    public static T CreateInitializedObject<T>()
    {
        return (T)ObjectBuilders[typeof(T)].Build();
    }
}
