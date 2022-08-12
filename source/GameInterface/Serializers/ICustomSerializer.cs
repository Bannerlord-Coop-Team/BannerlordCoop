using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Serializers
{
    public interface ICustomSerializer
    {
        byte[] Serialize();
        object Deserialize();
        void ResolveReferenceGuids();
    }
}
