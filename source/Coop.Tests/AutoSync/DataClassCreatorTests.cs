using Common.Serialization;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils.AutoSync.Dynamic;
using GameInterface.Utils.AutoSync.Example;
using HarmonyLib;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace Coop.Tests.AutoSync;
public class DataClassCreatorTests
{
    private int TestInt { get; set; } = 5;

    [Fact]
    public void CreateDataClass()
    {
        // Arrange
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var dataClassCreator = new DataClassGenerator(moduleBuilder);

        var TestIntProperty = AccessTools.Property(typeof(DataClassCreatorTests), nameof(TestInt));

        // Act
        var dataClassType = dataClassCreator.GenerateClass(TestIntProperty.PropertyType, TestIntProperty.Name);

        var dataClassObj = Activator.CreateInstance(dataClassType, new object[] { "MyData", TestIntProperty.GetValue(this)! });
        var t = typeof(int);
        // Assert
        Assert.True(Serializer.NonGeneric.CanSerialize(dataClassType));

        byte[] data;
        using (MemoryStream WrapperStream = new MemoryStream())
        {
            Serializer.Serialize(WrapperStream, dataClassObj.GetType());
            //data = WrapperStream.ToArray();

            var obj2 = Serializer.Deserialize<Type>(WrapperStream,);

            ;
        }

        using (MemoryStream WrapperStream = new MemoryStream(data))
        { 
            var obj2 = Serializer.Deserialize<Type>(WrapperStream);
            ;
        }


            var bytes = ProtoBufSerializer.Serialize(dataClassObj);

        var obj = ProtoBufSerializer.Deserialize(bytes);

        ;
    }

    [ProtoContract(SkipConstructor = true)]
    internal class ProtoMessageWrapper2
    {
        [ProtoMember(1)]
        public Type Type { get; }
        [ProtoMember(2)]
        public byte[] ContractData { get; }

        public ProtoMessageWrapper2(Type type, byte[] contractData)
        {
            //Type = type;
            ContractData = contractData;
        }
    }
}
