using Common.Serialization;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils;
using GameInterface.Utils.AutoSync;
using GameInterface.Utils.AutoSync.Dynamic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.AutoSync;
public class EventGeneratorTests
{
    private int TestInt { get; set; } = 5;

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
        var dataType = dataClassCreator.GenerateClass(testIntProperty.PropertyType, testIntProperty.Name);
        var eventType = eventClassCreator.GenerateEvent(moduleBuilder, dataType);

        var dataClassInstance = Activator.CreateInstance(dataType, new object[] { "MyData", testIntProperty.GetValue(this)! });
        var eventInstance = Activator.CreateInstance(eventType, new object[] { dataClassInstance! });

        // Assert
        var eventData = eventType.GetProperty("Data")!.GetValue(eventInstance);

        Assert.NotNull(eventData);
        Assert.Equal(eventData, dataClassInstance);
    }
}
