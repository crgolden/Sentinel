namespace Sentinel.Tests.Unit.Approvals;

using Microsoft.Extensions.Time.Testing;
using Moq;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ApprovalLedgerTests
{
    private readonly FakeTimeProvider _timeProvider = new(Generated.NewUtcTimestamp());
    private readonly Mock<IApprovalStore> _store = new(MockBehavior.Strict);
    private readonly Mock<IAuditLog> _auditLog = new(MockBehavior.Strict);
    private readonly Mock<IConversationStore> _conversations = Journals.AcceptingStore();
    private readonly ApprovalLedger _ledger;

    public ApprovalLedgerTests() =>
        _ledger = new ApprovalLedger(_store.Object, _auditLog.Object, Journals.Create(_conversations, new ConversationEventHub(), _timeProvider), _timeProvider);

    [Fact]
    public async Task RecordAsync_WritesAnEntryStampedNow()
    {
        // Arrange
        var conversation = new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8));
        var expected = new AuditEntry(_timeProvider.GetUtcNow(), conversation.OperatorId, AuditAction.MessageDispatched, conversation.TargetId, null, null);
        _auditLog.Setup(log => log.RecordAsync(expected)).Returns(Task.CompletedTask);

        // Act
        await _ledger.RecordAsync(conversation, AuditAction.MessageDispatched);

        // Assert
        _auditLog.Verify(log => log.RecordAsync(expected), Times.Once);
    }

    [Fact]
    public async Task OpenAsync_StoresAuditsAndJournalsTheApproval()
    {
        // Arrange
        var approval = ApprovalFixtures.Pending();
        var text = Generated.NewMessageText();
        _store.Setup(store => store.AddAsync(approval)).Returns(Task.CompletedTask);
        _auditLog.Setup(log => log.RecordAsync(It.Is<AuditEntry>(entry => entry.Action == AuditAction.ApprovalRequested && entry.ApprovalId == approval.Id))).Returns(Task.CompletedTask);

        // Act
        var opened = await _ledger.OpenAsync(approval, text);

        // Assert
        Assert.Same(approval, opened);
        _store.Verify(store => store.AddAsync(approval), Times.Once);
        _conversations.Verify(store => store.AppendAsync(approval.Conversation, It.Is<TranscriptEntry>(entry => entry.Kind == TranscriptEntryKind.ApprovalRequested && entry.Text == text && entry.ApprovalId == approval.Id)), Times.Once);
    }

    [Fact]
    public void Now_ReadsTheTimeProvider()
    {
        // Act
        var now = _ledger.Now;

        // Assert
        Assert.Equal(_timeProvider.GetUtcNow(), now);
    }
}
