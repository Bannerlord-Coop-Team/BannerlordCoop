using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace Coop
{
    class CoopObjectManager
    {
        public static readonly Dictionary<MBGUID, MBGUID> ObjectIdMap = new Dictionary<MBGUID, MBGUID>();
    }
}
