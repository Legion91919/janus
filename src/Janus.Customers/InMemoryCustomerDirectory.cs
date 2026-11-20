using System.Collections.Concurrent;
using Agentic.Blocks.Core;

namespace Janus.Customers;

/// <summary>Thread-safe in-memory store (persistence is slice 2). Ordered by customer number, like the old terminal.</summary>
internal sealed class InMemoryCustomerDirectory(IEnumerable<CustomerDetails> seed) : ICustomerDirectory
{
    private readonly ConcurrentDictionary<string, CustomerDetails> _customers =
        new(seed.Select(c => KeyValuePair.Create(c.CustomerNumber, c)), StringComparer.Ordinal);

    public Task<Result<CustomerDetails>> FindAsync(string customerNumber, CancellationToken ct = default)
    {
        var number = CustomerNumber.Normalise(customerNumber);
        if (!number.IsSuccess) return Task.FromResult<Result<CustomerDetails>>(number.Error);
        return Task.FromResult<Result<CustomerDetails>>(
            _customers.TryGetValue(number.Value, out var customer) ? customer : Errors.NotFound);
    }

    public Task<IReadOnlyList<CustomerDetails>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CustomerDetails>>(
            _customers.Values.OrderBy(c => c.CustomerNumber.PadLeft(8, '0'), StringComparer.Ordinal).ToList());
}

/// <summary>Invented customers, held in the new module's own format (the old terminal has its own copy).</summary>
internal static class DemoCustomers
{
    public static readonly IReadOnlyList<CustomerDetails> All =
    [
        new("10042", "Ines", "Calloway", new(1985, 3, 12), "Active", "Retail", new(2015, 6, 1), "B014"),
        new("10077", "Tobias", "Renner", new(1979, 11, 30), "Active", "SmallBusiness", new(2009, 2, 17), "B003"),
        new("10105", "Naledi", "Fourie", new(2001, 7, 4), "Dormant", "Retail", new(2021, 9, 9), "B014"),
        new("10213", "Kwame", "Ashby-Lindqvist", new(1968, 1, 22), "Closed", "Retail", new(1998, 4, 30), "B007"),
        new("10388", "Priya", "Vellum", new(1992, 12, 5), "Active", "SmallBusiness", new(2018, 10, 15), "B003"),
        new("10450", "Oskar", "Brandt", new(1955, 5, 19), "Active", "Retail", new(1987, 8, 3), "B001"),
    ];
}
