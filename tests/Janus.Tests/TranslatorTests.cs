using Agentic.Blocks.Core;
using Janus.Customers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Tests;

/// <summary>The anti-corruption layer: legacy fixed-width records in, modern customer records out.</summary>
public class TranslatorTests
{
    private static readonly ILegacyCustomerTranslator Translator =
        new ServiceCollection().AddCustomersModule().BuildServiceProvider().GetRequiredService<ILegacyCustomerTranslator>();

    //                         REC CUSTNO   SURNAME              GIVEN           DOB     ST SG SINCE   BRCH
    private const string Line = "CUS 00010213 ASHBY-LINDQVIST      KWAME           0680122 CL RT 0980430 B007";

    [Fact]
    public void Translates_a_fixed_width_record()
    {
        var result = Translator.TranslateOne(Line + "\nEOR 0001 RECORDS\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new CustomerDetails("10213", "Kwame", "Ashby-Lindqvist", new DateOnly(1968, 1, 22), "Closed", "Retail", new DateOnly(1998, 4, 30), "B007"),
            result.Value);
    }

    [Theory]
    [InlineData("0850312", 1985, 3, 12)]
    [InlineData("1150601", 2015, 6, 1)]
    [InlineData("0000101", 1900, 1, 1)]
    public void Century_digit_dates_are_decoded(string cyymmdd, int y, int m, int d)
    {
        var line = Line.Replace("0680122", cyymmdd, StringComparison.Ordinal);

        Assert.Equal(new DateOnly(y, m, d), Translator.TranslateOne(line).Value.DateOfBirth);
    }

    [Theory]
    [InlineData("ERR 0404 CUSTOMER NOT FOUND", ErrorType.NotFound, "customers.not_found")]
    [InlineData("ERR 0400 INVALID CUSTNO", ErrorType.Validation, "customers.invalid_number")]
    [InlineData("GARBAGE", ErrorType.Failure, "customers.legacy_unreadable")]
    [InlineData("", ErrorType.Failure, "customers.legacy_unreadable")]
    public void Legacy_error_lines_become_typed_errors(string body, ErrorType type, string code)
    {
        var result = Translator.TranslateOne(body);

        Assert.False(result.IsSuccess);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }

    [Theory]
    [InlineData("CL RT", "XX RT")]
    [InlineData("0680122", "0681322")]
    public void Unknown_codes_and_impossible_dates_are_rejected_not_guessed(string from, string to)
    {
        var result = Translator.TranslateOne(Line.Replace(from, to, StringComparison.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Equal("customers.legacy_unreadable", result.Error.Code);
    }

    [Fact]
    public void Translates_a_list_and_ignores_header_and_trailer()
    {
        var body = "HDR CUSTLIST 1261120\n" + Line + "\n" + Line.Replace("00010213", "00010214", StringComparison.Ordinal) + "\nEOR 0002 RECORDS\n";

        var result = Translator.TranslateMany(body);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("10214", result.Value[1].CustomerNumber);
    }

    [Fact]
    public void List_whose_trailer_count_disagrees_is_rejected()
    {
        var result = Translator.TranslateMany(Line + "\nEOR 0003 RECORDS\n");

        Assert.False(result.IsSuccess);
    }
}
