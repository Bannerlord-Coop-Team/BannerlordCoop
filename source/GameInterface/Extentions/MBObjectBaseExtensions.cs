using GameInterface.Services;
using GameInterface.Services.Entity;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Extentions
{
    public static class MBObjectBaseExtensions
    {
        public static bool IsControlled(this MBObjectBase obj)
        {
            var controlledEntityRegistry = ServiceLocator.Resolve<IControlledEntityRegistery>();
            return controlledEntityRegistry.IsOwned(obj.StringId);
        }
    }
}
