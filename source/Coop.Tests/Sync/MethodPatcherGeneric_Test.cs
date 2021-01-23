using System;
using System.Linq;
using Sync;
using Sync.Behaviour;
using Xunit;

namespace Coop.Tests.Sync
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MethodPatcherGeneric_Test
    {
        private class Foo
        {
            public string LatestArgument;
            public int NumberOfCalls;

            public T SyncedMethod<T>(string sSomeArgument)
                where T : new()
            {
                ++NumberOfCalls;
                LatestArgument = sSomeArgument;
                return new T();
            }

            public T SyncedMethod<T>()
                where T : new()
            {
                ++NumberOfCalls;
                return new T();
            }
        }

        [Fact]
        private void CanPatchBeCreated()
        {
            // Create patch
            Type[] generics = {typeof(DateTime)};
            MethodPatch patch = new MethodPatch(typeof(Foo));
            patch.InterceptGeneric(nameof(Foo.SyncedMethod), generics);

            // Verify generated patch
            Assert.True(
                patch.TryGetMethod(patch.Methods.First().MethodBase, out MethodAccess method));
            Assert.True(method.MethodBase.IsGenericMethod);
            Assert.Equal(generics, method.MethodBase.GetGenericArguments());
            Assert.True(MethodRegistry.MethodToId.ContainsKey(method));

            // Init object
            Foo instance = new Foo();
            Assert.Equal(0, instance.NumberOfCalls);
            int iNumberOfHandlerCalls = 0;

            // Verify the method access object
            string sMessage = "Hello World";
            int iNumberOfCalls = 0;
            method.Call(ETriggerOrigin.Authoritative, instance, new object[] {sMessage});
            ++iNumberOfCalls;
            Assert.Equal(iNumberOfCalls, instance.NumberOfCalls);
            Assert.Equal(sMessage, instance.LatestArgument);

            // Register handler
            method.SetHandler(instance, args =>
            {
                ++iNumberOfHandlerCalls;
                return ECallPropagation.Suppress;
            });

            // Trigger the handler
            instance.SyncedMethod<DateTime>(sMessage);
            Assert.Equal(
                iNumberOfCalls,
                instance.NumberOfCalls); // Call should've been intercepted!
            Assert.Equal(1, iNumberOfHandlerCalls);
        }
    }
}
