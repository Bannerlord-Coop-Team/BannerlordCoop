using System;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class MBGUIDSerializer : ICustomSerializer
    {
        private uint id;
        public MBGUIDSerializer(MBGUID _MBGUID)
        {
            id = _MBGUID.InternalValue;
        }

        public object Deserialize()
        {
            return new MBGUID(id);
        }
    }
}
