using System.Runtime.InteropServices;

namespace MdStudio.Helpers;

/// <summary>Case-insensitive natural order, as the Windows file explorer sorts ("2" before "10").</summary>
public sealed partial class NaturalStringComparer : IComparer<string>
{
    private NaturalStringComparer()
    {
    }

    /// <summary>Gets the single instance.</summary>
    public static NaturalStringComparer Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        return y is null ? 1 : StrCmpLogicalW(x, y);
    }

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int StrCmpLogicalW(string psz1, string psz2);
}
