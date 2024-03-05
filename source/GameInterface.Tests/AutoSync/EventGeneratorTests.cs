using GameInterface.Utils.AutoSync.Dynamic;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests.AutoSync;
/// <summary>
/// Tests for the EventGenerator class.
/// </summary>
public class EventGeneratorTests
{
    private int TestInt { get; set; } = 5;

    /// <summary>
    /// Test method to verify the functionality of creating an event class.
    /// </summary>
    [Fact]
    public void CreateEventClass()
    {
        // Arrange
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var dataClassCreator = new DataClassGenerator(moduleBuilder);
        var eventClassCreator = new EventMessageGenerator();

        var testIntProperty = AccessTools.Property(typeof(EventGeneratorTests), nameof(TestInt));

        // Act
        // Generate data class type
        var dataType = dataClassCreator.GenerateClass(testIntProperty.PropertyType, testIntProperty.Name);

        // Generate event class type
        var eventType = eventClassCreator.GenerateEvent(moduleBuilder, testIntProperty);

        // Create instances of data class and event class
        var dataClassInstance = Activator.CreateInstance(dataType, new object[] { "MyData", testIntProperty.GetValue(this)! });
        var eventInstance = Activator.CreateInstance(eventType, new object[] { dataClassInstance! });

        // Assert
        // Verify that data property of event class is not null and equals the data class instance
        var eventData = eventType.GetProperty("Data")!.GetValue(eventInstance);

        Assert.NotNull(eventData);
        Assert.Equal(eventData, dataClassInstance);
    }
}