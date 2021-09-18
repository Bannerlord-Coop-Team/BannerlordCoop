using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Coop.Tests.Network
{
    class ModuleInfoProviderMock : IModuleInfoProvider
    {
        public override List<ModuleInfo> GetModuleInfos()
        {
            var testModuleInfos = new List<ModuleInfo>();

            testModuleInfos.Add(new ModuleInfo("dummy", false,
                new ApplicationVersion(ApplicationVersionType.Development, 1, 2, 3, 4,
                     ApplicationVersionGameType.Multiplayer)));

            return testModuleInfos;
        }
    }
}
