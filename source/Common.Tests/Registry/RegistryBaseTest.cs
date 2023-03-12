namespace Common.Tests.Registry
{
    public class RegistryBaseTest
    {
        [Fact]
        public void Register()
        {
            var registry = new TestClassRegistry();

            Assert.True(registry.Register(new TestClass()));
            Assert.Equal(1, registry.Count);
        }

        [Fact]
        public void Register_ExistingObj()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.Register(testClass));
            Assert.False(registry.Register(testClass));
        }

        [Fact]
        public void Remove_ByObj()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.Register(testClass));
            Assert.Equal(1, registry.Count);

            Assert.True(registry.Remove(testClass));

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void Remove_ById()
        {
            var testClass = new TestClass();

            var registry = new TestClassRegistry();

            Assert.True(registry.Register(testClass));
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
