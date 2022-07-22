using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.CoopBattle
{
    public class NetworkLooseWeapon : INetworkObject
    {
        public Guid networkId;

        public Guid NetworkId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Destroy(Type type)
        {
            throw new NotImplementedException();
        }

        public void Move()
        {
            throw new NotImplementedException();
        }

        public void OnDeath()
        {
            throw new NotImplementedException();
        }
    }
}
