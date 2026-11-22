using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CraftingTemplateSerializationTest
    {
        ILifetimeScope container;
        public CraftingTemplateSerializationTest()
        {
            GameBootStrap.Initialize();

            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void CraftingTemplate_Full_Serialization()
        {
            CraftingTemplate testCraftingTemplate = (CraftingTemplate)FormatterServices.GetUninitializedObject(typeof(CraftingTemplate));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingTemplateBinaryPackage package = new CraftingTemplateBinaryPackage(testCraftingTemplate, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<CraftingTemplateBinaryPackage>(obj);

            CraftingTemplateBinaryPackage returnedPackage = (CraftingTemplateBinaryPackage)obj;

            Assert.Equal(returnedPackage.templateId, package.templateId);
        }
    }
}
