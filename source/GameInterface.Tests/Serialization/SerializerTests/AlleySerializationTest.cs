﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class AlleySerializationTest
    {
        IContainer container;
        public AlleySerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Alley_Serialize()
        {
            Alley alley = (Alley)FormatterServices.GetUninitializedObject(typeof(Alley));
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement.StringId = "My Settlement";
            MBObjectManager.Instance.RegisterObject(settlement);

            Alley_Settlement.SetValue(alley, settlement);

            List<Alley> alleys = new List<Alley>
            {
                alley,
            };

            Settlement_Alleys.SetValue(settlement, alleys);

            var factory = container.Resolve<IBinaryPackageFactory>();
            AlleyBinaryPackage package = new AlleyBinaryPackage(alley, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly PropertyInfo Settlement_Alleys = typeof(Settlement).GetProperty(nameof(Settlement.Alleys));
        
        private static readonly FieldInfo Alley_Owner = typeof(Alley).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Alley_Settlement = typeof(Alley).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Alley_Name = typeof(Alley).GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo Alley_Tag = typeof(Alley).GetField("_tag", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo Alley_State = typeof(Alley).GetProperty(nameof(Alley.State));


        [Fact]
        public void Alley_Full_Serialization()
        {            
            Alley alley = (Alley)FormatterServices.GetUninitializedObject(typeof(Alley));
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            Hero owner = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            // Register object with new string id
            var objectManager = container.Resolve<IObjectManager>();

            // Register owner with MBObjectManager
            owner.StringId = "My Hero";
            objectManager.AddExisting(owner.StringId, owner);

            // Register settlement with MBObjectManager
            settlement.StringId = "My Settlement";
            objectManager.AddExisting(settlement.StringId, settlement);

            // Attach settlement and commonArea
            Alley_Settlement.SetValue(alley, settlement);

            List<Alley> alleys = new List<Alley>
            {
                alley,
            };

            Settlement_Alleys.SetValue(settlement, alleys);

            // Assign common area with setup values
            Alley_Owner.SetValue(alley, owner);
            Alley_Settlement.SetValue(alley, settlement);
            Alley_State.SetValue(alley, Alley.AreaState.OccupiedByPlayer);
            Alley_Name.SetValue(alley, new TextObject("TestName"));
            Alley_Tag.SetValue(alley, "TestTag");

            var factory = container.Resolve<IBinaryPackageFactory>();
            AlleyBinaryPackage package = new AlleyBinaryPackage(alley, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<AlleyBinaryPackage>(obj);

            AlleyBinaryPackage returnedPackage = (AlleyBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Alley newAlley = returnedPackage.Unpack<Alley>(deserializeFactory);

            Assert.Same(alley.Owner, newAlley.Owner);
            Assert.Same(alley.Settlement, newAlley.Settlement);
            Assert.Equal(alley.State, newAlley.State);
            Assert.Equal(alley.Name, newAlley.Name);
            Assert.Equal(alley.Tag, newAlley.Tag);
        }
    }
}
