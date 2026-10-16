using GameInterface.Services.Modules;
using System;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Internal
{
    /// <summary>
    /// Binary package for ModuleInfo.
    /// </summary>
    [Serializable]
    public class ModuleInfoBinaryPackage : BinaryPackageBase<ModuleInfo>
    {
        private string Id;
        private bool IsOfficial;
        private bool IsDlc;
        private string Version;
        private string Name;

        public ModuleInfoBinaryPackage(ModuleInfo obj, IBinaryPackageFactory binaryPackageFactory)
            : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            Id = Object.Id;
            IsOfficial = Object.IsOfficial;
            IsDlc = Object.IsDlc;
            Version = Object.Version.ToString();
            Name = Object.Name;
        }

        protected override void UnpackInternal()
        {
            Object = new ModuleInfo(Id, IsOfficial, IsDlc, ApplicationVersion.FromString(Version)) { Name = Name };
        }
    }
}
