using HarmonyLib;
using HarmonyTranspilerTools;
using Sync.Patch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Sync
{
    class TestClass
    {
        public int MyVar = 0;
        public int MyVar2 = 0;
        public static int MyStaticVar = 0;
        public void AssignFieldWithParam(int x = 5)
        {
            //FieldPatcher_Test.InterceptMethod(x, ref MyVar);
            MyVar = x;
        }

        public void AssignStaticFieldWithParam(int x = 5)
        {
            //FieldPatcher_Test.InterceptMethod(x, ref MyStaticVar);
            MyStaticVar = x;
        }

        public void AssignMultiFieldsWithParam(int x = 5)
        {
            //FieldPatcher_Test.InterceptMethod(x, ref MyVar);
            MyVar = x;
            MyVar2 = MyVar * x + 1000;
        }

        public void AssignMultiFieldsWithParamLarge(int x = 5)
        {
            int y = 50;
            MyVar = x + y;
            AssignByRef(ref MyVar2, x);
            MyVar = 10;
            MyVar = Return100();
            MyVar = MyVar2;
        }

        public void AssignFieldByRef(int x = 5)
        {
            AssignByRef(ref MyVar, x);
        }

        public int Return100()
        {
            return 100;
        }
        public void AssignByReturn()
        {
            MyVar = Return100();
        }

        public void AssignMultiFieldsByRefLarge(int x = 5)
        {
            AssignByRef(ref MyVar2, x);
            AssignByRef(ref MyVar, x);
            AssignByRef(ref MyVar2, x);
            AssignByRef(ref MyVar, x);
            MyVar2 = Return100();
            AssignByRef(ref MyVar, x);
            AssignByRef(ref MyVar2, x);
        }

        private void AssignByRef(ref int x, int value)
        {
            x = value;
        }
    }

    public class FieldPatcher_Test
    {
        static int Calls = 0;
        static int Temp;
        static bool Allowed;

        public FieldPatcher_Test()
        {
            Calls = 0;
        }

        public static void InterceptMethod<T>(T variable, object instance, FieldInfo field)
        {
            if (Allowed)
            {
                field.SetValue(instance, variable);
            }
            Calls += 1;
        }

        [Fact]
        private void SingleAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignFieldWithParam));
            MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));

            FieldPatchFactory.hookMethod = intercept;
            FieldPatchFactory.GeneratePatch(field, caller, intercept);
            FieldPatchFactory.ReplaceMethod(caller);

            TestClass testClass = new TestClass();

            testClass.AssignFieldWithParam(100);

            Assert.Equal(1, Calls);
            Assert.Equal(0, testClass.MyVar);
        }

        [Fact]
        private void SingleStaticAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignStaticFieldWithParam));
            MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyStaticVar));

            FieldPatchFactory.hookMethod = intercept;
            FieldPatchFactory.GeneratePatch(field, caller, intercept);
            FieldPatchFactory.ReplaceMethod(caller);

            TestClass testClass = new TestClass();

            testClass.AssignStaticFieldWithParam(100);

            Assert.Equal(1, Calls);
            Assert.Equal(0, testClass.MyVar);
        }

        [Fact]
        private void MultiAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsWithParam));
            MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
            FieldInfo field2 = typeof(TestClass).GetField(nameof(TestClass.MyVar2));

            FieldPatchFactory.hookMethod = intercept;
            FieldPatchFactory.GeneratePatch(field, caller, intercept);
            FieldPatchFactory.GeneratePatch(field2, caller, intercept);
            FieldPatchFactory.ReplaceMethod(caller);

            TestClass testClass = new TestClass();

            testClass.AssignMultiFieldsWithParam(100);

            Assert.Equal(2, Calls);
            Assert.Equal(0, testClass.MyVar);
        }

        [Fact]
        private void LargeAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsWithParamLarge));
            MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));

            FieldPatchFactory.hookMethod = intercept;
            FieldPatchFactory.GeneratePatch(field, caller, intercept);
            FieldPatchFactory.ReplaceMethod(caller);

            TestClass testClass = new TestClass();

            testClass.AssignMultiFieldsWithParamLarge(100);

            Assert.Equal(4, Calls);
            Assert.Equal(0, testClass.MyVar);
        }

        [Fact]
        private void ByRefAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignFieldByRef));
            MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
            FieldInfo tmpField = typeof(FieldPatcher_Test).GetField(nameof(Temp), BindingFlags.NonPublic | BindingFlags.Static);

            FieldPatchFactory.hookMethod = intercept;
            FieldPatchFactory.tempField = tmpField;
            FieldPatchFactory.GeneratePatch(field, caller, intercept);
            FieldPatchFactory.ReplaceMethod(caller);

            TestClass testClass = new TestClass();

            testClass.AssignFieldByRef(100);

            Assert.Equal(1, Calls);
            Assert.Equal(0, testClass.MyVar);
        }

        [Fact]
        private void ByRefAssignmentInterceptionLarge()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsByRefLarge));
            MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
            FieldInfo tmpField = typeof(FieldPatcher_Test).GetField(nameof(Temp), BindingFlags.NonPublic | BindingFlags.Static);

            FieldPatchFactory.hookMethod = intercept;
            FieldPatchFactory.tempField = tmpField;
            FieldPatchFactory.GeneratePatch(field, caller, intercept);
            FieldPatchFactory.ReplaceMethod(caller);

            TestClass testClass = new TestClass();

            testClass.AssignMultiFieldsByRefLarge(100);

            Assert.Equal(3, Calls);
            Assert.Equal(0, testClass.MyVar);
        }
    }
}
