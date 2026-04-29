using Common.Serialization;
using GameInterface.AutoSync.Fields;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.AutoSync;
public class AutoSyncTests
{
    private readonly ITestOutputHelper output;

    public AutoSyncTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TypeSwitchingTest()
    {
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var objectManager = new Mock<IObjectManager>();

        var typeSwitchCreator = new FieldTypeSwitchCreator(moduleBuilder);

        var typeMap = new Dictionary<Type, List<FieldInfo>>
        {
            { typeof(Settlement), new List<FieldInfo>() },
            { typeof(MobileParty), new List<FieldInfo>() },
        };

        var typeSwitchType = typeSwitchCreator.Build(typeMap);
        dynamic typeSwitch = Activator.CreateInstance(typeSwitchType, objectManager.Object)!;

        typeSwitch.TypeSwitch(new FieldAutoSyncPacket(null, 0, 0, null));
    }

    [Fact]
    public void FieldSwitchTestingByValue()
    {
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var objectManager = new Mock<IObjectManager>();

        var typeSwitchCreator = new FieldSwitchCreator(moduleBuilder, typeof(SwitchTestClass));

        var fields = AccessTools.GetDeclaredFields(typeof(SwitchTestClass));
        var nameField = AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.Name));

        var fieldSwitchType = typeSwitchCreator.Build(fields.ToArray());
        dynamic fieldSwitch = Activator.CreateInstance(fieldSwitchType, objectManager.Object)!;

        var objId = "MyObj1";

        var obj = new SwitchTestClass();

        objectManager
            .Setup(x => x.TryGetObject<SwitchTestClass>(objId, out obj))
            .Returns(true);

        objectManager
            .Setup(x => x.TryGetId(obj, out objId))
            .Returns(true);

        Assert.Equal("hi", obj.Name);

        using (MemoryStream internalStream = new MemoryStream())
        {
            var newValue = "newValue";
            Serializer.Serialize(internalStream, newValue);
            var serializedStr = internalStream.ToArray();

            var packet = new FieldAutoSyncPacket(objId, 0, fields.IndexOf(nameField), serializedStr);

            fieldSwitch.FieldSwitch(packet);

            Assert.Equal(newValue, obj.Name);
        }
    }

    [Fact]
    public void FieldSwitchTestingByRef()
    {
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var objectManager = new Mock<IObjectManager>();
        var typeSwitchCreator = new FieldSwitchCreator(moduleBuilder, typeof(SwitchTestClass));

        var fields = AccessTools.GetDeclaredFields(typeof(SwitchTestClass));
        var refField = AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.RefClass));

        var fieldSwitchType = typeSwitchCreator.Build(fields.ToArray());
        dynamic fieldSwitch = Activator.CreateInstance(fieldSwitchType, objectManager.Object)!;

        var objId = "MyObj1";
        var obj = new SwitchTestClass();
        objectManager
            .Setup(x => x.TryGetObject<SwitchTestClass>(objId, out obj))
            .Returns(true);

        objectManager
            .Setup(x => x.TryGetId(obj, out objId))
            .Returns(true);

        var refObjId = "RefObjId1";
        var refObj = new SomeRefClass();
        objectManager
            .Setup(x => x.TryGetObject<SomeRefClass>(refObjId, out refObj))
            .Returns(true);

        objectManager
            .Setup(x => x.TryGetId(refObj, out refObjId))
            .Returns(true);

        Assert.Equal("hi", obj.Name);

        using (MemoryStream internalStream = new MemoryStream())
        {
            var newValue = refObjId;
            Serializer.Serialize(internalStream, newValue);
            var serializedStr = internalStream.ToArray();

            var packet = new FieldAutoSyncPacket(objId, 0, fields.IndexOf(refField), serializedStr);

            fieldSwitch.FieldSwitch(packet);

            Assert.Equal(refObj, obj.RefClass);
        }
    }

    public class SwitchTestClass
    {
        public string Name = "hi";
        public int MyInt = 1;
        public SomeRefClass? RefClass = null;
        public int MyProp { get; set; }

        public void SetMyInt(int val) { MyInt = val; }
    }

    public class SomeRefClass { }
}
