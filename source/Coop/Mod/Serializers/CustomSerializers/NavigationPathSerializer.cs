using System;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class NavigationPathSerializer : ICustomSerializer
    {
        private NavigationPath navigationPath;

        public NavigationPathSerializer(NavigationPath navigationPath)
        {
            this.navigationPath = navigationPath;
        }

        public object Deserialize()
        {
            return navigationPath;
        }
    }
}