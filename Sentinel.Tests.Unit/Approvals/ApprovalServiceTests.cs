namespace Sentinel.Tests.Unit.Approvals;

using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Approvals;
using Sentinel.Dispatch;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ApprovalServiceTests : IDisposable
{
    private readonly TelemetryHarness _telemetry = new();
    private readonly FakeTimeProvider _timeProvider = new(Generated.NewUtcTimestamp());
    private readonly Mock<IApprovalStore> _store = new(MockBehavior.Strict);
    private readonly Mock<IAuditLog> _auditLog = new(MockBehavior.Strict);
    private readonly Mock<IDispatchQueue> _queue = new(MockBehavior.Strict);
    private readonly ApprovalService _service;

    public ApprovalServiceTests() =>
        _service = new ApprovalService(_store.Object, _auditLog.Object, _queue.Object, _timeProvider, _telemetry.Telemetry);

    [Fact]
    public async Task GetPendingAsync_ReturnsTheStoredApprovals()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();
        _store.Setup(store => store.ListAsync(approval.Conversation.OperatorId)).ReturnsAsync([approval]);

        // Act
        var pending = await _service.GetPendingAsync(approval.Conversation.OperatorId);

        // Assert
        Assert.Equal([approval], pending);
    }

    [Fact]
    public async Task DecideAsync_UnknownApproval_ReturnsFalseAndDispatchesNothing()
    {
        // Arrange
        var operatorId = Generated.NewUserId();
        var approvalId = Guid.NewGuid();
        _store.Setup(store => store.TakeAsync(operatorId, approvalId)).ReturnsAsync((PendingApproval?)null);

        // Act
        var decided = await _service.DecideAsync(operatorId, approvalId, true, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decided);
    }

    [Theory]
    [InlineData(true, AuditAction.ApprovalGranted)]
    [InlineData(false, AuditAction.ApprovalRejected)]
    public async Task DecideAsync_PendingApproval_AuditsCountsAndDispatchesTheDecision(bool approved, AuditAction action)
    {
        // Arrange
        using var capture = _telemetry.Capture(Telemetry.Metrics.ApprovalDecisionCounterName);
        var approval = ApprovalFixtures.Pending();
        var reason = Generated.NewMessageText();
        var operatorId = approval.Conversation.OperatorId;
        _store.Setup(store => store.TakeAsync(operatorId, approval.Id)).ReturnsAsync(approval);
        _auditLog.Setup(log => log.RecordAsync(new AuditEntry(_timeProvider.GetUtcNow(), operatorId, action, approval.Conversation.TargetId, approval.Id, reason))).Returns(Task.CompletedTask);
        _queue.Setup(queue => queue.EnqueueAsync(DispatchCommand.Decision(approval, approved, reason), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        // Act
        var decided = await _service.DecideAsync(operatorId, approval.Id, approved, Generated.Padded(reason), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decided);
        Assert.Single(capture.Measurements);
        _queue.Verify(queue => queue.EnqueueAsync(DispatchCommand.Decision(approval, approved, reason), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_BlankReason_IsRecordedAsNoReason()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();
        var operatorId = approval.Conversation.OperatorId;
        _store.Setup(store => store.TakeAsync(operatorId, approval.Id)).ReturnsAsync(approval);
        _auditLog.Setup(log => log.RecordAsync(It.Is<AuditEntry>(entry => string.IsNullOrWhiteSpace(entry.Detail)))).Returns(Task.CompletedTask);
        _queue.Setup(queue => queue.EnqueueAsync(DispatchCommand.Decision(approval, true, null), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        // Act
        var decided = await _service.DecideAsync(operatorId, approval.Id, true, Generated.NewBlank(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decided);
    }

    public void Dispose() => _telemetry.Dispose();
}
