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

    [Fact]
    public void Validate_UppercaseId_Throws()
    {
        // Arrange
        var uppercaseId = Generated.UppercaseToken(8);
        var definition = ListenerFixtures.Definition() with { Id = uppercaseId };

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_IdStartingWithADigit_Throws()
    {
        // Arrange
        var digitFirstId = $"{Generated.DigitToken(1)}{Generated.LowercaseToken(7)}";
        var definition = ListenerFixtures.Definition() with { Id = digitFirstId };

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_IdWithASpace_Throws()
    {
        // Arrange
        var spacedId = $"{Generated.LowercaseToken(4)} {Generated.LowercaseToken(4)}";
        var definition = ListenerFixtures.Definition() with { Id = spacedId };

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_ReservedOrchestratorId_Throws()
    {
        // Arrange
        var definition = ListenerFixtures.Definition() with { Id = ListenerCatalog.OrchestratorTargetId };

        // Act
        var exception = Record.Exception(() => ListenerCatalog.Validate([definition]));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Validate_HttpBaseAddress_Throws()
    {
        // Arrange
        var definition = ListenerFixtures.Definition() with { BaseAddress = new UriBuilder(Uri.UriSchemeHttp, Generated.NewHostname()).Uri };

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
