﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WorkshopSerializationTest
    {
        IContainer container;
        public WorkshopSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Workshop_Serialize()
        {
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            Town town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            // Setup town to be referencable by StringId
            town.StringId = "myTown";

            MBObjectManager.Instance.RegisterObject(town);

            // Set town of workshop settlement
            settlement.Town = town;

            Workshop Workshop = new Workshop(settlement, "ws");

            Town_Workshops.SetValue(town, new Workshop[] { Workshop });

            // Setup serialization
            var factory = container.Resolve<IBinaryPackageFactory>();
            WorkshopBinaryPackage package = new WorkshopBinaryPackage(Workshop, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo Town_Workshops = typeof(Town).GetProperty(nameof(Town.Workshops));
        [Fact]
        public void Workshop_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            Town town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            // Setup town to be referencable by StringId
            town.StringId = "myTown";

            objectManager.AddExisting(town.StringId, town);

            // Set town of workshop settlement
            settlement.Town = town;

            Workshop Workshop = new Workshop(settlement, "ws");

            Town_Workshops.SetValue(town, new Workshop[] { Workshop });

            // Setup serialization
            var factory = container.Resolve<IBinaryPackageFactory>();
            WorkshopBinaryPackage package = new WorkshopBinaryPackage(Workshop, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WorkshopBinaryPackage>(obj);

            WorkshopBinaryPackage returnedPackage = (WorkshopBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Workshop newWorkshop = returnedPackage.Unpack<Workshop>(deserializeFactory);

            Assert.Same(Workshop, newWorkshop);
        }
    }
}
