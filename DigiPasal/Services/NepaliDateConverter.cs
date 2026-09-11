using System.Globalization;
using System.Text.RegularExpressions;

namespace DigiPasal.Services;

/// <summary>
/// Bikram Sambat (BS) ⇄ Gregorian (AD) conversion and formatting.
/// Backed by the official month-length table (2000–2100 BS, sourced from the
/// Nepal Panchanga Nirnayak Samiti) anchored at BS 2000-01-01 = AD 1943-04-14.
/// Internal storage stays AD (<see cref="DateTime"/>); only display/parsing go through here.
/// </summary>
public readonly struct NepaliDate
{
    public NepaliDate(int year, int month, int day, DayOfWeek dayOfWeek)
    {
        Year = year;
        Month = month;
        Day = day;
        DayOfWeek = dayOfWeek;
    }

    public int Year { get; }
    public int Month { get; }
    public int Day { get; }
    public DayOfWeek DayOfWeek { get; }

    public string MonthName => NepaliDateConverter.MonthName(Month);
    public string MonthNameNepali => NepaliDateConverter.MonthNameNepali(Month);
    public string WeekdayName => NepaliDateConverter.WeekdayName(DayOfWeek);
    public string WeekdayNameNepali => NepaliDateConverter.WeekdayNameNepali(DayOfWeek);

    public string DateInEnglish => $"{Day} {MonthName} {Year}";
    public string DateInNepali => $"{NepaliDateConverter.ToNepaliDigits(Day)} {MonthNameNepali} {NepaliDateConverter.ToNepaliDigits(Year)}";

    public override string ToString() => DateInEnglish;
}

public static class NepaliDateConverter
{
    public const int StartBsYear = 2000;
    public const int EndBsYear = 2100;

    // BS 2000-01-01 (Baisakh 1) = AD 1943-04-14
    private static readonly DateTime AnchorAd = new(1943, 4, 14);

    private static readonly string[] EnglishMonths =
    {
        "Baisakh", "Jestha", "Ashar", "Shrawan", "Bhadra",
        "Ashwin", "Kartik", "Mangsir", "Poush", "Magh", "Falgun", "Chaitra"
    };

    private static readonly string[] NepaliMonths =
    {
        "बैशाख", "जेष्ठ", "आषाढ", "श्रावण", "भदौ",
        "आश्विन", "कार्तिक", "मंसिर", "पुष", "माघ", "फाल्गुन", "चैत्र"
    };

    private static readonly string[] EnglishWeekdays =
    {
        "Aaitabar", "Sombaar", "Mangalbaar", "Budhabaar", "Bihibaar", "Sukrabaar", "Sanibaar"
    };

    private static readonly string[] NepaliWeekdays =
    {
        "आइतबार", "सोमबार", "मङ्गलबार", "बुधबार", "बिहीबार", "शुक्रबार", "शनिबार"
    };

    private static readonly char[] NepaliDigits = { '०', '१', '२', '३', '४', '५', '६', '७', '८', '९' };

    /// <summary>
    /// Days in each month of the BS year, ordered Baisakh..Chaitra.
    /// Index = year - <see cref="StartBsYear"/>.
    /// </summary>
    private static readonly int[][] MonthDays =
    {
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2000
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2001
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2002
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2003
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2004
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2005
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2006
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2007
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31 }, // 2008
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2009
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2010
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2011
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30 }, // 2012
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2013
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2014
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2015
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30 }, // 2016
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2017
        new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2018
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2019
        new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2020
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2021
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30 }, // 2022
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2023
        new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2024
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2025
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2026
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2027
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2028
        new[] { 31, 31, 32, 31, 32, 30, 30, 29, 30, 29, 30, 30 }, // 2029
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2030
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2031
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2032
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2033
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2034
        new[] { 30, 32, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31 }, // 2035
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2036
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2037
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2038
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30 }, // 2039
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2040
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2041
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2042
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30 }, // 2043
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2044
        new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2045
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2046
        new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2047
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2048
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30 }, // 2049
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2050
        new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2051
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2052
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30 }, // 2053
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2054
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2055
        new[] { 31, 31, 32, 31, 32, 30, 30, 29, 30, 29, 30, 30 }, // 2056
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2057
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2058
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2059
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2060
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2061
        new[] { 30, 32, 31, 32, 31, 31, 29, 30, 29, 30, 29, 31 }, // 2062
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2063
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2064
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2065
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 29, 31 }, // 2066
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2067
        new[] { 31, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2068
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2069
        new[] { 31, 31, 31, 32, 31, 31, 29, 30, 30, 29, 30, 30 }, // 2070
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2071
        new[] { 31, 32, 31, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2072
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 31 }, // 2073
        new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2074
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2075
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30 }, // 2076
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 29, 31 }, // 2077
        new[] { 31, 31, 31, 32, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2078
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 29, 30, 30 }, // 2079
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 29, 30, 30 }, // 2080
        new[] { 31, 31, 32, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2081
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2082
        new[] { 31, 31, 32, 31, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2083
        new[] { 31, 31, 32, 31, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2084
        new[] { 31, 32, 31, 32, 30, 31, 30, 30, 29, 30, 30, 30 }, // 2085
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2086
        new[] { 31, 31, 32, 31, 31, 31, 30, 30, 29, 30, 30, 30 }, // 2087
        new[] { 30, 31, 32, 32, 30, 31, 30, 30, 29, 30, 30, 30 }, // 2088
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2089
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2090
        new[] { 31, 31, 32, 31, 31, 31, 30, 30, 29, 30, 30, 30 }, // 2091
        new[] { 30, 31, 32, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2092
        new[] { 30, 32, 31, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2093
        new[] { 31, 31, 32, 31, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2094
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 30, 30, 30, 30 }, // 2095
        new[] { 30, 31, 32, 32, 31, 30, 30, 29, 30, 29, 30, 30 }, // 2096
        new[] { 31, 32, 31, 32, 31, 30, 30, 30, 29, 30, 30, 30 }, // 2097
        new[] { 31, 31, 32, 31, 31, 31, 29, 30, 29, 30, 30, 31 }, // 2098
        new[] { 31, 31, 32, 31, 31, 31, 30, 29, 29, 30, 30, 30 }, // 2099
        new[] { 31, 32, 31, 32, 30, 31, 30, 29, 30, 29, 30, 30 }  // 2100
    };

    public static bool IsSupported(DateTime ad)
        => ad.Date >= AnchorAd && ToBsSafe(ad).Year <= EndBsYear;

    public static string MonthName(int month) => EnglishMonths[month - 1];
    public static string MonthNameNepali(int month) => NepaliMonths[month - 1];

    public static string WeekdayName(DayOfWeek dayOfWeek)
        => EnglishWeekdays[(int)dayOfWeek];

    public static string WeekdayNameNepali(DayOfWeek dayOfWeek)
        => NepaliWeekdays[(int)dayOfWeek];

    public static string ToNepaliDigits(int value)
        => ToNepaliDigits(value.ToString(CultureInfo.InvariantCulture));

    public static string ToNepaliDigits(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch >= '0' && ch <= '9')
                sb.Append(NepaliDigits[ch - '0']);
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    public static string ToEnglishDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            var idx = System.Array.IndexOf(NepaliDigits, ch);
            sb.Append(idx >= 0 ? (char)('0' + idx) : ch);
        }
        return sb.ToString();
    }

    public static NepaliDate ToBs(DateTime ad)
    {
        var days = (ad.Date - AnchorAd).Days;
        if (days < 0)
            throw new ArgumentOutOfRangeException(nameof(ad), "Date is before BS 2000-01-01 (AD 1943-04-14).");

        var year = StartBsYear;
        while (year < EndBsYear && days >= GetYearDays(year))
        {
            days -= GetYearDays(year);
            year++;
        }

        if (days >= GetYearDays(year))
            throw new ArgumentOutOfRangeException(nameof(ad), $"Date is beyond BS {EndBsYear}.");

        var months = MonthDays[year - StartBsYear];
        var month = 0;
        while (month < 12 && days >= months[month])
        {
            days -= months[month];
            month++;
        }

        return new NepaliDate(year, month + 1, days + 1, ad.DayOfWeek);
    }

    public static NepaliDate FromBs(int bsYear, int bsMonth, int bsDay)
    {
        if (bsYear < StartBsYear || bsYear > EndBsYear)
            throw new ArgumentOutOfRangeException(nameof(bsYear), $"BS year must be between {StartBsYear} and {EndBsYear}.");
        if (bsMonth < 1 || bsMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(bsMonth), "BS month must be between 1 and 12.");

        var months = MonthDays[bsYear - StartBsYear];
        if (bsDay < 1 || bsDay > months[bsMonth - 1])
            throw new ArgumentOutOfRangeException(nameof(bsDay), "BS day is outside the valid range for the month.");

        var days = bsDay - 1;
        for (var m = 1; m < bsMonth; m++)
            days += months[m - 1];
        for (var y = StartBsYear; y < bsYear; y++)
            days += GetYearDays(y);

        var ad = AnchorAd.AddDays(days);
        return new NepaliDate(bsYear, bsMonth, bsDay, ad.DayOfWeek);
    }

    private static NepaliDate ToBsSafe(DateTime ad)
    {
        try
        {
            return ToBs(ad);
        }
        catch
        {
            return default;
        }
    }

    private static int GetYearDays(int year) => MonthDays[year - StartBsYear].Sum();

    // ── Formatting ────────────────────────────────────────────────

    /// <summary>"11 Bhadau 2083"</summary>
    public static string Format(DateTime ad)
    {
        var bs = ToBsSafe(ad);
        return bs.Year == 0 ? ad.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : $"{bs.Day} {bs.MonthName} {bs.Year}";
    }

    /// <summary>"बिहीबार, 11 Bhadau 2083"</summary>
    public static string FormatWeekday(DateTime ad)
    {
        var bs = ToBsSafe(ad);
        return bs.Year == 0 ? ad.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture)
            : $"{bs.WeekdayNameNepali}, {bs.Day} {bs.MonthName} {bs.Year}";
    }

    /// <summary>"11 Bhadau"</summary>
    public static string FormatShort(DateTime ad)
    {
        var bs = ToBsSafe(ad);
        return bs.Year == 0 ? ad.ToString("dd MMM", CultureInfo.InvariantCulture)
            : $"{bs.Day} {bs.MonthName}";
    }

    /// <summary>"११ भदौ २०८३"</summary>
    public static string FormatNepali(DateTime ad)
    {
        var bs = ToBsSafe(ad);
        return bs.Year == 0 ? ad.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : FormatNepali(bs.Year, bs.Month, bs.Day);
    }

    /// <summary>"शुक्रबार, ११ भदौ २०८३"</summary>
    public static string FormatNepaliWeekday(DateTime ad)
    {
        var bs = ToBsSafe(ad);
        return bs.Year == 0 ? ad.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture)
            : $"{bs.WeekdayNameNepali}, {bs.DateInNepali}";
    }

    private static string FormatNepali(int year, int month, int day)
        => $"{ToNepaliDigits(day)} {NepaliMonths[month - 1]} {ToNepaliDigits(year)}";

    // ── Parsing (AD or BS text) ───────────────────────────────────

    private static readonly Regex DateParts = new(
        @"(?<y>\d{4})\s*[-/.]\s*(?<m>\d{1,2})\s*[-/.]\s*(?<d>\d{1,2})|(?<d2>\d{1,2})\s*[-/.]\s*(?<m2>\d{1,2})\s*[-/.]\s*(?<y2>\d{4})",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses "2026-09-11" (AD) or "2083/06/25"/"25/06/2083" (BS) into an AD DateTime.
    /// A 4-digit year ≥ 2050 is treated as BS; otherwise the value is parsed as AD.
    /// </summary>
    public static bool TryParseAny(string? value, out DateTime ad)
    {
        ad = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var s = ToEnglishDigits(value.Trim());

        var match = DateParts.Match(s);
        if (match.Success)
        {
            if (match.Groups["y"].Success || match.Groups["y2"].Success)
            {
                var bsYear = match.Groups["y"].Success
                    ? int.Parse(match.Groups["y"].Value)
                    : int.Parse(match.Groups["y2"].Value);

                if (bsYear >= 2050)
                {
                    var month = match.Groups["y"].Success
                        ? int.Parse(match.Groups["m"].Value)
                        : int.Parse(match.Groups["m2"].Value);
                    var day = match.Groups["y"].Success
                        ? int.Parse(match.Groups["d"].Value)
                        : int.Parse(match.Groups["d2"].Value);

                    try
                    {
                        ad = FromBs(bsYear, month, day).ToAdForParsing();
                        return true;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        return false;
                    }
                }
            }
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            ad = parsed;
            return true;
        }

        return false;
    }

    private static DateTime ToAdForParsing(this NepaliDate bsDate)
    {
        var days = bsDate.Day - 1;
        for (var m = 1; m < bsDate.Month; m++)
            days += MonthDays[bsDate.Year - StartBsYear][m - 1];
        for (var y = StartBsYear; y < bsDate.Year; y++)
            days += GetYearDays(y);
        return AnchorAd.AddDays(days);
    }
}