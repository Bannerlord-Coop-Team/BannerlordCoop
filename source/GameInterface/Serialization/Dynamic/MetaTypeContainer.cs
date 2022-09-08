using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Serialization.Dynamic
{
    public class MetaTypeContainer : IMetaTypeContainer
    {
        readonly MetaType metaType;
        int numFields;

        public MetaTypeContainer(MetaType metaType)
        {
            this.metaType = metaType;
            numFields = metaType.GetFields().Length + 1;
        }

        public IMetaTypeContainer AddDerivedType<T>()
        {
            metaType.AddSubType(numFields++, typeof(T));
            return this;
        }
    }
}
