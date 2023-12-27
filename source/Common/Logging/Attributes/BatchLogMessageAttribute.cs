using System;

namespace Common.Logging.Attributes;

/// <summary>
/// Logs messages in batches using <see cref="BatchLogger"/>
/// </summary>
/// <remarks>
/// Normally used for messages that are triggered very often to keep the log more readable.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class BatchLogMessageAttribute : Attribute
{
}
