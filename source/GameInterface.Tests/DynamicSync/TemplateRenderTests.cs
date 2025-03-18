using Scriban;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.DynamicSync;
public class TemplateRenderTests
{
    private readonly ITestOutputHelper output;

    public TemplateRenderTests(ITestOutputHelper output)
    {
        this.output = output;
    }
    [Fact]
    public void PrefixClassGenerationTest()
    {
        const string resourceName = "GameInterface.DynamicSync.Templates.Patches.PropertySetPrefixTemplate";
        string fileText = "";

        using (Stream stream = typeof(GameInterface).Assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException())
        using (StreamReader reader = new StreamReader(stream))
        {
            fileText = reader.ReadToEnd();
        }

        var template = Template.Parse(fileText);

        var result = template.Render(new
        {
            Libraries = new string[] { "SomeTest.Library" },
            PropertyDeclaringType = "TestType",
            PropertyName = "TestProperty",
            PropertyType = "int",
            Namespace = "Test",
            MessageName = "TestPropertyUpdated"
        }, member => member.Name);

        Assert.Contains("using SomeTest.Library;", result);
        Assert.Contains($"[HarmonyPatch(typeof(TestType))]", result);
        Assert.Contains($"TestPropertyPrefix", result);
        Assert.Contains($"int value", result);
        Assert.Contains($"namespace GameInterface.DynamicSync.Test", result);
        Assert.Contains($"new TestPropertyUpdated(__instance, value)", result);
    }
}
