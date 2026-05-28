using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using GameInterface.Registry.Auto;
using GameInterface.Services.CharacterObjects;
using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
/// <summary>
/// Configures AutoSync for CharacterObject
/// </summary>
namespace GameInterface.Services.CharacterObjects
{
    internal class CharacterObjectSync : IDynamicSync
    {
        static readonly ILogger Logger = LogManager.GetLogger<CharacterObjectSync>();
        public CharacterObjectSync(DynamicSyncRegistry autoSyncBuilder)
        {
            //fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._characterRestrictionFlags)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._originCharacter)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._persona)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._characterTraits))); // surrogate probably
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._civilianEquipmentTemplate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._battleEquipmentTemplate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CharacterObject), nameof(CharacterObject._occupation)));

            //properties
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CharacterObject), nameof(CharacterObject.HiddenInEncyclopedia)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CharacterObject), nameof(CharacterObject.HeroObject)));
        }
    }
}
