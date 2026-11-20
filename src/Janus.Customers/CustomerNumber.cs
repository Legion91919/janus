using Agentic.Blocks.Core;

namespace Janus.Customers;

/// <summary>Customer numbers are up to 8 digits; the old terminal zero-pads them, the new API does not.</summary>
internal static class CustomerNumber
{
    public static Result<string> Normalise(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > 8 || !raw.All(char.IsAsciiDigit))
        {
            return Errors.InvalidNumber;
        }

        var trimmed = raw.TrimStart('0');
        return trimmed.Length == 0 ? Errors.InvalidNumber : trimmed;
    }
}

internal static class Errors
{
    public static readonly Error NotFound = Error.NotFound("customers.not_found", "No customer has that number.");
    public static readonly Error InvalidNumber = Error.Validation("customers.invalid_number", "customerNumber", "A customer number is 1 to 8 digits.");
    public static Error Unreadable(string why) => Error.Failure("customers.legacy_unreadable", "The old system returned a record the bridge cannot read: " + why);
}
