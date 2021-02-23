using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    /// <summary>
    /// For serializing already existing objects on server
    /// </summary>
    [Serializable]
    class MBObjectSerializer : ICustomSerializer
    {
        MBGUIDSerializer ID;
        public MBObjectSerializer(MBObjectBase value)
        {
            ID = new MBGUIDSerializer(value.Id);
        }

        public virtual object Deserialize()
        {
            return MBObjectManager.Instance.GetObject((MBGUID)ID.Deserialize());
        }
    }
}
