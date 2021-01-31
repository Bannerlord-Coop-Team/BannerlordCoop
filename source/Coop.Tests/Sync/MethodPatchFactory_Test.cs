using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodPatchFactory_Test
    {
        public MethodPatchFactory_Test()
        {
            m_DispatcherCalls = new List<MethodAccess>();
        }

        private class Foo
        {
            public enum EMethod
            {
                SingleArg,
                ThreeArgs
            }

            public List<EMethod> CallHistory { get; } = new List<EMethod>();
            public List<object[]> ArgsHistory { get; } = new List<object[]>();

            public void SingleArg(int i)
            {
                CallHistory.Add(EMethod.SingleArg);
                ArgsHistory.Add(new object[] {i});
            }

            public void ThreeArgs(int i, float f, double d)
            {
                CallHistory.Add(EMethod.ThreeArgs);
                ArgsHistory.Add(new object[] {i, f, d});
            }
        }

        private readonly Foo m_Foo = new Foo();

        [ThreadStatic] private static List<MethodAccess> m_DispatcherCalls;

        private static bool DispatcherTrue(
            MethodAccess methodAccess,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(methodAccess);
            return true;
        }

        private static bool DispatcherFalse(
            MethodAccess methodAccess,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(methodAccess);
            return false;
        }

        private static void DispatcherVoid(
            MethodAccess methodAccess,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(methodAccess);
        }
        
        private static bool DispatcherCallOriginal(
            MethodAccess methodAccess,
            object instance,
            params object[] args)
        {
            m_DispatcherCalls.Add(methodAccess);
            methodAccess.Call(EOriginator.RemoteAuthority, instance, args);
            return false;
        }

        [Fact]
        private void ArgumentsAreForwarded()
        {
            // Generate prefix for ThreeArgs
            MethodInfo method = AccessTools.Method(typeof(Foo), nameof(Foo.ThreeArgs));
            MethodInfo dispatcher = AccessTools.Method(
                typeof(MethodPatchFactory_Test),
                nameof(DispatcherCallOriginal));
            MethodAccess access = new MethodAccess(method);
            DynamicMethod prefix = MethodPatchFactory<MethodPatchFactory_Test>.GeneratePatch(
                "Prefix",
                access,
                dispatcher);

            // Call
            int iArg = 42;
            float fArg = 43.0f;
            double dArg = 44.0;
            object ret = prefix.Invoke(m_Foo, new object[] {m_Foo, iArg, fArg, dArg});
            Assert.IsType<bool>(ret);
            Assert.False((bool) ret); // DispatcherCallOriginal returns false
            Assert.Single(m_DispatcherCalls);
            Assert.NotNull(m_DispatcherCalls[0]);
            Assert.Same(access, m_DispatcherCalls[0]);

            // Verify original was called
            Assert.Single(m_Foo.CallHistory);
            Assert.Equal(Foo.EMethod.ThreeArgs, m_Foo.CallHistory[0]);
            Assert.Single(m_Foo.ArgsHistory);
            Assert.Equal(3, m_Foo.ArgsHistory[0].Length);

            // Verify individual args where forwarded
            Assert.NotNull(m_Foo.ArgsHistory[0][0]);
            Assert.IsType<int>(m_Foo.ArgsHistory[0][0]);
            Assert.Equal(iArg, (int) m_Foo.ArgsHistory[0][0]);
            Assert.NotNull(m_Foo.ArgsHistory[0][1]);
            Assert.IsType<float>(m_Foo.ArgsHistory[0][1]);
            Assert.Equal(fArg, (float) m_Foo.ArgsHistory[0][1]);
            Assert.NotNull(m_Foo.ArgsHistory[0][2]);
            Assert.IsType<double>(m_Foo.ArgsHistory[0][2]);
            Assert.Equal(dArg, (double) m_Foo.ArgsHistory[0][2]);
            
            MethodPatchFactory<MethodPatchFactory_Test>.UnpatchAll();
        }

        [Fact]
        private void GeneratePrefixWithMultipleArgsWorks()
        {
            // Generate prefix for SingleArg
            MethodInfo method = AccessTools.Method(typeof(Foo), nameof(Foo.ThreeArgs));
            MethodInfo dispatcher = AccessTools.Method(
                typeof(MethodPatchFactory_Test),
                nameof(DispatcherTrue));
            MethodAccess access = new MethodAccess(method);
            DynamicMethod prefix = MethodPatchFactory<MethodPatchFactory_Test>.GeneratePatch(
                "Prefix",
                access,
                dispatcher);

            // Call
            object ret = prefix.Invoke(m_Foo, new object[] {m_Foo, 42, 43.0f, 44.0});
            Assert.IsType<bool>(ret);
            Assert.True((bool) ret); // AlwaysCallOriginal
            
            MethodPatchFactory<MethodPatchFactory_Test>.UnpatchAll();
        }
    }
}
