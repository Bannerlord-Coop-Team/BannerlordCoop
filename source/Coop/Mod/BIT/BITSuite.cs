using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.BIT
{
    class BITSuite
    {
        public static BITSuite Instance { get { return lazy.Value; } }
        
        public BITSuite()
        {
            communicator.SendData("Hello");
        }

        #region Private
        private static readonly Lazy<BITSuite> lazy = new Lazy<BITSuite>(() => new BITSuite());
        private static RunnerCommunicator communicator = new RunnerCommunicator();
        #endregion


    }
}
