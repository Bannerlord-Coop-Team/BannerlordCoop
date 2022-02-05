using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace Common.Tests
{
    public class TestModuleProvider : IModuleInfoProvider
    {
        public static ModuleInfo GameModule { get; private set; }
        public static ModuleInfo ModModule { get; private set; }

        static TestModuleProvider()
        {
            var gameVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 2, 3, 4, ApplicationVersionGameType.Multiplayer);
            var modVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 0, 1, 23, ApplicationVersionGameType.Multiplayer);

            GameModule = new ModuleInfo("TestModule1", true, gameVersion);
            ModModule = new ModuleInfo("TestModule2", false, modVersion);
        }

        public override List<ModuleInfo> GetModuleInfos()
        {
            return new List<ModuleInfo>
            {
                GameModule,
                ModModule
            };
        }
    }
    
    public class TestModuleProvider2 : IModuleInfoProvider
    {
        public static ModuleInfo GameModule { get; private set; }
        public static ModuleInfo ModModule { get; private set; }

        static TestModuleProvider2()
        {
            var gameVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 2, 4, 4, ApplicationVersionGameType.Multiplayer);
            var modVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 0, 2, 23, ApplicationVersionGameType.Multiplayer);

            GameModule = new ModuleInfo("TestModule1", true, gameVersion);
            ModModule = new ModuleInfo("TestModule2", false, modVersion);
        }

        public override List<ModuleInfo> GetModuleInfos()
        {
            return new List<ModuleInfo>
            {
                GameModule,
                ModModule
            };
        }
    }

    public class CompatibilityInfo_Test
    {
        [Fact]
        public void GameVersionEqualsOfficialModule()
        {
            CompatibilityInfo.ModuleProvider = new TestModuleProvider();
            var compatInfo = CompatibilityInfo.Get();

            Assert.Equal(2, compatInfo.Modules.Count);
            Assert.Equal(TestModuleProvider.GameModule.Version, compatInfo.GameVersion());
        }

        [Fact]
        public void GameVersionMatchesTest()
        {
            CompatibilityInfo.ModuleProvider = new TestModuleProvider();
            var compatInfo1 = CompatibilityInfo.Get();
            CompatibilityInfo.ModuleProvider = new TestModuleProvider2();
            var compatInfo2 = CompatibilityInfo.Get();

            Assert.True(compatInfo1.GameVersionMatches(compatInfo1));
            Assert.True(compatInfo2.GameVersionMatches(compatInfo2));
            Assert.False(compatInfo1.GameVersionMatches(compatInfo2));
            Assert.False(compatInfo2.GameVersionMatches(compatInfo1));
        }

        [Fact]
        public void CompatibleWithTest()
        {
            CompatibilityInfo.ModuleProvider = new TestModuleProvider();
            var compatInfo1 = CompatibilityInfo.Get();
            CompatibilityInfo.ModuleProvider = new TestModuleProvider2();
            var compatInfo2 = CompatibilityInfo.Get();

            Assert.False(compatInfo1.CompatibleWith(compatInfo2));
            Assert.False(compatInfo2.CompatibleWith(compatInfo1));
            Assert.True(compatInfo1.CompatibleWith(compatInfo1));
            Assert.True(compatInfo2.CompatibleWith(compatInfo2));
        }

        [Fact]
        public void SerializationTest()
        {
            CompatibilityInfo.ModuleProvider = new TestModuleProvider();
            var compatInfo1 = CompatibilityInfo.Get();
            CompatibilityInfo.ModuleProvider = new TestModuleProvider2();
            var compatInfo2 = CompatibilityInfo.Get();

            var serialized1 = compatInfo1.Serialize();
            var compatInfo1Deserialized = CompatibilityInfo.Deserialize(serialized1);
            
            var serialized2 = compatInfo2.Serialize();
            var compatInfo2Deserialized = CompatibilityInfo.Deserialize(serialized2);

            Assert.True(compatInfo1Deserialized.Equals(compatInfo1));
            Assert.True(compatInfo2Deserialized.Equals(compatInfo2));

            Assert.False(serialized1 == serialized2);
        }
    }
}
