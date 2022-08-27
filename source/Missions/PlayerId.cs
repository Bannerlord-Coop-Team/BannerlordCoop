using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions
{
    public struct PlayerId
    {
        public Guid Id;
        public PlayerId(Guid id)
        {
            Id = id;
        }
    }
}
