using Common.Logging;
using Common.Serialization;
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
    public void FieldSwitchTestingByValue()
    {
        Assert.NotNull(typeof(ProtoBufSerializer).GetMethods().Where(m => m.Name == nameof(ProtoBufSerializer.Deserialize) && m.IsGenericMethod));

        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TestAutoSyncAsm"), AssemblyBuilderAccess.RunAndCollect);
        var moduleBuilder = dynamicAssembly.DefineDynamicModule("TestAutoSyncAsm");

        var objectManager = new TestObjManager();

        var typeSwitchCreator = new FieldSwitchCreator(moduleBuilder, typeof(SwitchTestClass), objectManager);

        var fields = AccessTools.GetDeclaredFields(typeof(SwitchTestClass));
        var nameField = AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.Name));

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

            var packet = new AutoSyncFieldPacket(objId, 0, fields.IndexOf(nameField), serializedStr);

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

        var objectManager = new TestObjManager();
        var typeSwitchCreator = new FieldSwitchCreator(moduleBuilder, typeof(SwitchTestClass), objectManager);

        var fields = AccessTools.GetDeclaredFields(typeof(SwitchTestClass));
        var refField = AccessTools.Field(typeof(SwitchTestClass), nameof(SwitchTestClass.RefClass));
        
        var fieldSwitch = typeSwitchCreator.Build(fields.ToArray(), objectManager);

        var objId = "MyObj1";
        var obj = new SwitchTestClass();
        objectManager.AddExisting(objId, obj);

        var refObjId = "RefObjId1";
        var refObj = new SomeRefClass();
        objectManager.AddExisting(refObjId, refObj);


        Assert.Equal("hi", obj.Name);

        using (MemoryStream internalStream = new MemoryStream())
        {
            var newValue = refObjId;
            Serializer.Serialize(internalStream, newValue);
            var serializedStr = internalStream.ToArray();

            var packet = new AutoSyncFieldPacket(objId, 0, fields.IndexOf(refField), serializedStr);

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
    }

    public class SomeRefClass { }

    public class TestObjManager : IObjectManager
    {
        private Dictionary<string, object> idMap = new Dictionary<string, object>();

        public bool AddExisting(string id, object obj)
        {
            return idMap.TryAdd(id, obj);
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


        private HashSet<Type> managedTypes = new HashSet<Type>
        {
            typeof(SomeRefClass),
            typeof(SwitchTestClass) 
        };
        public bool IsTypeManaged(Type type)
        {
            return managedTypes.Contains(type);
        }

        public bool Remove(object obj)
        {
            throw new NotImplementedException();
        }

        public bool TryGetId(object obj, out string id)
        {
            id = null;

            foreach (var kvp in idMap)
            {
                if (kvp.Value == obj)
                {
                    id = kvp.Key;
                    return true;
                }
                    
            }

            return false;
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
