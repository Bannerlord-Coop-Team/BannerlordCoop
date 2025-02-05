using HarmonyLib;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.Hero;

namespace E2E.Tests.Util.ObjectBuilders;
internal class HeroBuilder : IObjectBuilder
{
    public object Build()
    {
        var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();

        var hero = HeroCreator.CreateSpecialHero(characterObject);
        hero.ChangeState(CharacterStates.Active);
        hero.Clan = clan;
                
        return hero;
    }
}
