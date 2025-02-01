using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects
{
    internal class BasicCharacterObjectSync : IAutoSync
    {
        public BasicCharacterObjectSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Age)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BeardTags)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Culture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultFormationClass)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultFormationGroup)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DismountResistance)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BodyPropertyRange)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FaceDirtAmount)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FaceMeshCache)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FormationPositionPreference)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.HairTags)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsFemale)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsObsolete)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsSoldier)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.KnockbackResistance)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.KnockdownResistance)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Level)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Race)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.TattooTags)));
        }
    }
}
