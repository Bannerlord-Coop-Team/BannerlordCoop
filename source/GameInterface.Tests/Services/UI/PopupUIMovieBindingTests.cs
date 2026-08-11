using GameInterface.Services.UI.Credits;
using GameInterface.Services.UI.Donate;
using GameInterface.Services.UI.JoinCancel;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>
/// Cross-checks the popup movies against their view models so a renamed VM member cannot leave
/// a dead "@Property" or "Command.Click" binding behind in the XML. Only valid for popups whose
/// widgets all bind against the root data source (no DataSource scopes).
/// </summary>
public class PopupUIMovieBindingTests
{
    public static TheoryData<string, Type> PopupMovies => new()
    {
        { "DonatePopupUIMovie.xml", typeof(DonatePopupVM) },
        { "CreditsPopupUIMovie.xml", typeof(CreditsPopupVM) },
        { "CoopJoinCancelOverlay.xml", typeof(JoinCancelVM) },
    };

    [Theory]
    [MemberData(nameof(PopupMovies))]
    public void PopupMovies_BindOnlyAgainstTheRootDataSource(string movieFile, Type viewModelType)
    {
        _ = viewModelType;
        var document = XDocument.Load(FindMoviePath(movieFile));

        Assert.DoesNotContain(document.Descendants().SelectMany(element => element.Attributes()),
            attribute => attribute.Name.LocalName == "DataSource");
    }

    [Theory]
    [MemberData(nameof(PopupMovies))]
    public void PropertyBindings_ResolveToViewModelProperties(string movieFile, Type viewModelType)
    {
        var document = XDocument.Load(FindMoviePath(movieFile));

        var propertyNames = document.Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Value.StartsWith("@", StringComparison.Ordinal))
            .Select(attribute => attribute.Value.Substring(1))
            .Distinct()
            .ToList();

        Assert.NotEmpty(propertyNames);
        foreach (var propertyName in propertyNames)
        {
            var property = viewModelType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(property?.GetMethod != null,
                $"{movieFile} binds '@{propertyName}' but {viewModelType.Name} has no readable public property '{propertyName}'.");
        }
    }

    [Theory]
    [MemberData(nameof(PopupMovies))]
    public void CommandBindings_ResolveToViewModelMethods(string movieFile, Type viewModelType)
    {
        var document = XDocument.Load(FindMoviePath(movieFile));

        var methodNames = document.Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName.StartsWith("Command.", StringComparison.Ordinal))
            .Select(attribute => attribute.Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(methodNames);
        foreach (var methodName in methodNames)
        {
            var method = viewModelType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            Assert.True(method != null,
                $"{movieFile} invokes '{methodName}' but {viewModelType.Name} has no parameterless public method '{methodName}'.");
        }
    }

    internal static string FindMoviePath(string movieFile, [CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", movieFile));
    }
}
