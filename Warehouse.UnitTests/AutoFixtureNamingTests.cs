using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;

namespace Warehouse.UnitTests;

/// <summary>
/// Demonstrates AutoFixture for non-critical fields (names); business rules stay explicit elsewhere.
/// </summary>
public class AutoFixtureNamingTests
{
    [Theory]
    [AutoData]
    public void Fixture_generates_non_empty_zone_name(string zoneName)
    {
        zoneName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Explicit_fixture_customization_for_strings()
    {
        var fixture = new Fixture();
        var name = fixture.Create<string>();
        name.Should().NotBeNull();
    }
}
