using GameInterface.Serialization.Dynamic;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class WeaponDesignSerializationTests
    {
        private readonly ITestOutputHelper output;
        private readonly Harmony harmony;
        public WeaponDesignSerializationTests(ITestOutputHelper output)
        {
            harmony = new Harmony($"testing.{GetType()}");
            harmony.PatchAll();
            this.output = output;
        }

        [Fact]
        public void NominalWeaponDesignObjectSerialization()
        {
            var testModel = MakeWeaponDesignSerializable();

            WeaponDesign weaponDesign = new WeaponDesign(
                new CraftingTemplate(), 
                new TextObject("TestDesign"), 
                new WeaponDesignElement[]
                {
                    WeaponDesignElement.CreateUsablePiece(new CraftingPiece()),
                    WeaponDesignElement.CreateUsablePiece(new CraftingPiece()),
                    WeaponDesignElement.CreateUsablePiece(new CraftingPiece()),
                });

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(weaponDesign);

            WeaponDesign newWeaponDesign = ser.Deserialize<WeaponDesign>(data);

            Assert.NotNull(newWeaponDesign);
        }

        [Fact]
        public void NullWeaponDesignObjectSerialization()
        {
            var testModel = MakeWeaponDesignSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            WeaponDesign newWeaponDesign = ser.Deserialize<WeaponDesign>(data);

            Assert.Null(newWeaponDesign);
        }

        private RuntimeTypeModel MakeWeaponDesignSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<WeaponDesign>();
            generator.CreateDynamicSerializer<WeaponDesignElement>();
            generator.CreateDynamicSerializer<WeaponDescription>();
            generator.CreateDynamicSerializer<CraftingPiece>();
            generator.CreateDynamicSerializer<PieceData>();
            generator.CreateDynamicSerializer<BladeData>();

            generator.AssignSurrogate<CraftingTemplate, SurrogateStub<CraftingTemplate>>();
            generator.AssignSurrogate<TextObject, SurrogateStub<TextObject>>();
            generator.AssignSurrogate<Vec3, SurrogateStub<Vec3>>();
            generator.AssignSurrogate<BasicCultureObject, SurrogateStub<BasicCultureObject>>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(WeaponDesign)));

            return testModel;
        }
    }

    [HarmonyPatch(typeof(WeaponDesign))]
    class WeaponDesignConstructorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("CalculatePivotDistances")]
        public static bool CalculatePivotDistancesPrefix()
        {
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CalculateWeaponLength")]
        public static bool CalculateWeaponLengthPrefix()
        {
            return false;
        }

        

        [HarmonyPrefix]
        [HarmonyPatch("CalculateHolsterShiftAmount")]
        public static bool CalculateHolsterShiftAmountPrefix()
        {
            return false;
        }
    }
}
