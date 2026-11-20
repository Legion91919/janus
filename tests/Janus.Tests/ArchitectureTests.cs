using System.Reflection;
using Janus.Customers;
using Janus.Facade;
using Xunit;

namespace Janus.Tests;

/// <summary>Strangler-fig boundaries: the new module never knows the facade or the old system; the facade only talks to the old system over HTTP.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Customers = typeof(CustomerDetails).Assembly;
    private static readonly Assembly Facade = typeof(FacadeApp).Assembly;

    private static IEnumerable<string> References(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!);

    [Fact]
    public void New_module_does_not_reference_the_facade_or_the_legacy_service()
    {
        Assert.DoesNotContain(References(Customers), n => n is "Janus.Facade" or "Janus.LegacyTerminal");
    }

    [Fact]
    public void Facade_reaches_the_legacy_service_only_over_http()
    {
        Assert.DoesNotContain(References(Facade), n => n == "Janus.LegacyTerminal");
    }

    [Fact]
    public void New_module_exposes_only_its_contract()
    {
        var exported = Customers.GetExportedTypes().Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(["CustomerDetails", "CustomersModule", "ICustomerDirectory", "ILegacyCustomerTranslator"], exported);
    }

    [Fact]
    public void Facade_uses_only_public_types_of_the_new_module()
    {
        var used = Facade.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(f => f.FieldType)
                .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)))
            .Where(t => t.Assembly == Customers);

        Assert.All(used, t => Assert.True(t.IsPublic, $"{t.Name} is internal to the Customers module"));
    }
}
