using Common.Util;

namespace Common.Tests.Utils
{
    public class AllowedInstanceTests
    {
        [Fact]
        public void AllowedInstance_DoesNotAllowChange()
        {
            var testObj1 = new TestObject(5);

            // Does not set without allow lock
            TestObject.UpdateSomeInt(testObj1, 10);
            Assert.Equal(5, testObj1.SomeInt);
        }

        [Fact]
        public void AllowedInstance_AllowChange()
        {
            var testObj1 = new TestObject(5);

            // Sets with allow lock
            TestObject.OverrideInt(testObj1, 10);
            Assert.Equal(10, testObj1.SomeInt);
        }

        [Fact]
        public void AllowedInstance_LockClears()
        {
            var testObj1 = new TestObject(5);

            // Sets with allow lock
            TestObject.OverrideInt(testObj1, 10);

            // Lock clears
            TestObject.UpdateSomeInt(testObj1, 15);
            Assert.Equal(10, testObj1.SomeInt);
        }

        [Fact]
        public void AllowedInstance_DifferentInstance_DoesNotChange()
        {
            var testObj1 = new TestObject(5);
            var testObj2 = new TestObject(6);

            // Sets with allow lock
            TestObject.OverrideInt(testObj1, 10);
            Assert.Equal(10, testObj1.SomeInt);

            TestObject.UpdateSomeInt(testObj2, 15);
            Assert.Equal(6, testObj2.SomeInt);
        }
    }

    public class TestObject
    {
        static AllowedInstance<TestObject> allowedInstance = new AllowedInstance<TestObject>();

        public int SomeInt { get; private set; }

        public TestObject(int startIntValue)
        {
            SomeInt = startIntValue;
        }

        public static void UpdateSomeInt(TestObject obj, int value)
        {
            if (obj == allowedInstance?.Instance)
            {
                obj.SomeInt = value;
            }
        }

        public static void OverrideInt(TestObject obj, int value)
        {
            using (allowedInstance)
            {
                allowedInstance.Instance = obj;
                UpdateSomeInt(obj, value);
            }
        }
    }
}
