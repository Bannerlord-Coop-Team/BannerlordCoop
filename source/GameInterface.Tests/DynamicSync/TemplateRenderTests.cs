using GameInterface.AutoSync.Templates;
using Xunit;

namespace GameInterface.Tests.AutoSync;
public class TemplateRenderTests
{
    [Fact]
    public void InterfaceReferenceSet_UsesAvailableHandleApis()
    {
        var result = TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", new
        {
            Interface = true,
            MemberType = "ITestValue",
            MemberDeclaringType = "TestOwner",
            MemberDeclaringTypeName = "TestOwner",
            MemberName = "Value",
            ReadOnly = false,
            Debug = false,
        });

        Assert.Contains("TryGetHandle(data.Value, out valueId)", result);
        Assert.Contains("TryGetObjectWithLogging(data.ValueId, out value)", result);
        Assert.DoesNotContain("TryGetHandle(data.Value.GetType(), data.Value", result);
        Assert.DoesNotContain("TryGetObjectWithLogging(type, data.ValueId", result);
    }
}
