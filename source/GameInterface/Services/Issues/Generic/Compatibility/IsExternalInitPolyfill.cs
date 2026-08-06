// Polyfill so C# 9 `record`/`init` compiles against netstandard2.0, which has no IsExternalInit type.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
