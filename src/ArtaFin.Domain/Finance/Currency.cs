namespace ArtaFin.Domain.Finance;

/// <summary>
/// ISO 4217 currency metadata supported by this application.
/// The catalog is intentionally explicit: adding a currency requires reviewing its
/// ISO minor-unit exponent and the persistence precision before it can be used.
/// </summary>
public readonly record struct Currency
{
    public string Code { get; }
    public int MinorUnit { get; }

    private Currency(string code, int minorUnit)
    {
        Code = code;
        MinorUnit = minorUnit;
    }

    public static Currency FromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Currency code is required.", nameof(code));

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length != 3 || normalizedCode.Any(character => !char.IsAsciiLetter(character)))
            throw new ArgumentException("Currency code must contain exactly three ASCII letters.", nameof(code));

        var minorUnit = GetMinorUnit(normalizedCode);
        if (minorUnit is null)
            throw new NotSupportedException($"Currency '{normalizedCode}' is not currently supported.");

        return new Currency(normalizedCode, minorUnit.Value);
    }

    internal void EnsureValid()
    {
        if (string.IsNullOrEmpty(Code) || GetMinorUnit(Code) != MinorUnit)
            throw new ArgumentException("A valid, catalogued currency is required.", nameof(Currency));
    }

    private static int? GetMinorUnit(string code) => code switch
    {
        // ISO 4217 currencies with zero minor units.
        "BIF" or "CLP" or "DJF" or "GNF" or "ISK" or "JPY" or "KMF" or
        "KRW" or "PYG" or "RWF" or "UGX" or "UYI" or "VND" or "XAF" or
        "XOF" or "XPF" => 0,

        // ISO 4217 currencies with three minor units.
        "BHD" or "IQD" or "JOD" or "KWD" or "LYD" or "OMR" or "TND" => 3,

        // ISO 4217 units with four decimal places.
        "CLF" or "UYW" => 4,

        // Supported two-minor-unit currencies.
        "AED" or "AFN" or "ALL" or "AMD" or "ANG" or "AOA" or "ARS" or
        "AUD" or "AWG" or "AZN" or "BAM" or "BBD" or "BDT" or "BGN" or
        "BMD" or "BND" or "BOB" or "BRL" or "BSD" or "BTN" or "BWP" or
        "BYN" or "BZD" or "CAD" or "CDF" or "CHF" or "CNY" or "COP" or
        "CRC" or "CUP" or "CVE" or "CZK" or "DKK" or "DOP" or "DZD" or
        "EGP" or "ERN" or "ETB" or "EUR" or "FJD" or "GBP" or "GEL" or
        "GHS" or "GIP" or "GMD" or "GTQ" or "GYD" or "HKD" or "HNL" or
        "HTG" or "HUF" or "IDR" or "ILS" or "INR" or "IRR" or "JMD" or
        "KES" or "KGS" or "KHR" or "KZT" or "LAK" or "LBP" or "LKR" or
        "MAD" or "MDL" or "MGA" or "MKD" or "MMK" or "MNT" or "MOP" or
        "MRU" or "MUR" or "MVR" or "MWK" or "MXN" or "MYR" or "MZN" or
        "NAD" or "NGN" or "NIO" or "NOK" or "NPR" or "NZD" or "PAB" or
        "PEN" or "PGK" or "PHP" or "PKR" or "PLN" or "QAR" or "RON" or
        "RSD" or "RUB" or "SAR" or "SBD" or "SCR" or "SDG" or "SEK" or
        "SGD" or "SHP" or "SLE" or "SOS" or "SRD" or "SSP" or "STN" or
        "SVC" or "SZL" or "THB" or "TJS" or "TMT" or "TOP" or "TRY" or
        "TTD" or "TWD" or "TZS" or "UAH" or "USD" or "UYU" or "UZS" or
        "VES" or "WST" or "XCD" or "YER" or "ZAR" or "ZMW" or "ZWG" => 2,

        _ => null
    };
}
