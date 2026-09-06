using System;

namespace Common.Commands;

public sealed class CoopCommandResult
{
    public CoopCommandResult(bool succeeded, string output, string errorCode = null)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (succeeded && errorCode != null)
            throw new ArgumentException("A successful command result cannot have an error code.", nameof(errorCode));
        if (!succeeded && string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("A failed command result must have an error code.", nameof(errorCode));

        Succeeded = succeeded;
        Output = output;
        ErrorCode = errorCode;
    }

    public bool Succeeded { get; }

    public string Output { get; }

    public string ErrorCode { get; }
}
