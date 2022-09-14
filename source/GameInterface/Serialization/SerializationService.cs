using GameInterface.Serialization.Dynamic;
using GameInterface.Serialization.Surrogates;
using System.CodeDom.Compiler;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Hero;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.Core.HorseComponent;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Serialization
{
    public interface ISerializationService
    {
    }

    public class SerializationService : ISerializationService
    {
        public SerializationService()
        {
            IDynamicModelGenerator modelGenerator = new DynamicModelGenerator();

            CollectDynamicSerializers(modelGenerator);
            CollectSurrogates(modelGenerator);

            modelGenerator.Compile();
        }

        private void CollectDynamicSerializers(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<CharacterAttributes>();
            modelGenerator.CreateDynamicSerializer<CharacterObject>();
            modelGenerator.CreateDynamicSerializer<BasicCharacterObject>().AddDerivedType<CharacterObject>();
            modelGenerator.CreateDynamicSerializer<CharacterPerks>();
            modelGenerator.CreateDynamicSerializer<CharacterTraits>();
            modelGenerator.CreateDynamicSerializer<Hero>(new string[]
            {
                            "_mother",
                            "_father"
            });
            modelGenerator.CreateDynamicSerializer<IHeroDeveloper>();

            // Derived ItemComponents
            modelGenerator.CreateDynamicSerializer<ArmorComponent>();
            modelGenerator.CreateDynamicSerializer<HorseComponent>();
            modelGenerator.CreateDynamicSerializer<SaddleComponent>();
            modelGenerator.CreateDynamicSerializer<TradeItemComponent>();
            modelGenerator.CreateDynamicSerializer<WeaponComponent>();
            modelGenerator.CreateDynamicSerializer<WeaponComponentData>();
            modelGenerator.CreateDynamicSerializer<MaterialProperty>();

            modelGenerator.CreateDynamicSerializer<ItemComponent>()
                .AddDerivedType<ArmorComponent>()
                .AddDerivedType<HorseComponent>()
                .AddDerivedType<SaddleComponent>()
                .AddDerivedType<TradeItemComponent>()
                .AddDerivedType<WeaponComponent>();

            modelGenerator.CreateDynamicSerializer<ItemModifier>();
            modelGenerator.CreateDynamicSerializer<ItemModifierGroup>();
            modelGenerator.CreateDynamicSerializer<ItemObject>();

            modelGenerator.CreateDynamicSerializer<MBBodyProperty>();
            modelGenerator.CreateDynamicSerializer<BodyProperties>();
            modelGenerator.CreateDynamicSerializer<DynamicBodyProperties>();
            modelGenerator.CreateDynamicSerializer<StaticBodyProperties>();

            modelGenerator.CreateDynamicSerializer<MBCharacterSkills>();
            modelGenerator.CreateDynamicSerializer<CharacterSkills>();

            modelGenerator.CreateDynamicSerializer<MBEquipmentRoster>();
            modelGenerator.CreateDynamicSerializer<Equipment>();
            modelGenerator.CreateDynamicSerializer<EquipmentElement>();

            modelGenerator.CreateDynamicSerializer<Monster>(new string[]
            {
                "_monsterMissionData",
            });

            modelGenerator.CreateDynamicSerializer<SkeletonScale>();

            modelGenerator.CreateDynamicSerializer<WeaponDesign>();
            modelGenerator.CreateDynamicSerializer<WeaponDesignElement>();
            modelGenerator.CreateDynamicSerializer<WeaponDescription>();
            modelGenerator.CreateDynamicSerializer<CraftingPiece>();
            modelGenerator.CreateDynamicSerializer<PieceData>();
            modelGenerator.CreateDynamicSerializer<BladeData>();
        }

        private void CollectSurrogates(IDynamicModelGenerator modelGenerator)
        {
            #region StringIdSurrogates
            modelGenerator.AssignSurrogate<CultureObject, StringIdSurrogate<CultureObject>>();
            modelGenerator.AssignSurrogate<BasicCultureObject, StringIdSurrogate<BasicCultureObject>>();
            modelGenerator.AssignSurrogate<Town, StringIdSurrogate<Town>>();
            modelGenerator.AssignSurrogate<Settlement, StringIdSurrogate<Settlement>>();
            modelGenerator.AssignSurrogate<TraitObject, StringIdSurrogate<TraitObject>>();
            modelGenerator.AssignSurrogate<ItemCategory, StringIdSurrogate<ItemCategory>>();
            modelGenerator.AssignSurrogate<CraftingTemplate, StringIdSurrogate<CraftingTemplate>>();

            modelGenerator.AssignSurrogate<SkillObject, StringIdSurrogate<SkillObject>>();
            modelGenerator.AssignSurrogate<PerkObject, StringIdSurrogate<PerkObject>>();
            modelGenerator.AssignSurrogate<CharacterAttribute, StringIdSurrogate<CharacterAttribute>>();

            // TODO make sure this works correctly
            // May need to request clan creation on server first
            modelGenerator.AssignSurrogate<Clan, StringIdSurrogate<Clan>>();
            #endregion

            modelGenerator.AssignSurrogate<TextObject, TextObjectSurrogate>();
            modelGenerator.AssignSurrogate<MBCharacterSkills, MBCharacterSkillsSurrogate>();

            modelGenerator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();
            modelGenerator.AssignSurrogate<HeroLastSeenInformation, HeroLastSeenInformationSurrogate>();

            // Library classes
            modelGenerator.AssignSurrogate<Vec3, Vec3Surrogate>();
            modelGenerator.AssignSurrogate<Vec2, Vec2Surrogate>();
            modelGenerator.AssignSurrogate<MatrixFrame, MatrixFrameSurrogate>();
            modelGenerator.AssignSurrogate<Mat3, Mat3Surrogate>();

            // TODO figure out
            modelGenerator.AssignSurrogate<IssueBase, IssueBaseSurrogate>();
            modelGenerator.AssignSurrogate<MobileParty, MobilePartySurrogate>();
            modelGenerator.AssignSurrogate<PartyBase, MobilePartySurrogate>();
        }
    }
}
