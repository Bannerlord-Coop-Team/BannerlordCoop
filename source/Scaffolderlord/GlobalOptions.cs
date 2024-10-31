using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord
{
    public static class GlobalOptions
    {
        public static ILogger GlobalLogger { get; set; } = NullLogger.Instance;
        public static bool OverrideExistingFiles { get; set; } = false;
    }
}
