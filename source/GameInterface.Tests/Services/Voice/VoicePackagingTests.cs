using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public class VoicePackagingTests
{
    [Theory]
    [InlineData("Concentus")]
    [InlineData("NAudio.Core")]
    [InlineData("NAudio.WinMM")]
    public void BuiltClientDependencyIsPresentAndManaged(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
        Assert.True(File.Exists(path), path);
        Assert.Equal(name, System.Reflection.AssemblyName.GetAssemblyName(path).Name);
    }

    [Fact]
    public void ReleaseInputsPinManagedDependenciesAndIncludeRedistributionNoticesAndVoicePrefab()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Deploy.targets"))) root = root.Parent;
        Assert.NotNull(root);
        string path = root!.FullName;
        var project = XDocument.Load(Path.Combine(path, "source/GameInterface/GameInterface.csproj"));
        Assert.Contains(project.Descendants("PackageReference"), p => (string?)p.Attribute("Include") == "Concentus" && (string?)p.Attribute("Version") == "1.1.7");
        Assert.Contains(project.Descendants("PackageReference"), p => (string?)p.Attribute("Include") == "NAudio.WinMM" && (string?)p.Attribute("Version") == "2.2.1");
        foreach (string notice in new[] { "Concentus-LICENSE.txt", "NAudio-LICENSE.txt", "Microsoft-NET-LICENSE.txt", "Microsoft-NET-THIRD-PARTY-NOTICES.txt" })
            Assert.NotEmpty(File.ReadAllText(Path.Combine(path, "deploy/ThirdPartyNotices", notice)));
        var deploy = XDocument.Load(Path.Combine(path, "Deploy.targets"));
        Assert.Contains(deploy.Descendants(), node => node.Name.LocalName == "DeployStaticFiles" &&
            (string?)node.Attribute("Include") == "$(DeploySourceDir)\\**\\*");
        var movie = XDocument.Load(Path.Combine(path, "UIMovies/CoopOptionsUIMovie.xml"));
        Assert.Contains(movie.Descendants(), node => (string?)node.Attribute("DataSource") == "{VoiceTab}");
        Assert.Contains(movie.Descendants(), node => (string?)node.Attribute("Command.Click") == "ExecuteTest");
    }
}
