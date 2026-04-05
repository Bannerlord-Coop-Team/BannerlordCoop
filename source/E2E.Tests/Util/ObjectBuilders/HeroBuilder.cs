using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Hero;

namespace E2E.Tests.Util.ObjectBuilders;
internal class HeroBuilder : IObjectBuilder
{
    public object Build()
    {
        var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
        var clan = GameObjectCreator.CreateInitializedObject<Clan>();

        //StealthEquipment Temporary fix
        characterObject.Culture.DefaultBattleEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
        characterObject.Culture.DefaultStealthEquipmentRoster = GameObjectCreator.CreateInitializedObject<MBEquipmentRoster>();
        characterObject.Culture.DefaultStealthEquipmentRoster.AllEquipments[0]._itemSlots[0].Item = GameObjectCreator.CreateInitializedObject<ItemObject>();

        var hero = HeroCreator.CreateSpecialHero(characterObject);
        hero.ChangeState(CharacterStates.Active);
        hero.Clan = clan;

        hero.OwnedAlleys = new List<Alley>();
        hero.OwnedCaravans = new List<CaravanPartyComponent>();

        return hero;
    }
}
