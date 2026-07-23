using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEngineTypeBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new SiegeEngineType();
        }
    }
}