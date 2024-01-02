using System;

namespace Common.Logging.Attributes;

/// <summary>
/// Disables logging for a message
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DontLogMessageAttribute : Attribute
{
}
