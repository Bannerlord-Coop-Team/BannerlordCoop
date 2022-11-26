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
        public string StringId;

        public MonsterBinaryPackage(Monster obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            StringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            Game.Current.ObjectManager.GetObject<Monster>(StringId);
        }
    }
}
