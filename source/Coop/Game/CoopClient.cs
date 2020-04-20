using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Game
{
    public static class CoopClient
    {
        public static WeakReference<ClientModel> Client = new WeakReference<ClientModel>(null);
    }
}
