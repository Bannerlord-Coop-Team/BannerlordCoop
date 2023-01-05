using GameInterface.Serialization;
using GameInterface.Serialization.Internal;
using GameInterface.Tests.Serialization;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;
using Xunit.Sdk;

namespace GameInterface.Tests
{
    public class TestModuleProvider : IModuleInfoProvider
    {
        public static ModuleInfo GameModule { get; private set; }
        public static ModuleInfo ModModule { get; private set; }

        static TestModuleProvider()
        {
            var gameVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 2, 3, 4);
            var modVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 0, 1, 23);

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
            var gameVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 2, 4, 4);
            var modVersion = new ApplicationVersion(ApplicationVersionType.EarlyAccess, 1, 0, 2, 23);

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

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CompatibilityInfoBinaryPackage package = new CompatibilityInfoBinaryPackage(compatInfo, factory);

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

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CompatibilityInfoBinaryPackage package1 = new CompatibilityInfoBinaryPackage(compatInfo1, factory);
            CompatibilityInfoBinaryPackage package2 = new CompatibilityInfoBinaryPackage(compatInfo2, factory);

            package1.Pack();
            package2.Pack();

            byte[] bytes1 = BinaryFormatterSerializer.Serialize(package1);
            byte[] bytes2 = BinaryFormatterSerializer.Serialize(package2);

            Assert.NotEmpty(bytes1);
            Assert.NotEmpty(bytes2);
        }

        [Fact]
        public void FullSerializationTest()
        {
            CompatibilityInfo.ModuleProvider = new TestModuleProvider();
            var compatInfo1 = CompatibilityInfo.Get();
            CompatibilityInfo.ModuleProvider = new TestModuleProvider2();
            var compatInfo2 = CompatibilityInfo.Get();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CompatibilityInfoBinaryPackage package1 = new CompatibilityInfoBinaryPackage(compatInfo1, factory);
            CompatibilityInfoBinaryPackage package2 = new CompatibilityInfoBinaryPackage(compatInfo2, factory);

            package1.Pack();
            package2.Pack();

            byte[] bytes1 = BinaryFormatterSerializer.Serialize(package1);
            byte[] bytes2 = BinaryFormatterSerializer.Serialize(package2);

            Assert.NotEmpty(bytes1);
            Assert.NotEmpty(bytes2);

            object obj1 = BinaryFormatterSerializer.Deserialize(bytes1);
            object obj2 = BinaryFormatterSerializer.Deserialize(bytes2);

            Assert.IsType<CompatibilityInfoBinaryPackage>(obj1);
            Assert.IsType<CompatibilityInfoBinaryPackage>(obj2);

            CompatibilityInfoBinaryPackage returnedPackage1 = (CompatibilityInfoBinaryPackage)obj1;
            CompatibilityInfoBinaryPackage returnedPackage2 = (CompatibilityInfoBinaryPackage)obj2;

            CompatibilityInfo ret1 = returnedPackage1.Unpack<CompatibilityInfo>();
            CompatibilityInfo ret2 = returnedPackage2.Unpack<CompatibilityInfo>();

            Assert.False(ret1 == ret2);
            Assert.Equal(ret1, compatInfo1);
            Assert.Equal(ret2, compatInfo2);
        }
    }
}
