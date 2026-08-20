using System.Globalization;

namespace HealthLens.Api.Services.Csv;

/// <summary>
/// Timestamp parsing for the Takeout exports. Every intraday row carries a timestamp, so this is the
/// single hottest parse in an import: the ISO-8601 shape Google emits
/// (<c>2024-01-29T05:12:33[.fff][Z|+hh:mm]</c>) is decoded digit-by-digit instead of going through
/// <see cref="DateTime.TryParse(ReadOnlySpan{char}, IFormatProvider, DateTimeStyles, out DateTime)"/>
/// and its format probing. Anything that doesn't match falls back to TryParse, so odd rows still parse.
/// Timestamps without a zone are treated as UTC, matching the rest of this app.
/// </summary>
public static class Timestamps
{
    public static bool TryParseUtc(ReadOnlySpan<char> raw, out DateTime utc)
    {
        var trimmed = raw.Trim();
        if (trimmed.IsEmpty)
        {
            utc = default;
            return false;
        }

        return TryParseIso(trimmed, out utc)
            || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }

    private static bool TryParseIso(ReadOnlySpan<char> s, out DateTime utc)
    {
        utc = default;

        if (s.Length < 19 || s[4] != '-' || s[7] != '-' || (s[10] != 'T' && s[10] != ' ') || s[13] != ':' || s[16] != ':')
        {
            return false;
        }

        if (!TryDigits(s[..4], out var year) || !TryDigits(s.Slice(5, 2), out var month) || !TryDigits(s.Slice(8, 2), out var day) ||
            !TryDigits(s.Slice(11, 2), out var hour) || !TryDigits(s.Slice(14, 2), out var minute) || !TryDigits(s.Slice(17, 2), out var second))
        {
            return false;
        }

        if (year is < 1 or > 9999 || (uint)(month - 1) > 11 || hour > 23 || minute > 59 || second > 59 ||
            day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        var rest = s[19..];
        long fractionTicks = 0;

        if (rest.Length > 0 && rest[0] is '.' or ',')
        {
            var end = 1;
            while (end < rest.Length && char.IsAsciiDigit(rest[end]))
            {
                end++;
            }

            var digits = rest[1..end];
            for (var i = 0; i < 7; i++)
            {
                fractionTicks = (fractionTicks * 10) + (i < digits.Length ? digits[i] - '0' : 0);
            }

            rest = rest[end..];
        }

        var offsetMinutes = 0;
        if (rest.Length > 0)
        {
            if (rest[0] is 'Z' or 'z')
            {
                if (rest.Length != 1)
                {
                    return false;
                }
            }
            else if (rest[0] is '+' or '-')
            {
                var body = rest[1..];
                var minutesPart = body.Length switch
                {
                    5 when body[2] == ':' => body[3..],
                    4 => body[2..],
                    _ => default,
                };

                if (minutesPart.IsEmpty || !TryDigits(body[..2], out var offsetHours) || !TryDigits(minutesPart, out var offsetMins))
                {
                    return false;
                }

                offsetMinutes = (offsetHours * 60) + offsetMins;
                if (rest[0] == '-')
                {
                    offsetMinutes = -offsetMinutes;
                }
            }
            else
            {
                return false;
            }
        }

        utc = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc)
            .AddTicks(fractionTicks)
            .AddMinutes(-offsetMinutes);
        return true;
    }

    private static bool TryDigits(ReadOnlySpan<char> s, out int value)
    {
        value = 0;
        foreach (var c in s)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        return true;
    }
}
