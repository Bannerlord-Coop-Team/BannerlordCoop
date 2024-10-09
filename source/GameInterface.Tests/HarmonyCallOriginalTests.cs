using Common.Messaging;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests
{
    public class HarmonyCallOriginalTests
    {
        [Fact]
        public void CallOriginal_Full()
        {
            // Arrange
            Harmony harmony = new Harmony(nameof(HarmonyCallOriginalTests));

            
            var prefix = AccessTools.Method(typeof(MyPatch), nameof(MyPatch.Prefix));
            var transpiler = AccessTools.Method(typeof(MyPatch), nameof(MyPatch.Transpiler));

            foreach (var property in typeof(MyClass).GetProperties())
            {
                var original = property.GetSetMethod();
                harmony.Patch(original, transpiler: new HarmonyMethod(transpiler));
            }

            // Act
            var myClass = new MyClass();

            MessageBroker.Instance.Subscribe<FieldChangeAttempted>((message) =>
            {
                var payload = message.What;
                ;
            });

            myClass.MyData = 10;
            myClass.MyData2 = 10;
            myClass.MyData3 = 10;

            // Assert

            Assert.Equal(1, myClass.MyData);

            MyPatch.CallOriginal(myClass, 20);
            Assert.Equal(20, myClass.MyData);
        }
    }


    public class MyClass
    {
        public int MyData { get; set; } = 1;
        //public int MyData
        //{
        //    get { return _myData; }
        //    set { Signals.PublishSignal(this, value, ref _myData, _myData); }
        //}
        private int _myData = 1;
        public int MyData2 { get; set; } = 1;
        public int MyData3 { get; set; } = 1;
    }

    public class MyPatch
    {
        public bool Prefix(ref MyClass __instance)
        {
            return false;
        }

        private static readonly MethodInfo signal = typeof(Signals).GetMethod(nameof(Signals.PublishSignal));
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstrs = new List<CodeInstruction>();

            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Stfld &&
                    instr.operand is FieldInfo fieldInfo)
                {
                    var boxType = fieldInfo.FieldType;
                    var name = fieldInfo.DeclaringType!.Name + '.' + fieldInfo.Name;
                    Signals.AddSignal(name, fieldInfo);

                    var genericSignal = signal.MakeGenericMethod(fieldInfo.FieldType);

                    //newInstrs.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    //newInstrs.Add(new CodeInstruction(OpCodes.Ldflda, fieldInfo));
                    //newInstrs.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    //newInstrs.Add(new CodeInstruction(OpCodes.Ldfld, fieldInfo));
                    //newInstrs.Add(new CodeInstruction(OpCodes.Call, genericSignal));

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldflda, fieldInfo);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, fieldInfo);
                    yield return new CodeInstruction(OpCodes.Ldstr, name);
                    yield return new CodeInstruction(OpCodes.Call, genericSignal);
                    continue;
                }

                //newInstrs.Add(instr);
                yield return instr;
            }

            //return newInstrs;
        }
        
        public static void CallOriginal(MyClass instance, int val)
        {
            var field = typeof(MyClass).GetField("<MyData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

            field.SetValue(instance, val);
        }
    }

    public class Signals
    {
        private static Dictionary<string, FieldInfo> FieldMap = new Dictionary<string, FieldInfo>();

        public static void PublishSignal<T>(object instance, T newValue, ref T variable, T currentValue, string fieldName)
        {
            if (EqualityComparer<T>.Default.Equals(currentValue, newValue) == false)
            {
                if (FieldMap.TryGetValue(fieldName, out FieldInfo fieldInfo) == false)
                {
                    throw new InvalidOperationException($"Unable to find {fieldName}");
                }

                MessageBroker.Instance.Publish(instance, new FieldChangeAttempted(instance, fieldInfo, newValue));
            }
        }

        public static void AddSignal(string name, FieldInfo field)
        {
            if (!FieldMap.ContainsKey(name))
            {
                FieldMap.Add(name, field);
            }
        }
    }

    public class FieldChangeAttempted : IEvent
    {
        public object Instance { get; }
        public FieldInfo FieldInfo { get; }
        public object NewValue { get; }

        public FieldChangeAttempted(object instance, FieldInfo fieldInfo, object newVal)
        {
            Instance = instance;
            FieldInfo = fieldInfo;
            NewValue = newVal;
        }
    }
}
