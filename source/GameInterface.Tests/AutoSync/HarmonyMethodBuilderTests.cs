using Autofac;
using GameInterface.Policies;
using GameInterface.Utils.AutoSync.Dynamic;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests.AutoSync;

/// <summary>
/// Tests for the HarmonyMethodBuilder class.
/// </summary>
public class HarmonyMethodBuilderTests
{
    private int TestInt { get; set; } = 5;
    public string StringId = "TestStringId";

    /// <summary>
    /// Test method to verify the functionality of creating a Harmony method.
    /// </summary>
    [Fact]
    public void CreateHarmonyMethod()
    {
        var containerBuilder = new ContainerBuilder();

        containerBuilder.RegisterType<DummyPolicy>().As<ISyncPolicy>();

        var container = containerBuilder.Build();

        ContainerProvider.SetContainer(container);

        ModInformation.IsServer = true;

        // Arrange
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var testIntProperty = AccessTools.Property(typeof(HarmonyMethodBuilderTests), nameof(TestInt));
        var eventClassCreator = new EventMessageGenerator();

        var dataClassType = new DataClassGenerator(moduleBuilder).GenerateClass(testIntProperty.PropertyType, "TestObjectData");
        var eventType = eventClassCreator.GenerateEvent(moduleBuilder, testIntProperty);

        var methodGenerator = new HarmonyPatchGenerator(moduleBuilder, testIntProperty, dataClassType, eventType);

        Assert.NotNull(AccessTools.Field(GetType(), "StringId"));

        // Act
        var patchMethod = methodGenerator.GenerateSetterPrefixPatch<HarmonyMethodBuilderTests>(
            testIntProperty.GetSetMethod(true), IdGetterMethod);


        Harmony harmony = new Harmony("This is a test");

        harmony.Patch(testIntProperty.GetSetMethod(true), prefix: new HarmonyMethod(patchMethod));

        // Assert
        TestInt += 1;
    }

    /// <summary>
    /// Dummy policy class for testing purposes.
    /// </summary>
    class DummyPolicy : ISyncPolicy
    {
        /// <summary>
        /// When true this skips patch functionality.
        /// </summary>
        public bool AllowOriginal() => false;
    }

    /// <summary>
    /// Method to get the ID for testing purposes.
    /// </summary>
    /// <param name="instance">An instance of HarmonyMethodBuilderTests.</param>
    /// <returns>The string ID.</returns>
    public static string IdGetterMethod(HarmonyMethodBuilderTests instance)
    {
        return instance.StringId;
    }
}