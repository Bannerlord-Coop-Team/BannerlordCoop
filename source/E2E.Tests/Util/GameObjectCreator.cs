using E2E.Tests.Util.ObjectBuilders;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util;
internal class GameObjectCreator
{
    private static Dictionary<Type, IObjectBuilder> ObjectBuilders = new Dictionary<Type, IObjectBuilder>
    {
        { typeof(CharacterObject), new CharacterObjectBuilder() },
        { typeof(Settlement), new SettlementBuilder() }
    };

    public static T CreateInitializedObject<T>()
    {
        return (T)ObjectBuilders[typeof(T)].Build();
    }
}
