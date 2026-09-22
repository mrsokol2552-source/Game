#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// Enables C# 9.0 record and init-only properties in netstandard2.1 / Unity runtime.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
