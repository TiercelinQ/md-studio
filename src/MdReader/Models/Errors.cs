namespace MdReader.Models;

/// <summary>Raised when a file cannot be read or written because access is denied or the file is locked.</summary>
public sealed class FileAccessDeniedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FileAccessDeniedException"/> class.</summary>
    public FileAccessDeniedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileAccessDeniedException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    public FileAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileAccessDeniedException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    /// <param name="innerException">Original exception.</param>
    public FileAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when a file is binary or not decodable as text.</summary>
public sealed class NotTextFileException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="NotTextFileException"/> class.</summary>
    public NotTextFileException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotTextFileException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    public NotTextFileException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotTextFileException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    /// <param name="innerException">Original exception.</param>
    public NotTextFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when a file cannot be written (read-only, disk full, locked).</summary>
public sealed class FileWriteException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteException"/> class.</summary>
    public FileWriteException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileWriteException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    public FileWriteException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FileWriteException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    /// <param name="innerException">Original exception.</param>
    public FileWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when the document page sends a message that does not follow the protocol.</summary>
public sealed class PageProtocolException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PageProtocolException"/> class.</summary>
    public PageProtocolException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PageProtocolException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    public PageProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PageProtocolException"/> class.</summary>
    /// <param name="message">Technical message.</param>
    /// <param name="innerException">Original exception.</param>
    public PageProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
