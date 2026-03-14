using GameInterface.AutoSync;
using Serilog;
using System.Reflection;

namespace GameInterface.AutoSyncPOC
{
    internal class AutoSyncRegistry : IAutoSyncBuilder
    {
        private readonly ILogger logger;
        private readonly IFieldRegistry fieldRegistry;
        private readonly IAutoSyncPatcher patcher;

        public AutoSyncRegistry(ILogger logger, IFieldRegistry fieldRegistry, IAutoSyncPatcher patcher)
        {
            this.logger = logger;
            this.fieldRegistry = fieldRegistry;
            this.patcher = patcher;
        }
        public void Dispose()
        {
        }

        public void AddField(FieldInfo field)
        {
            if (!fieldRegistry.TryAddField(field))
            {
                logger.Warning("Field was already registered {fieldName}", field);
                return;
            }
        }

        public void AddFieldChangeMethod(MethodBase methodBase)
        {
            throw new System.NotImplementedException();
        }

        public void AddProperty(PropertyInfo property)
        {
            throw new System.NotImplementedException();
        }

        public void Build()
        {
            patcher.PatchFields();
        }
    }
}
