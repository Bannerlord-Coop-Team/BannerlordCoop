using Common.Serialization;
using Common.Util;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        var typeSwitchCreator = new TypeSwitchCreator(moduleBuilder);

        var types = new Type[] { typeof(int), typeof(float), typeof(string) };

        var typeSwitch = typeSwitchCreator.Build(types);

        typeSwitch.TypeSwitch(0);

        for (var i = 0; i < types.Length; i++)
        {
            Assert.Equal(i, typeSwitch.TypeSwitch(i));
        }
    }

    [Fact]
    public void FieldSwitchTesting()
    {
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var typeSwitchCreator = new FieldSwitchCreator(moduleBuilder, typeof(SwitchTestClass));

        var fields = AccessTools.GetDeclaredFields(typeof(SwitchTestClass));

        var objectManager = new TestObjManager();
        var fieldSwitch = typeSwitchCreator.Build(fields.ToArray(), objectManager);

        var objId = "MyObj1";

        var obj = new SwitchTestClass();

        objectManager.AddExisting(objId, obj);

        Assert.Equal("hi", obj.Name);

        using (MemoryStream internalStream = new MemoryStream())
        {
            var newValue = "newValue";
            Serializer.Serialize(internalStream, newValue);
            var serializedStr = internalStream.ToArray();

            var packet = new AutoSyncFieldPacket(objId, 0, 0, serializedStr);

            fieldSwitch.FieldSwitch(packet);

            Assert.Equal(newValue, obj.Name);
        }
    }

    public class SwitchTestClass
    {
        public string Name = "hi";
        public int MyInt = 1;
    }

    private class TestObjManager : IObjectManager
    {
        private Dictionary<string, SwitchTestClass> idMap = new Dictionary<string, SwitchTestClass>();

        public bool AddExisting(string id, object obj)
        {
            return idMap.TryAdd(id, (SwitchTestClass)obj);
        }

        public bool AddNewObject(object obj, out string newId)
        {
            throw new NotImplementedException();
        }

        public bool Contains(object obj)
        {
            throw new NotImplementedException();
        }

        public bool Contains(string id)
        {
            throw new NotImplementedException();
        }

        public bool IsTypeManaged(Type type)
        {
            throw new NotImplementedException();
        }

        public bool Remove(object obj)
        {
            throw new NotImplementedException();
        }

        public bool TryGetId(object obj, out string id)
        {
            throw new NotImplementedException();
        }

        public bool TryGetObject<T>(string id, out T obj) where T : class
        {
            obj = null;

            if (idMap.TryGetValue(id, out var value) == false) return false;

            obj = value as T;
            return true;
        }
    }
}
