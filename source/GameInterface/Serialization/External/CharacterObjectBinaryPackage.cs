using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for CharacterObject
    /// </summary>
    [Serializable]
    public class CharacterObjectBinaryPackage : BinaryPackageBase<CharacterObject>
    {
        public static readonly FieldInfo CharacterObject_battleEquipmentTemplate = typeof(CharacterObject).GetField("_battleEquipmentTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo CharacterObject_civilianEquipmentTemplate = typeof(CharacterObject).GetField("_civilianEquipmentTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo CharacterObject_originCharacter = typeof(CharacterObject).GetField("_originCharacter", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly PropertyInfo CharacterObject_UpgradeTargets = typeof(CharacterObject).GetProperty(nameof(CharacterObject.UpgradeTargets));

        string battleEquipmentTemplateId;
        string civilianEquipmentTemplateId;
        string originCharacterId;
        string[] UpgradeTargetIds;

        public CharacterObjectBinaryPackage(CharacterObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public static readonly HashSet<string> Excludes = new HashSet<string>
        {
            "_battleEquipmentTemplate",
            "_civilianEquipmentTemplate",
            "_originCharacter",
            "<UpgradeTargets>k__BackingField",
        };

        protected override void PackInternal()
        {
            // Iterate through all of the instance fields of the object's type, excluding any fields that are specified in the Excludes collection
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(Excludes))
            {
                // Get the value of the current field in the object
                // Add a binary package of the field value to the StoredFields collection
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }

            // Get the value of the CharacterObject_battleEquipmentTemplate field in the object
            CharacterObject battleEquipmentTemplate = CharacterObject_battleEquipmentTemplate.GetValue<CharacterObject>(Object);
            battleEquipmentTemplateId = battleEquipmentTemplate?.StringId;

            // Get the value of the CharacterObject_civilianEquipmentTemplate field in the object
            CharacterObject civilianEquipmentTemplate = CharacterObject_civilianEquipmentTemplate.GetValue<CharacterObject>(Object);
            civilianEquipmentTemplateId = civilianEquipmentTemplate?.StringId;

            // Get the value of the CharacterObject_originCharacter field in the object
            CharacterObject originCharacter = CharacterObject_originCharacter.GetValue<CharacterObject>(Object);
            originCharacterId = originCharacter?.StringId;

            // Store the result of calling the PackIds method on the object's UpgradeTargets property in the UpgradeTargetIds variable
            UpgradeTargetIds = PackIds(Object.UpgradeTargets);
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            // Resolve Ids for StringId resolvable objects
            CharacterObject_battleEquipmentTemplate.SetValue(Object, ResolveId<CharacterObject>(battleEquipmentTemplateId));
            CharacterObject_civilianEquipmentTemplate.SetValue(Object, ResolveId<CharacterObject>(civilianEquipmentTemplateId));
            CharacterObject_originCharacter.SetValue(Object, ResolveId<CharacterObject>(originCharacterId));

            CharacterObject_UpgradeTargets.SetValue(Object, ResolveIds<CharacterObject>(UpgradeTargetIds).ToArray());
        }
    }
}