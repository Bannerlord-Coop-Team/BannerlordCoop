using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for Monster
    /// </summary>
    [Serializable]
    public class MonsterBinaryPackage : BinaryPackageBase<Monster>
    {

        public static string[] Excludes = new string[]
        {
            "_monsterMissionData",
        };


        public MonsterBinaryPackage(Monster obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(Excludes))
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
        }
    }
}
