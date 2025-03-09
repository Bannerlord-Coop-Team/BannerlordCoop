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

        string stringId;

        string battleEquipmentTemplateId;
        string civilianEquipmentTemplateId;
        string originCharacterId;
        string[] UpgradeTargetIds;

        public CharacterObjectBinaryPackage(CharacterObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
            stringId = ResolveId(Object) ?? string.Empty;

            base.PackFields(Excludes);

            // Get the value of the CharacterObject_battleEquipmentTemplate field in the object
            battleEquipmentTemplateId = ResolveId(Object._battleEquipmentTemplate);

            // Get the value of the CharacterObject_civilianEquipmentTemplate field in the object
            civilianEquipmentTemplateId = ResolveId(Object._civilianEquipmentTemplate);

            // Get the value of the CharacterObject_originCharacter field in the object
            originCharacterId = ResolveId(Object._originCharacter);

            // Store the result of calling the PackIds method on the object's UpgradeTargets property in the UpgradeTargetIds variable
            UpgradeTargetIds = ResolveIds(Object.UpgradeTargets);
        }

        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<CharacterObject>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            base.UnpackFields();

            // Resolve Ids for StringId resolvable objects
            Object._battleEquipmentTemplate = ResolveObject<CharacterObject>(battleEquipmentTemplateId);
            Object._civilianEquipmentTemplate = ResolveObject<CharacterObject>(civilianEquipmentTemplateId);
            Object._originCharacter = ResolveObject<CharacterObject>(originCharacterId);

            Object.UpgradeTargets = ResolveObjects<CharacterObject>(UpgradeTargetIds).ToArray();
        }
    }
}