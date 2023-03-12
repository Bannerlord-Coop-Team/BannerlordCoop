using System;

namespace Common.Tests.Registry
{
    public class RegistryBaseTest
    {
        [Fact]
        public void Register()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.RegisterNewObject(testClass));
            Assert.Equal(1, registry.Count);

            // Verify associated object stored is the same as testClass
            Assert.True(registry.TryGetValue(testClass, out var resolvedId));
            Assert.True(registry.TryGetValue(resolvedId, out var resolvedObj));

            Assert.Same(testClass, resolvedObj);
        }

        [Fact]
        public void Register_ExistingObjAsNew()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.RegisterNewObject(testClass));
            Assert.False(registry.RegisterNewObject(testClass));

            Assert.Equal(1, registry.Count);
        }

        [Fact]
        public void Register_ExistingObj()
        {
            var testClass = new TestClass();
            Guid guid = Guid.NewGuid();

            var registry = new TestClassRegistry();

            Assert.True(registry.RegisterExistingObject(guid, testClass));
            Assert.False(registry.RegisterExistingObject(guid, testClass));

            Assert.True(registry.TryGetValue(guid, out var resolvedObject));

            Assert.Same(testClass, resolvedObject);

            Assert.Equal(1, registry.Count);
        }

        [Fact]
        public void Remove_ByObj()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.RegisterNewObject(testClass));
            Assert.Equal(1, registry.Count);

            Assert.True(registry.Remove(testClass));

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void Remove_ById()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.RegisterNewObject(testClass));
            Assert.Equal(1, registry.Count);

            Assert.True(registry.TryGetValue(testClass, out var id));

            Assert.True(registry.Remove(id));

            Assert.Equal(0, registry.Count);
        }
    }

    internal class TestClassRegistry : RegistryBase<TestClass> { }

    internal class TestClass
    {

    }
}
