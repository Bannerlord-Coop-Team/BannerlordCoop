using GameInterface.Services.Template.Handlers;

namespace GameInterface.Services.MobileParties.Extensions;

/// <summary>
/// TODO fill me out
/// </summary>
internal static class TemplateExtensions
{
    // The "this" keyword makes this into an extension
    // Also this class is required to be static
    public static void SomeExtension(this TemplateHandler handler)
    {
        // Do something
    }

    public static void ExtensionUsageExample(TemplateHandler handler)
    {
        // This can also be used in different classes
        // An extension essentailly adds another function to a class
        handler.SomeExtension();
    }
}
