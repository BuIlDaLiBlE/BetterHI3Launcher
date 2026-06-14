/*
 * This code was a part of Hi3Helper.Plugin.Core library licensed under MIT License, developed by Collapse Launcher Team.
 * The original code can be found at:
 * https://github.com/CollapseLauncher/Hi3Helper.Plugin.Core/blob/main/Management/GameVersion.cs
 *
 * This version of the code has been modified to comply with .NET Framework 4.8 and usages for BetterHi3Launcher project.
 */

#if NET6_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
#endif
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable
// ReSharper disable UnusedMember.Global
namespace BetterHI3Launcher.Utility;

/// <summary>
/// Represents a version of API, Game Installation or Assets.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct StructVersion :
    IEquatable<StructVersion>
#if NET8_0_OR_GREATER
    , IUtf8SpanFormattable
    , IUtf8SpanParsable<StructVersion>
#endif
#if NET6_0_OR_GREATER
    , ISpanFormattable
    , ISpanParsable<StructVersion>
#endif
{
#if NET6_0_OR_GREATER
    private const string ExSeparators = ",.;|";
#else
    private static readonly char[] ExSeparators = ",.;|".ToCharArray();
#endif
    private static ReadOnlySpan<byte> ExSeparatorsUtf8 => ",.;|"u8;

    public static readonly StructVersion Empty;

#if NET9_0_OR_GREATER
    public StructVersion(params ReadOnlySpan<int> ver)
#else
    public StructVersion(params int[] ver) : this(ver.AsSpan()) { }
    public StructVersion(ReadOnlySpan<int> ver)
#endif
    {
        if (ver.Length == 0)
        {
            throw new ArgumentException("Version array entered should have length at least 1 or max. 4!");
        }

        Major = ver[0];
        Minor = ver.Length >= 2 ? ver[1] : 0;
        Build = ver.Length >= 3 ? ver[2] : 0;
        Revision = ver.Length >= 4 ? ver[3] : 0;
    }

    public StructVersion(Version version)
    {
        Major = version.Major;
        Minor = version.Minor;
        Build = version.Build;
        Revision = version.Revision;
    }

    public StructVersion(string? version) : this(version.AsSpan())
    {
    }

    public StructVersion(ReadOnlySpan<char> version)
    {
        if (!TryParse(version, null, out StructVersion versionOut))
        {
            string msg = $"Version should be either in \"x\", \"x.x\", \"x.x.x\" or \"x.x.x.x\" format or all the values aren't numbers! (current value: \"{version.ToString()}\")";
            throw new ArgumentException(msg);
        }

        Major    = versionOut.Major;
        Minor    = versionOut.Minor;
        Build    = versionOut.Build;
        Revision = versionOut.Revision;
    }

    public readonly StructVersion GetIncrementedVersion()
    {
        int nextMajor = Major;
        int nextMinor = Minor;

        nextMinor++;
        if (nextMinor < 10)
        {
            return new StructVersion(nextMajor, nextMinor, Build, Revision);
        }

        nextMinor = 0;
        nextMajor++;

        return new StructVersion(nextMajor, nextMinor, Build, Revision);
    }

    /// <summary>
    /// Create a <see cref="Span{T}"/> of <see cref="int"/> representation of this struct.
    /// </summary>
    /// <returns>A <see cref="Span{T}"/> of <see cref="int"/> with 4-int by length.</returns>
    public unsafe ReadOnlySpan<int> AsSpan() => new(Unsafe.AsPointer(ref this), 4);

    /// <summary>
    /// Convert this instance into <see cref="Version"/>.
    /// </summary>
    /// <returns>A <see cref="Version"/> instance.</returns>
    public readonly Version ToVersion() => new(Major, Minor, Build, Revision);

    /// <summary>
    /// Create a string representation of <see cref="StructVersion"/> into "Major.Minor.Build.Revision" format if Revision number is defined or "Major.Minor.Build" if not.
    /// </summary>
    public readonly override string ToString() => ToString(string.Empty);

    /// <summary>
    /// Create a string representation of <see cref="StructVersion"/>.
    /// </summary>
    /// <param name="format">
    /// <para>
    /// An optional format specifier.<br/><br/>
    /// 
    /// If it's 'S' or 's', depending on each Major, Minor, Build or Revision field whether it's non-0, then write shorter version as possible.<br/>
    ///     For Example:<br/>
    ///       If 3.0.0.0, then write as "3"<br/>
    ///       If 3.2.0.0, then write as "3.2"<br/>
    ///       and so on.<br/><br/>
    /// 
    /// If it's 'N' or 'n', the "Major.Minor.Build" format is written.<br/>
    /// If it's 'F' or 'f', the "Major.Minor.Build.Revision" format is written.<br/><br/>
    /// 
    /// Otherwise or by default, it will automatically write to "Major.Minor.Build.Revision" format if Revision number is defined or "Major.Minor.Build" if not.
    /// </para>
    /// </param>
    /// <param name="formatProvider">A format provider instance to write the result.</param>
    /// <returns>A string representation of <see cref="StructVersion"/>.</returns>
    public readonly unsafe string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        scoped Span<char> writeStackalloc = stackalloc char[48];
        fixed (char* ptr = &writeStackalloc[0])
        {
            return !TryFormat(writeStackalloc, out int written, format.AsSpan(), formatProvider)
                ? throw new InvalidOperationException("Cannot write string to stackalloc buffer!")
                : new string(ptr, 0, written);
        }
    }

    public static bool operator <(StructVersion? left, StructVersion? right) =>
        left.HasValue && right.HasValue &&
        (left.Value.Major < right.Value.Major ||
        (left.Value.Major == right.Value.Major && left.Value.Minor < right.Value.Minor) ||
        (left.Value.Major == right.Value.Major && left.Value.Minor == right.Value.Minor && left.Value.Build < right.Value.Build) ||
        (left.Value.Major == right.Value.Major && left.Value.Minor == right.Value.Minor && left.Value.Build == right.Value.Build && left.Value.Revision < right.Value.Revision));

    public static bool operator >(StructVersion? left, StructVersion? right) =>
        right < left;

#if NET10_0_OR_GREATER
    public static bool operator <=(GameVersion? left, GameVersion? right) =>
        left < right || left == right;

    public static bool operator >=(GameVersion? left, GameVersion? right) =>
        right < left || right == left;
#endif

    public static bool operator ==(StructVersion? left, StructVersion? right) =>
        left.HasValue && right.HasValue &&
        left.Value.Major == right.Value.Major &&
        left.Value.Minor == right.Value.Minor &&
        left.Value.Build == right.Value.Build &&
        left.Value.Revision == right.Value.Revision;

    public static bool operator !=(StructVersion? left, StructVersion? right) =>
        !(left == right);

    public static bool operator ==(StructVersion? left, string? right)
    {
        if (!left.HasValue || !TryParse(right, null, out StructVersion rightAsParsed))
        {
            return false;
        }

        return left.Value == rightAsParsed;
    }

    public static bool operator !=(StructVersion? left, string? right) =>
        !(left == right);

    public static bool operator ==(string? left, StructVersion? right) =>
        right == left;

    public static bool operator !=(string? left, StructVersion? right) =>
        !(right == left);

    public readonly bool Equals(StructVersion other) => this == other;

    public readonly override bool Equals(
#if NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        object? obj) =>
        EqualsInner(this, obj);

    public readonly override int GetHashCode() =>
        HashCode.Combine(Major, Minor, Build, Revision);

    private static bool EqualsInner(object? fromVersion, object? toVersion)
    {
        if (fromVersion is StructVersion fromVersionAsIComp &&
            toVersion is string toVersionStr &&
            TryParse(toVersionStr, null, out StructVersion toVersionParsed))
        {
            return EqualsInner(fromVersionAsIComp, toVersionParsed);
        }

        return false;
    }

    private static bool EqualsInner(StructVersion? thisVersion, StructVersion? versionToCompare)
    {
        if (versionToCompare == null ||
            thisVersion == null)
        {
            return false;
        }

        ReadOnlySpan<int> thisSpan = thisVersion.Value.AsSpan();
        ReadOnlySpan<int> otherSpan = versionToCompare.Value.AsSpan();

        return otherSpan.Length == 4 &&
               thisSpan[0] == otherSpan[0] &&
               thisSpan[1] == otherSpan[1] &&
               thisSpan[2] == otherSpan[2] &&
               thisSpan[3] == otherSpan[3];
    }

    /// <summary>
    /// Formats the current <see cref="StructVersion"/> instance into a character span.
    /// </summary>
    /// <param name="destination">
    /// The span of characters to which the formatted version string will be written.
    /// </param>
    /// <param name="charsWritten">
    /// When this method returns, contains the number of characters that were written to <paramref name="destination"/>.
    /// </param>
    /// <param name="format">
    /// <para>
    /// An optional format specifier.<br/><br/>
    /// 
    /// If it's 'S' or 's', depending on each Major, Minor, Build or Revision field whether it's non-0, then write shorter version as possible.<br/>
    ///     For Example:<br/>
    ///       If 3.0.0.0, then write as "3"<br/>
    ///       If 3.2.0.0, then write as "3.2"<br/>
    ///       and so on.<br/><br/>
    /// 
    /// If it's 'N' or 'n', the "Major.Minor.Build" format is written.<br/>
    /// If it's 'F' or 'f', the "Major.Minor.Build.Revision" format is written.<br/><br/>
    /// 
    /// Otherwise or by default, it will automatically write to "Major.Minor.Build.Revision" format if Revision number is defined or "Major.Minor.Build" if not.
    /// </para>
    /// </param>
    /// <param name="provider">
    /// An optional format provider. This parameter is ignored in this implementation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if formatting was successful and the destination span was large enough. Otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The formatted version string will be in the form "Major.Minor.Build" or "Major.Minor.Build.Revision" depending on the format specifier.
    /// </remarks>
    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;

        bool isUseAutoFormat      = !format.IsEmpty && (format[0] | 0x20) == 's'; // This compares both 's' or 'S' as true.
        bool isForceUseFullFormat = !format.IsEmpty && (format[0] | 0x20) == 'f'; // This compares both 'f' or 'F' as false.
        bool isUseMiniFormat      = !isForceUseFullFormat &&
                                    ((!format.IsEmpty && (format[0] | 0x20) == 'n') // This compares both 'n' or 'N' as true.
                                     || Revision == 0);

        if (destination.Length < 4)
        {
            return false;
        }

        bool isIgnoreMinorNum = isUseAutoFormat && Minor == 0;
        if (!TryWriteAppend(Major, destination, out int offsetWritten, isIgnoreMinorNum))
        {
            return false;
        }
        charsWritten += offsetWritten;

        if (isIgnoreMinorNum)
        {
            return true;
        }

        bool isIgnoreBuildNum = isUseAutoFormat && Build == 0;
        if (!TryWriteAppend(Minor, destination.Slice(charsWritten), out offsetWritten, isIgnoreBuildNum))
        {
            return false;
        }
        charsWritten += offsetWritten;

        if (isIgnoreBuildNum)
        {
            return true;
        }

        bool isIgnoreRevisionNum = isUseAutoFormat && Revision == 0;
        if (!TryWriteAppend(Build, destination.Slice(charsWritten), out offsetWritten, isUseMiniFormat))
        {
            return false;
        }
        charsWritten += offsetWritten;

        if (isIgnoreRevisionNum || isUseMiniFormat)
        {
            return true;
        }

        if (!TryWriteAppend(Revision, destination.Slice(charsWritten), out offsetWritten, true))
        {
            return false;
        }

        charsWritten += offsetWritten;
        return true;
    }

    /// <summary>
    /// Formats the current <see cref="StructVersion"/> instance into a character span.
    /// </summary>
    /// <param name="utf8Destination">
    /// The span of UTF-8 characters to which the formatted version string will be written.
    /// </param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of characters that were written to <paramref name="utf8Destination"/>.
    /// </param>
    /// <param name="format">
    /// <para>
    /// An optional format specifier.<br/><br/>
    /// 
    /// If it's 'S' or 's', depending on each Major, Minor, Build or Revision field whether it's non-0, then write shorter version as possible.<br/>
    ///     For Example:<br/>
    ///       If 3.0.0.0, then write as "3"<br/>
    ///       If 3.2.0.0, then write as "3.2" and so on.<br/><br/>
    /// 
    /// If it's 'N' or 'n', the "Major.Minor.Build" format is written.<br/>
    /// If it's 'F' or 'f', the "Major.Minor.Build.Revision" format is written.<br/><br/>
    /// 
    /// Otherwise or by default, it will automatically write to "Major.Minor.Build.Revision" format if Revision number is defined or "Major.Minor.Build" if not.
    /// </para>
    /// </param>
    /// <param name="provider">
    /// An optional format provider. This parameter is ignored in this implementation.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if formatting was successful and the destination span was large enough. Otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The formatted version string will be in the form "Major.Minor.Build" or "Major.Minor.Build.Revision" depending on the format specifier.
    /// </remarks>
    public readonly bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        const byte formatByteS = (byte)'s';
        const byte formatByteF = (byte)'f';
        const byte formatByteN = (byte)'n';

        bytesWritten = 0;

        bool isUseAutoFormat      = !format.IsEmpty && (format[0] | 0x20) == formatByteS; // This compares both 's' or 'S' as true.
        bool isForceUseFullFormat = !format.IsEmpty && (format[0] | 0x20) == formatByteF; // This compares both 'f' or 'F' as false.
        bool isUseMiniFormat      = !isForceUseFullFormat &&
                                    ((!format.IsEmpty && (format[0] | 0x20) == formatByteN) // This compares both 'n' or 'N' as true.
                                     || Revision == 0);

        if (utf8Destination.Length < 4)
        {
            return false;
        }

        bool isIgnoreMinorNum = isUseAutoFormat && Minor == 0;
        if (!TryWriteAppend(Major, utf8Destination, out int offsetWritten, isIgnoreMinorNum))
        {
            return false;
        }
        bytesWritten += offsetWritten;

        if (isIgnoreMinorNum)
        {
            return true;
        }

        bool isIgnoreBuildNum = isUseAutoFormat && Build == 0;
        if (!TryWriteAppend(Minor, utf8Destination.Slice(bytesWritten), out offsetWritten, isIgnoreBuildNum))
        {
            return false;
        }
        bytesWritten += offsetWritten;

        if (isIgnoreBuildNum)
        {
            return true;
        }

        bool isIgnoreRevisionNum = isUseAutoFormat && Revision == 0;
        if (!TryWriteAppend(Build, utf8Destination.Slice(bytesWritten), out offsetWritten, isUseMiniFormat))
        {
            return false;
        }
        bytesWritten += offsetWritten;

        if (isIgnoreRevisionNum || isUseMiniFormat)
        {
            return true;
        }

        if (!TryWriteAppend(Revision, utf8Destination.Slice(bytesWritten), out offsetWritten, true))
        {
            return false;
        }

        bytesWritten += offsetWritten;
        return true;
    }

    private static bool TryWriteAppend(int cur, Span<char> dest, out int outWritten, bool isFinal = false)
    {
        // Ensure that the integer is non-negative before formatting
        if (cur < 0)
        {
            outWritten = 0;
            return false;
        }

#if NET6_0_OR_GREATER
        if (!cur.TryFormat(dest, out outWritten))
        {
            return false;
        }
#else
        outWritten = 0;
        string? format;
        try
        {
            format = Convert.ToString(cur);
        }
        catch
        {
            return false;
        }

        foreach (char c in format)
        {
            if (dest.Length <= outWritten)
            {
                return false;
            }
            dest[outWritten++] = c;
        }
#endif

        switch (isFinal)
        {
            case true:
                return true;
            case false when dest.Length <= outWritten:
                return false;
            default:
                dest[outWritten++] = '.';
                return true;
        }
    }

    private static bool TryWriteAppend(int cur, Span<byte> dest, out int outWritten, bool isFinal = false)
    {
        // Ensure that the integer is non-negative before formatting
        if (cur < 0)
        {
            outWritten = 0;
            return false;
        }

#if NET6_0_OR_GREATER
        if (!cur.TryFormat(dest, out outWritten))
        {
            return false;
        }
#else
        outWritten = 0;
        string? format;
        try
        {
            format = Convert.ToString(cur);
        }
        catch
        {
            return false;
        }

        foreach (char c in format)
        {
            if (dest.Length <= outWritten)
            {
                return false;
            }
            dest[outWritten++] = (byte)c;
        }
#endif

        switch (isFinal)
        {
            case true:
                return true;
            case false when dest.Length <= outWritten:
                return false;
            default:
                dest[outWritten++] = (byte)'.';
                return true;
        }
    }

    /// <summary>
    /// Parses a UTF-8 encoded byte span into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span representing the version string.</param>
    /// <param name="provider">An optional format provider. This parameter is ignored in this implementation.</param>
    /// <returns>A <see cref="StructVersion"/> instance parsed from the input.</returns>
    /// <exception cref="ArgumentException">Thrown if the input is not a valid GameVersion string.</exception>
    public static StructVersion Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        return !TryParse(utf8Text, provider, out StructVersion result) ?
            throw new ArgumentException("Input UTF-8 string is not a valid GameVersion!") : result;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span representing the version string.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="StructVersion"/> if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out StructVersion result)
        => TryParse(utf8Text, null, out result);

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded byte span representing the version string.</param>
    /// <param name="provider">An optional format provider. This parameter is ignored in this implementation.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="StructVersion"/> if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out StructVersion result)
    {
        if (utf8Text.IsEmpty)
        {
            result = default;
            return false;
        }

#if NET5_0_OR_GREATER
        Span<int> versionInt = stackalloc int[4];
        int       i          = 0;
        foreach (Range currentRange in utf8Text.SplitAny(ExSeparatorsUtf8))
        {
            if (!int.TryParse(utf8Text[currentRange], out int currentInt))
            {
                result = default;
                return false;
            }

            versionInt[i++] = currentInt;
        }

        result = new StructVersion(versionInt);
        return true;
#else
        Span<int> versionInt = stackalloc int[4];
        int       i          = 0;
        int       start      = 0;

        for (int pos = 0; pos < utf8Text.Length; pos++)
        {
            bool isSeparator = false;
            foreach (byte separator in ExSeparatorsUtf8)
            {
                if (utf8Text[pos] != separator)
                {
                    continue;
                }

                isSeparator = true;
                break;
            }

            if (!isSeparator && pos != utf8Text.Length - 1)
            {
                continue;
            }

            int                end     = isSeparator ? pos : pos + 1;
            ReadOnlySpan<byte> segment = utf8Text.Slice(start, end - start);

            if (segment.Length > 0)
            {
                char asChar = (char)segment[0];
                if (!int.TryParse($"{asChar}", out int currentInt) || i >= 4)
                {
                    result = default;
                    return false;
                }

                versionInt[i++] = currentInt;
            }

            start = pos + 1;
        }

        result = new StructVersion(versionInt);
        return true;
#endif
    }

    /// <summary>
    /// Parses a string into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="s">The string representing the version.</param>
    /// <param name="provider">An optional format provider. This parameter is ignored in this implementation.</param>
    /// <returns>A <see cref="StructVersion"/> instance parsed from the input string.</returns>
    /// <exception cref="ArgumentException">Thrown if the input is not a valid GameVersion string.</exception>
    public static StructVersion Parse(string s, IFormatProvider? provider)
        => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Attempts to parse a string into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="s">The string representing the version.</param>
    /// <param name="provider">An optional format provider. This parameter is ignored in this implementation.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="StructVersion"/> if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(
#if NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        string? s, IFormatProvider? provider, out StructVersion result)
        => TryParse(s.AsSpan(), provider, out result);

    /// <summary>
    /// Parses a character span into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="s">The character span representing the version.</param>
    /// <param name="provider">An optional format provider. This parameter is ignored in this implementation.</param>
    /// <returns>A <see cref="StructVersion"/> instance parsed from the input span.</returns>
    /// <exception cref="ArgumentException">Thrown if the input is not a valid GameVersion string.</exception>
    public static StructVersion Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return !TryParse(s, provider, out StructVersion result) ?
            throw new ArgumentException("Input string is not a valid GameVersion!") : result;
    }

    /// <summary>
    /// Attempts to parse a character span into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="versionSpan">The character span representing the version.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="StructVersion"/> if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> versionSpan, out StructVersion result)
        => TryParse(versionSpan, null, out result);

    /// <summary>
    /// Attempts to parse a character span into a <see cref="StructVersion"/> instance.
    /// </summary>
    /// <param name="versionSpan">The character span representing the version.</param>
    /// <param name="provider">An optional format provider. This parameter is ignored in this implementation.</param>
    /// <param name="result">When this method returns, contains the parsed <see cref="StructVersion"/> if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if parsing was successful; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> versionSpan, IFormatProvider? provider, out StructVersion result)
    {
        result = default;
        if (versionSpan.IsEmpty)
        {
            return false;
        }

#if !NET6_0_OR_GREATER
        // .NET Framework 4.8 compatible implementation
        string   versionString = versionSpan.ToString();
        string[] splits        = versionString.Split(ExSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (splits.Length == 0)
        {
            if (!int.TryParse(versionString.Trim(), out int majorOnly))
            {
                return false;
            }

            result = new StructVersion(majorOnly);
            return true;
        }

        scoped Span<int> versionSplits = stackalloc int[4];
        for (int i = 0; i < splits.Length && i < 4; i++)
        {
            if (!int.TryParse(splits[i].Trim(), out int versionParsed))
            {
                return false;
            }

            versionSplits[i] = versionParsed;
        }

        result = new StructVersion(versionSplits);
        return true;
#else
        scoped Span<Range> ranges = stackalloc Range[8];
        int splitRanges = versionSpan.SplitAny(ranges,
                                               ExSeparators, StringSplitOptions.TrimEntries |  StringSplitOptions.RemoveEmptyEntries);

        if (splitRanges == 0)
        {
            if (!int.TryParse(versionSpan, null, out int majorOnly))
            {
                return false;
            }

            result = new StructVersion(majorOnly);
            return true;
        }

        scoped Span<int> versionSplits = stackalloc int[4];
        for (int i = 0; i < splitRanges; i++)
        {
            if (!int.TryParse(versionSpan[ranges[i]], null, out int versionParsed))
            {
                return false;
            }

            versionSplits[i] = versionParsed;
        }

        result = new StructVersion(versionSplits);
        return true;
#endif
    }

#if NET6_0_OR_GREATER
    public readonly string VersionString => string.Join('.', VersionArray);
#endif
    public readonly int[] VersionArrayManifest => [Major, Minor, Build, Revision]; // DO NOT REMOVE: Used by legacy Collapse Launcher code
    public readonly int[] VersionArray => [Major, Minor, Build]; // DO NOT REMOVE: Used by legacy Collapse Launcher code
    public readonly int get_Major() => Major; // DO NOT REMOVE: To comply with IVersion interface
    public readonly int get_Minor() => Major; // DO NOT REMOVE: To comply with IVersion interface
    public readonly int get_Build() => Build; // DO NOT REMOVE: To comply with IVersion interface
    public readonly int get_Revision() => Revision; // DO NOT REMOVE: To comply with IVersion interface

    public readonly int Major;
    public readonly int Minor;
    public readonly int Build;
    public readonly int Revision;

    public static implicit operator StructVersion?(string? versionString)
    {
        if (versionString == null)
        {
            return null;
        }

        if (versionString.Length == 0)
        {
            return Empty;
        }

        if (TryParse(versionString.AsSpan(), out StructVersion result))
        {
            return result;
        }

        return null;
    }

    public static implicit operator StructVersion(Version? version)
        => version is null ? Empty : new StructVersion(version);

    public static implicit operator StructVersion(string version)
        => string.IsNullOrEmpty(version) ? Empty : new StructVersion(version.AsSpan());

    public static implicit operator StructVersion(ReadOnlySpan<char> versionCharSpan)
        => versionCharSpan.IsEmpty ? Empty : new StructVersion(versionCharSpan);

    public static implicit operator StructVersion(ReadOnlySpan<int> versionSpan)
        => versionSpan.IsEmpty ? Empty : new StructVersion(versionSpan);
}
