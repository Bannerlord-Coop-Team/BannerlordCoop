using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core
{
    public static class ModContext
    {
        public static ICoopInstanceInfo Current { get; internal set; }
    }
}
