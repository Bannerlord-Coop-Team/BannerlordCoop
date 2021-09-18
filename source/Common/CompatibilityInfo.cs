using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using TaleWorlds.Library;

namespace Common
{
    [Serializable]
    public struct Version
    {
        public int Major;
        public int Minor;
        public int Revision;
        public int ChangeSet;

        public Version(int major, int minor, int revision, int changeSet)
        {
          Major = major;
          Minor = minor;
          Revision = revision;
          ChangeSet = changeSet;
        }

        public Version(ApplicationVersion applicationVersion)
        {
          Major = applicationVersion.Major;
          Minor = applicationVersion.Minor;
          Revision = applicationVersion.Revision;
          ChangeSet = applicationVersion.ChangeSet;
        }
    }

    [Serializable]
    public struct ModuleInfo
    {
        public string Id { get; set; }
        public bool IsOfficial { get; set; }
        public Version Version { get; set; }

        public ModuleInfo(string id, bool isOfficial, ApplicationVersion applicationVersion)
        {
            Id = id;
            IsOfficial = isOfficial;
            Version = new Version(applicationVersion);
        }
    }

    public abstract class IModuleInfoProvider
    {
      public abstract List<ModuleInfo> GetModuleInfos();
    }

    public class TaleWorldsModuleInfoProvider : IModuleInfoProvider
    {
      public override List<ModuleInfo> GetModuleInfos()
      {
          var modules = TaleWorlds.ModuleManager.ModuleHelper.GetModules();
          var moduleInfos = new List<ModuleInfo>();
          foreach (TaleWorlds.ModuleManager.ModuleInfo moduleInfo in modules)
          {
              if (!moduleInfo.IsSelected)
                continue;
              moduleInfos.Add(new ModuleInfo(moduleInfo.Id, moduleInfo.IsOfficial, moduleInfo.Version));
          }

          return moduleInfos;
      }
    }

    [Serializable]
    public class CompatibilityInfo
    {
        public List<ModuleInfo> Modules { get; private set; }
        public static IModuleInfoProvider ModuleProvider { get; set; }

        static CompatibilityInfo()
        {
            ModuleProvider = new TaleWorldsModuleInfoProvider();
        }

        public static CompatibilityInfo Get()
        {
            CompatibilityInfo info = new CompatibilityInfo();

            if (ModuleProvider == null)
                return info;

            foreach (ModuleInfo moduleInfo in ModuleProvider.GetModuleInfos())
            {
                info.AddModule(moduleInfo);
            }

            return info;
        }

        public Version GameVersion()
        {
            return Modules.Find(m => m.IsOfficial).Version;
        }

        public bool CompatibleWith(CompatibilityInfo other)
        {
            foreach (ModuleInfo ourModule in Modules)
            {
                if (!other.Modules.Contains(ourModule))
                    return false;
            }
            return true;
        }

        public bool GameVersionMatches(CompatibilityInfo other)
        {
            return GameVersion().Equals(other.GameVersion());
        }

        public override bool Equals(object obj)
        {
            if (obj is CompatibilityInfo)
            {
                return CompatibleWith(obj as CompatibilityInfo);
            }
            return false;
        }

        public static CompatibilityInfo Deserialize(byte[] serialized)
        {
            var serializedString = System.Text.Encoding.ASCII.GetString(serialized);
            var stringReader = new System.IO.StringReader(serializedString);
            var serializer = new XmlSerializer(typeof(CompatibilityInfo));

            return (CompatibilityInfo)serializer.Deserialize(stringReader);
        }

        public byte[] Serialize()
        {
            var stringWriter = new System.IO.StringWriter();
            var serializer = new XmlSerializer(typeof(CompatibilityInfo));
            serializer.Serialize(stringWriter, this);

            var serializedString = stringWriter.GetStringBuilder().ToString();

            return System.Text.Encoding.ASCII.GetBytes(serializedString);
        }

        private CompatibilityInfo()
        {
            Modules = new List<ModuleInfo>();
        }

        private void AddModule(ModuleInfo moduleInfo)
        {
            Modules.Add(moduleInfo);
        }

    }
}
