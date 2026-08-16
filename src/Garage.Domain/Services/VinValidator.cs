namespace Garage.Domain.Services;

/// <summary>
/// What we can tell about a VIN without asking anyone. A failed check digit is reported
/// separately from a malformed VIN because the check digit is mandatory in North America
/// but not in Europe — a European VIN can be perfectly genuine and still fail it, so it
/// warns rather than rejects.
/// </summary>
public record VinCheck(bool IsWellFormed, bool? CheckDigitValid, string? Problem)
{
    /// <summary>True when nothing is wrong enough to stop the user proceeding.</summary>
    public bool CanProceed => IsWellFormed;

    public bool IsSuspicious => IsWellFormed && CheckDigitValid == false;
}

/// <summary>
/// Story V-3 leans on this: a camera reads characters imperfectly, so a scan is checked
/// before it is trusted rather than being typed straight into the form.
/// </summary>
public static class VinValidator
{
    /// <summary>I, O and Q are excluded from VINs so they cannot be confused with 1 and 0.</summary>
    private const string Disallowed = "IOQ";

    private const string Weights = "8765432A098765432";

    public static VinCheck Check(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return new VinCheck(false, null, "Enter a VIN.");
        }

        var candidate = vin.Trim().ToUpperInvariant();

        if (candidate.Length != 17)
        {
            return new VinCheck(false, null, $"A VIN is 17 characters — that one is {candidate.Length}.");
        }

        if (candidate.Any(c => !char.IsAsciiLetterOrDigit(c)))
        {
            return new VinCheck(false, null, "A VIN is letters and digits only.");
        }

        if (candidate.Any(Disallowed.Contains))
        {
            return new VinCheck(false, null, "A VIN never contains the letters I, O or Q.");
        }

        return new VinCheck(true, HasValidCheckDigit(candidate), null);
    }

    /// <summary>
    /// The ninth character is a checksum over the other sixteen. Verifying it catches
    /// most single-character misreads from a scan.
    /// </summary>
    public static bool HasValidCheckDigit(string vin)
    {
        var sum = 0;

        for (var i = 0; i < 17; i++)
        {
            var value = Transliterate(vin[i]);
            if (value < 0)
            {
                return false;
            }

            var weight = Weights[i] == 'A' ? 10 : Weights[i] - '0';
            sum += value * weight;
        }

        var remainder = sum % 11;
        var expected = remainder == 10 ? 'X' : (char)('0' + remainder);

        return vin[8] == expected;
    }

    /// <summary>Letters carry numeric values in the checksum; digits stand for themselves.</summary>
    private static int Transliterate(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        'A' or 'J' => 1,
        'B' or 'K' or 'S' => 2,
        'C' or 'L' or 'T' => 3,
        'D' or 'M' or 'U' => 4,
        'E' or 'N' or 'V' => 5,
        'F' or 'W' => 6,
        'G' or 'P' or 'X' => 7,
        'H' or 'Y' => 8,
        'R' or 'Z' => 9,
        _ => -1
    };
}
