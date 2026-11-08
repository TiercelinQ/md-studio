using System.Diagnostics.CodeAnalysis;

namespace MdStudio.Models;

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

/// <summary>
/// Message shown to the user as a toast, and expected failure returned by a service. Both texts are
/// mandatory and already localized (toast specification TS-21).
/// </summary>
/// <param name="Type">Severity: it alone sets the toast duration, persistence and accessibility role.</param>
/// <param name="Title">Short title, no final period.</param>
/// <param name="Message">One or two sentences: the cause, then the possible action.</param>
public sealed record ResultError(FeedbackType Type, string Title, string Message);

/// <summary>Button of a toast (at most two per toast, TS-03).</summary>
/// <param name="Label">Infinitive verb, three words at most.</param>
/// <param name="IsPrimary">True for the main action, shown first in the accent color.</param>
/// <param name="Run">Work done on click; the toast closes afterwards (TS-43).</param>
public sealed record ToastAction(string Label, bool IsPrimary, Action Run);

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
