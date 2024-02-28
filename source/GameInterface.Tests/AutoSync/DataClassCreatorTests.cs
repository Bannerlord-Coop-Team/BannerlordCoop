using Common.Serialization;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils.AutoSync.Dynamic;
using GameInterface.Utils.AutoSync.Example;
using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.AutoSync;
public class DataClassCreatorTests
{
    private int TestInt { get; set; } = 5;

    [Fact]
    public void CreateDataClass()
    {
        // Arrange
        var typeMapper = new SerializableTypeMapper();
        var serializer = new ProtoBufSerializer(typeMapper);

        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var dataClassCreator = new DataClassGenerator(moduleBuilder);

        var testIntProperty = AccessTools.Property(typeof(DataClassCreatorTests), nameof(TestInt));

        // Act
        var dataClassType = dataClassCreator.GenerateClass(testIntProperty.PropertyType, testIntProperty.Name);

        var expectedStringId = "MyData";
        var expectedValue = testIntProperty.GetValue(this)!;
        var dataClassObj = Activator.CreateInstance(dataClassType, new object[] { expectedStringId, expectedValue })!;

        typeMapper.AddTypes(new Type[] { dataClassObj.GetType() });

        // Assert
        Assert.True(Serializer.NonGeneric.CanSerialize(dataClassType));

        var bytes = serializer.Serialize(dataClassObj);

        var deserializedObject = serializer.Deserialize(bytes);

        var stringIdGetter = dataClassType.GetProperty("StringId")!.GetGetMethod()!;
        Assert.Equal(expectedStringId, stringIdGetter.Invoke(dataClassObj, Array.Empty<object>()));

        var valueGetter = dataClassType.GetProperty("Value")!.GetGetMethod()!;
        Assert.Equal(expectedValue, valueGetter.Invoke(dataClassObj, Array.Empty<object>()));

        Assert.Equal(dataClassObj, deserializedObject);
    }
}
