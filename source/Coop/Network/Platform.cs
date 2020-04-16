using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Network
{
    public static class Platform
    {
        public static INetwork Create()
        {
            return new NetworkSteam();
        }
    }
}
