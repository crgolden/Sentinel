namespace Sentinel.Tests.Unit.Listeners;

using Sentinel.Listeners;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ListenerRegistryTests
{
    [Fact]
    public void Listeners_OrdersTheMasterFirstThenPinnedThenByName()
    {
        // Arrange
        var tool = ListenerFixtures.Definition() with { DisplayName = Generated.NewTokenFromSecondHalfOfAlphabet(8) };
        var pinned = ListenerFixtures.Definition(pinned: true);
        var master = ListenerFixtures.Definition(ListenerKind.Master);
        var other = ListenerFixtures.Definition() with { DisplayName = Generated.NewTokenFromFirstHalfOfAlphabet(8) };
        var registry = new ListenerRegistry([tool, pinned, master, other]);

        // Act
        var ordered = registry.Listeners;

        // Assert
        Assert.Equal([master.Id, pinned.Id, other.Id, tool.Id], ordered.Select(snapshot => snapshot.Id));
    }

    [Fact]
    public void Listeners_BeforeAnyRefresh_AreUnknown()
    {
        // Arrange
        var registry = new ListenerRegistry([ListenerFixtures.Definition()]);

        // Act
        var snapshot = Assert.Single(registry.Listeners);

        // Assert
        Assert.Equal(ListenerState.Unknown, snapshot.State);
    }

    [Fact]
    public void Update_StateChange_RaisesChangedAndReplacesTheSnapshot()
    {
        // Arrange
        var definition = ListenerFixtures.Definition();
        var registry = new ListenerRegistry([definition]);
        var raised = 0;
        registry.Changed += () => raised++;
        var online = ListenerFixtures.Online(definition);

        // Act
        registry.Update(online);

        // Assert
        Assert.Equal(1, raised);
        Assert.Same(online, registry.Find(definition.Id));
    }

    [Fact]
    public void Update_SameStateAndCardVersion_DoesNotRaiseChanged()
    {
        // Arrange
        var definition = ListenerFixtures.Definition();
        var registry = new ListenerRegistry([definition]);
        var online = ListenerFixtures.Online(definition);
        registry.Update(online);
        var raised = 0;
        registry.Changed += () => raised++;

        // Act
        registry.Update(online with { LastCheckedAt = Generated.NewUtcTimestamp() });

        // Assert
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Update_UnknownListener_Throws()
    {
        // Arrange
        var registry = new ListenerRegistry([ListenerFixtures.Definition()]);

        // Act
        var exception = Record.Exception(() => registry.Update(ListenerFixtures.Online(ListenerFixtures.Definition())));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Find_UnconfiguredId_ReturnsNull()
    {
        // Arrange
        var registry = new ListenerRegistry([ListenerFixtures.Definition()]);

        // Act
        var snapshot = registry.Find(Generated.LowercaseToken(9));

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public void Definitions_ReturnsEveryConfiguredDefinition()
    {
        // Arrange
        var definition = ListenerFixtures.Definition();
        var registry = new ListenerRegistry([definition]);

        // Act
        var definitions = registry.Definitions;

        // Assert
        Assert.Equal([definition], definitions);
    }
}
