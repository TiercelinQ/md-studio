using System.Diagnostics.CodeAnalysis;

namespace MdReader.Models;

/// <summary>Severity of a message shown to the user.</summary>
public enum FeedbackType
{
    /// <summary>The operation succeeded.</summary>
    Success,

    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>A non-blocking problem.</summary>
    Warning,

    /// <summary>A failure the user must notice.</summary>
    Danger,
}

/// <summary>Expected failure returned by a service, ready to be shown to the user.</summary>
/// <param name="Type">Severity of the failure.</param>
/// <param name="Message">Main message, already localized.</param>
/// <param name="Description">Optional detail, already localized.</param>
public sealed record ResultError(FeedbackType Type, string Message, string? Description = null);

/// <summary>Empty value for operations that return nothing.</summary>
public sealed record Unit
{
    /// <summary>The single instance.</summary>
    public static Unit Value { get; } = new();
}

/// <summary>Outcome of a service operation: either data or an expected failure.</summary>
/// <typeparam name="T">Type of the returned data.</typeparam>
public readonly record struct Result<T>
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Ok { get; private init; }

    /// <summary>Gets the returned data when <see cref="Ok"/> is true.</summary>
    public T? Data { get; private init; }

    /// <summary>Gets the failure when <see cref="Ok"/> is false.</summary>
    public ResultError? Error { get; private init; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="data">The returned data.</param>
    /// <returns>A successful result.</returns>
    [SuppressMessage("Design", "CA1000", Justification = "Result<T>.Success / Failure is the single result contract imposed by rules/errors.md.")]
    public static Result<T> Success(T data) => new() { Ok = true, Data = data };

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A failed result.</returns>
    [SuppressMessage("Design", "CA1000", Justification = "Result<T>.Success / Failure is the single result contract imposed by rules/errors.md.")]
    public static Result<T> Failure(ResultError error) => new() { Ok = false, Error = error };
}
