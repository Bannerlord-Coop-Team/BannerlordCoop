using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CraftingTemplateSerializationTest
    {
        IContainer container;
        public CraftingTemplateSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CraftingTemplate_Serialize()
        {
            CraftingTemplate testCraftingTemplate = (CraftingTemplate)FormatterServices.GetUninitializedObject(typeof(CraftingTemplate));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingTemplateBinaryPackage package = new CraftingTemplateBinaryPackage(testCraftingTemplate, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CraftingTemplate_Full_Serialization()
        {
            CraftingTemplate testCraftingTemplate = (CraftingTemplate)FormatterServices.GetUninitializedObject(typeof(CraftingTemplate));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingTemplateBinaryPackage package = new CraftingTemplateBinaryPackage(testCraftingTemplate, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CraftingTemplateBinaryPackage>(obj);

            CraftingTemplateBinaryPackage returnedPackage = (CraftingTemplateBinaryPackage)obj;

            Assert.Equal(returnedPackage.templateId, package.templateId);
        }
    }
}
