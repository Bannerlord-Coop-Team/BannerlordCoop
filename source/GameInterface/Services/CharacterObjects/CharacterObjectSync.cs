using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects
{
    internal class CharacterObjectSync : IAutoSync
    {
        public CharacterObjectSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CharacterObject), nameof(CharacterObject.HiddenInEncylopedia)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CharacterObject), nameof(CharacterObject.HeroObject)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._characterRestrictionFlags)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._originCharacter)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._persona)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._characterTraits)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._civilianEquipmentTemplate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._battleEquipmentTemplate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._occupation)));
        }
    }
}
