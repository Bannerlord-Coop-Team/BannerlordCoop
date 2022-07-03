using HarmonyLib;
using Sync;
using Sync.Behaviour;
using Sync.Call;
using Sync.Patch;
using Sync.Value;
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
        public int MyVar2 = 2;
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

    [Collection("FieldPatcher")]
    public class FieldPatcherAllow_Test
    {
        public EFieldChangeAction ChangeGate()
        {
            return EFieldChangeAction.Allow;
        }

        [Fact]
        private void AllowSingleAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignFieldWithParam));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignFieldWithParam(100);

            Assert.Equal(100, testClass.MyVar);
        }

        [Fact]
        private void AllowSingleStaticAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignStaticFieldWithParam));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyStaticVar));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignStaticFieldWithParam(100);

            Assert.Equal(100, TestClass.MyStaticVar);
        }

        [Fact]
        private void AllowMultiAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsWithParam));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
            FieldInfo field2 = typeof(TestClass).GetField(nameof(TestClass.MyVar2));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            FieldPatch patch2 = new FieldPatch(field2, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.GeneratePatch(patch2, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            int value = 100;
            testClass.AssignMultiFieldsWithParam(value);

            Assert.Equal(value, testClass.MyVar);
            Assert.Equal(testClass.MyVar * value + 1000, testClass.MyVar2);
        }

        [Fact]
        private void AllowLargeAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsWithParamLarge));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignMultiFieldsWithParamLarge(100);

            Assert.Equal(testClass.MyVar2, testClass.MyVar);
        }

        // TODO manage byRef
        //[Fact]
        //private void ByRefAssignmentInterception()
        //{
        //    MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignFieldByRef));
        //    FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
        //    FieldInfo tmpField = typeof(FieldPatcher_Test).GetField(nameof(Temp), BindingFlags.NonPublic | BindingFlags.Static);

        //    FieldPatcher.hookMethod = intercept;
        //    FieldPatcher.tempField = tmpField;
        //    FieldPatcher.GeneratePatch(field, caller, intercept);
        //    FieldPatcher.ReplaceMethod(caller);

        //    TestClass testClass = new TestClass();

        //    testClass.AssignFieldByRef(100);

        //    Assert.Equal(1, Calls);
        //    Assert.Equal(0, testClass.MyVar);
        //}

        //[Fact]
        //private void ByRefAssignmentInterceptionLarge()
        //{
        //    MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsByRefLarge));
        //    MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
        //    FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
        //    FieldInfo tmpField = typeof(FieldPatcher_Test).GetField(nameof(Temp), BindingFlags.NonPublic | BindingFlags.Static);

        //    FieldPatcher.hookMethod = intercept;
        //    FieldPatcher.tempField = tmpField;
        //    FieldPatcher.GeneratePatch(field, caller, intercept);
        //    FieldPatcher.ReplaceMethod(caller);

        //    TestClass testClass = new TestClass();

        //    testClass.AssignMultiFieldsByRefLarge(100);

        //    Assert.Equal(3, Calls);
        //    Assert.Equal(0, testClass.MyVar);
        //}
    }

    [Collection("FieldPatcher")]
    public class FieldPatcherDeny_Test
    {
        public FieldPatcherDeny_Test()
        {
            FieldPatch.FieldPatches.Clear();
            FieldPatcher.UnpatchAll();
        }

        public EFieldChangeAction ChangeGate()
        {
            return EFieldChangeAction.Deny;
        }

        [Fact]
        private void SingleAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignFieldWithParam));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignFieldWithParam(100);

            Assert.Equal(0, testClass.MyVar);
        }

        [Fact]
        private void SingleStaticAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignStaticFieldWithParam));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyStaticVar));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignStaticFieldWithParam(100);

            Assert.Equal(0, TestClass.MyStaticVar);
        }

        [Fact]
        private void MultiAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsWithParam));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
            FieldInfo field2 = typeof(TestClass).GetField(nameof(TestClass.MyVar2));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            FieldPatch patch2 = new FieldPatch(field2, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.GeneratePatch(patch2, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignMultiFieldsWithParam(100);

            Assert.Equal(0, testClass.MyVar);
            Assert.Equal(2, testClass.MyVar2);
        }

        [Fact]
        private void LargeAssignmentInterception()
        {
            MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsWithParamLarge));
            FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));

            FieldPatch patch = new FieldPatch(field, ChangeGate);
            PatchedInvokable invokable = new PatchedInvokable(caller, typeof(TestClass));

            FieldPatcher.GeneratePatch(patch, caller);
            FieldPatcher.AddTranspiler(invokable);

            TestClass testClass = new TestClass();

            testClass.AssignMultiFieldsWithParamLarge(100);

            Assert.Equal(0, testClass.MyVar);
        }

        // TODO manage byRef
        //[Fact]
        //private void ByRefAssignmentInterception()
        //{
        //    MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignFieldByRef));
        //    FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
        //    FieldInfo tmpField = typeof(FieldPatcher_Test).GetField(nameof(Temp), BindingFlags.NonPublic | BindingFlags.Static);

        //    FieldPatcher.hookMethod = intercept;
        //    FieldPatcher.tempField = tmpField;
        //    FieldPatcher.GeneratePatch(field, caller, intercept);
        //    FieldPatcher.ReplaceMethod(caller);

        //    TestClass testClass = new TestClass();

        //    testClass.AssignFieldByRef(100);

        //    Assert.Equal(1, Calls);
        //    Assert.Equal(0, testClass.MyVar);
        //}

        //[Fact]
        //private void ByRefAssignmentInterceptionLarge()
        //{
        //    MethodInfo caller = typeof(TestClass).GetMethod(nameof(TestClass.AssignMultiFieldsByRefLarge));
        //    MethodInfo intercept = typeof(FieldPatcher_Test).GetMethod(nameof(FieldPatcher_Test.InterceptMethod));
        //    FieldInfo field = typeof(TestClass).GetField(nameof(TestClass.MyVar));
        //    FieldInfo tmpField = typeof(FieldPatcher_Test).GetField(nameof(Temp), BindingFlags.NonPublic | BindingFlags.Static);

        //    FieldPatcher.hookMethod = intercept;
        //    FieldPatcher.tempField = tmpField;
        //    FieldPatcher.GeneratePatch(field, caller, intercept);
        //    FieldPatcher.ReplaceMethod(caller);

        //    TestClass testClass = new TestClass();

        //    testClass.AssignMultiFieldsByRefLarge(100);

        //    Assert.Equal(3, Calls);
        //    Assert.Equal(0, testClass.MyVar);
        //}
    }
}
