using Common.Extensions;
using System.Reflection;

namespace Common.Tests.Extensions;

public class FastInvokeTests
{
    public class MyTestClass
    {
        public int MyIntProperty { get; set; }
        public string? MyStringProperty { get; set; }

        private int SomePrivateMethod()
        {
            return 1;
        }
    }

    [Fact]
    public void BuildUntypedGetter_ReturnsCorrectGetterForIntProperty()
    {
        // Arrange
        var memberInfo = typeof(MyTestClass).GetProperty("MyIntProperty");

        // Act
        var getter = FastInvoke.BuildUntypedGetter<MyTestClass, int>(memberInfo);

        // Assert
        var obj = new MyTestClass { MyIntProperty = 42 };
        Assert.Equal(42, getter(obj));
    }

    [Fact]
    public void BuildUntypedGetter_ReturnsCorrectGetterForStringProperty()
    {
        // Arrange
        var memberInfo = typeof(MyTestClass).GetProperty("MyStringProperty");

        // Act
        var getter = FastInvoke.BuildUntypedGetter<MyTestClass, string>(memberInfo);

        // Assert
        var obj = new MyTestClass { MyStringProperty = "hello world" };
        Assert.Equal("hello world", getter(obj));
    }

    [Fact]
    public void BuildUntypedGetter_ThrowsArgumentNullExceptionForNullMemberInfo()
    {
        // Arrange
        MemberInfo memberInfo = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => FastInvoke.BuildUntypedGetter<MyTestClass, int>(memberInfo));
    }

    [Fact]
    public void BuildUntypedSetter_SetsIntProperty()
    {
        // Arrange
        var myObject = new MyTestClass();
        var memberInfo = typeof(MyTestClass).GetProperty("MyIntProperty");
        var setter = memberInfo.BuildUntypedSetter<MyTestClass, int>();

        // Act
        setter(myObject, 42);

        // Assert
        Assert.Equal(42, myObject.MyIntProperty);
    }

    [Fact]
    public void BuildUntypedSetter_SetsStringProperty()
    {
        // Arrange
        var myObject = new MyTestClass();
        var memberInfo = typeof(MyTestClass).GetProperty("MyStringProperty");
        var setter = memberInfo.BuildUntypedSetter<MyTestClass, string>();

        // Act
        setter(myObject, "hello world");

        // Assert
        Assert.Equal("hello world", myObject.MyStringProperty);
    }

    [Fact]
    public void BuildUntypedSetter_ThrowsArgumentNullExceptionForNullMemberInfo()
    {
        // Arrange
        MemberInfo memberInfo = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => memberInfo.BuildUntypedSetter<MyTestClass, int>());
    }

    [Fact]
    public void CreatePrivateCall()
    {
        var methodInfo = typeof(MyTestClass).GetMethod("SomePrivateMethod", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var @delegate = methodInfo.BuildDelegate<Func<MyTestClass, int>>();

        MyTestClass testClass = new MyTestClass();

        Assert.Equal(1, @delegate(testClass));
    }
}
