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

        protected CustomSerializerWithGuid() { }

        protected CustomSerializerWithGuid(object obj) : base(obj)
        {
            Guid = CoopObjectManager.AddObject(obj);
        }

        protected override object Deserialize(object newObj)
        {
            if (!CoopObjectManager.RegisterExistingObject(Guid, newObj))
            {
                return CoopObjectManager.GetObject(Guid);
            }

            return base.Deserialize(newObj);
        }
    }
}
