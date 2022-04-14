using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public abstract class CustomSerializerWithGuid : CustomSerializer
    {
        public Guid Guid { get; private set; }

        protected CustomSerializerWithGuid(object obj) : base(obj)
        {
            Guid = CoopObjectManager.AddObject(obj);
        }

        protected override object Deserialize(object newObj)
        {
            object existingObj = CoopObjectManager.GetObject(Guid);
            if(existingObj != null)
            {
                return existingObj;
            }

            object deserialized = base.Deserialize(newObj);
            CoopObjectManager.RegisterExistingObject(Guid, deserialized);
            return deserialized;
        }
    }
}
