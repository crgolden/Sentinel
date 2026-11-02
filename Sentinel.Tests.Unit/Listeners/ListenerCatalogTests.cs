namespace Sentinel.Tests.Unit.Listeners;

using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ListenerCatalogTests
{
    [Fact]
    public void Validate_DistinctHttpsListeners_ReturnsThem()
    {
        // Arrange
        IReadOnlyList<ListenerDefinition> definitions = [ListenerFixtures.Definition(), ListenerFixtures.Definition(ListenerKind.Master)];

        // Act
        var actual = ListenerCatalog.Validate(definitions);

        // Assert
        Assert.Same(definitions, actual);
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("1digit")]
    [InlineData("has space")]
    [InlineData(ListenerCatalog.OrchestratorTargetId)]
    public void Validate_InvalidOrReservedId_Throws(string id)
    {
        // Arrange
        var definition = ListenerFixtures.Definition() with { Id = id };

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_HttpBaseAddress_Throws()
    {
        // Arrange
        var definition = ListenerFixtures.Definition() with { BaseAddress = new Uri($"http://{Generated.NewHostname()}/") };

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_DuplicateIds_Throws()
    {
        // Arrange
        var definition = ListenerFixtures.Definition();

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition, definition with { DisplayName = Generated.NewDisplayName() }]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_TwoMasterBridges_Throws()
    {
        // Arrange
        IReadOnlyList<ListenerDefinition> definitions = [ListenerFixtures.Definition(ListenerKind.Master), ListenerFixtures.Definition(ListenerKind.Master)];

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate(definitions));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }
}
