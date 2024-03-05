using Common.Serialization;
using GameInterface.Utils.AutoSync.Dynamic;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests.AutoSync;
/// <summary>
/// Tests for the DataClassCreator class.
/// </summary>
public class DataClassCreatorTests
{
    private int TestInt { get; set; } = 5;

    /// <summary>
    /// Test method to verify the functionality of creating a data class.
    /// </summary>
    [Fact]
    public void CreateDataClass()
    {
        // Arrange
        var typeMapper = new SerializableTypeMapper();
        var serializer = new ProtoBufSerializer(typeMapper);

        // Define dynamic assembly and module
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        // Initialize data class generator
        var dataClassCreator = new DataClassGenerator(moduleBuilder);

        // Get test property
        var testIntProperty = AccessTools.Property(typeof(DataClassCreatorTests), nameof(TestInt));

        // Act
        // Generate data class type
        var dataClassType = dataClassCreator.GenerateClass(testIntProperty.PropertyType, testIntProperty.Name);

        // Create an instance of the data class
        var expectedStringId = "MyData";
        var expectedValue = testIntProperty.GetValue(this)!;
        var dataClassObj = Activator.CreateInstance(dataClassType, new object[] { expectedStringId, expectedValue })!;

        // Add generated class type to the type mapper
        typeMapper.AddTypes(new Type[] { dataClassObj.GetType() });

        // Assert
        // Verify serialization capability
        Assert.True(Serializer.NonGeneric.CanSerialize(dataClassType));

        // Serialize the data class object
        var bytes = serializer.Serialize(dataClassObj);

        // Deserialize the serialized object
        var deserializedObject = serializer.Deserialize(bytes);

        // Verify stringId property value
        var stringIdGetter = dataClassType.GetProperty("StringId")!.GetGetMethod()!;
        Assert.Equal(expectedStringId, stringIdGetter.Invoke(dataClassObj, Array.Empty<object>()));

        // Verify value property value
        var valueGetter = dataClassType.GetProperty("Value")!.GetGetMethod()!;
        Assert.Equal(expectedValue, valueGetter.Invoke(dataClassObj, Array.Empty<object>()));

        // Verify object equality after serialization and deserialization
        Assert.Equal(dataClassObj, deserializedObject);
    }
}