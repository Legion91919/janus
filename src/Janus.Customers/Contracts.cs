using Agentic.Blocks.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Janus.Customers;

/// <summary>The modern customer record. Status: Active, Dormant, Closed. Segment: Retail, SmallBusiness.</summary>
public sealed record CustomerDetails(
    string CustomerNumber,
    string GivenName,
    string FamilyName,
    DateOnly DateOfBirth,
    string Status,
    string Segment,
    DateOnly CustomerSince,
    string Branch);

/// <summary>Customer details owned by the new module (the capability that has moved off the old terminal).</summary>
public interface ICustomerDirectory
{
    Task<Result<CustomerDetails>> FindAsync(string customerNumber, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerDetails>> ListAsync(CancellationToken ct = default);
}

/// <summary>Anti-corruption layer: turns the old terminal's fixed-width text into <see cref="CustomerDetails"/>.</summary>
public interface ILegacyCustomerTranslator
{
    Result<CustomerDetails> TranslateOne(string legacyBody);

    Result<IReadOnlyList<CustomerDetails>> TranslateMany(string legacyBody);
}

public static class CustomersModule
{
    /// <summary>Registers the module. <paramref name="seedDemoData"/> loads invented customers (Development and tests only).</summary>
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, bool seedDemoData = false)
    {
        services.TryAddSingleton<ILegacyCustomerTranslator, LegacyCustomerTranslator>();
        services.TryAddSingleton<ICustomerDirectory>(_ => new InMemoryCustomerDirectory(seedDemoData ? DemoCustomers.All : []));
        return services;
    }
}
