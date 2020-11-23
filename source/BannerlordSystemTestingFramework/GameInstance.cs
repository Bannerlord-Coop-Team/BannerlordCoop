using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BannerlordSystemTestingLibrary
{
    public class GameInstance : IDisposable
    {
        GameProcess process;
        public GameInstance(string args)
        {
            process = new GameProcess(args);
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            process.Start();

            // Wait for menu to be ready
        }

        public void Stop()
        {
            process.Kill();
        }
    }
}
