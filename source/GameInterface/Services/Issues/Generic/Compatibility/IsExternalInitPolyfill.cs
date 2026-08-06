// Polyfill required for C# 9 positional `record` types (and `init`-only properties) to compile against
// netstandard2.0 (GameInterface.csproj's own TargetFramework): the compiler emits a reference to
// System.Runtime.CompilerServices.IsExternalInit for every init accessor, but netstandard2.0's own BCL doesn't
// define that type (it shipped in .NET 5's runtime). This is the standard, widely-used workaround - a private,
// compiler-recognized marker type matched by full name, not by any special attribute. Safe/inert at runtime -
// this type is never instantiated, only referenced by the compiler's own generated IL metadata.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
