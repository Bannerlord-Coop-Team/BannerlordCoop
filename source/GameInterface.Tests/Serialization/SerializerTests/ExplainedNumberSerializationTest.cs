using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ExplainedNumberSerializationTest
    {

        [Fact]
        public void ExplainedNumber_Serialize()
        {
            ExplainedNumber explainedNumberObject = new ExplainedNumber();         

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ExplainedNumberBinaryPackage package = new ExplainedNumberBinaryPackage(explainedNumberObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void ExplainedNumber_Full_Serialization()
        {
            ExplainedNumber explainedNumberObject = new ExplainedNumber(3_000.5F,true,new TextObject());
            
            BinaryPackageFactory factory = new BinaryPackageFactory();
            ExplainedNumberBinaryPackage package = new ExplainedNumberBinaryPackage(explainedNumberObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ExplainedNumberBinaryPackage>(obj);

            ExplainedNumberBinaryPackage returnedPackage = (ExplainedNumberBinaryPackage)obj;

            ExplainedNumber newExplainedNumberObject = returnedPackage.Unpack<ExplainedNumber>();

            Assert.Equal(explainedNumberObject.BaseNumber, newExplainedNumberObject.BaseNumber);

        }
    }
}
