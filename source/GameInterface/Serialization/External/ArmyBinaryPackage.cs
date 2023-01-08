using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using System.Collections.Generic;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Army
    /// </summary>
    [Serializable]
    public class ArmyBinaryPackage : BinaryPackageBase<Army>
    {

        private static readonly MethodInfo AddEventHandlers = typeof(Army).GetMethod("AddEventHandlers", BindingFlags.NonPublic | BindingFlags.Instance);
        public ArmyBinaryPackage(Army obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_hourlyTickEvent",
            "_tickEvent",
        };

        protected override void PackInternal()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            // Resolves _hourlyTickEvent and _tickEvent
            AddEventHandlers.Invoke(Object, new object[0]);

            // Resolves _armiesCache for Kingdom
            if (Object.Kingdom != null)
            {
                List<Army> kingdomArmies = (List<Army>)KingdomBinaryPackage.Kingdom_Armies.GetValue(Object.Kingdom);
                if (kingdomArmies.Contains(Object) == false)
                {
                    kingdomArmies.Add(Object);
                }
            }
        }
    }
}
