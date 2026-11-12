namespace Sentinel.Tests.Unit.Dispatch;

using System.Text.Json;
using A2A;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;

[Trait("Category", "Unit")]
public sealed class ListenerUpdateClassifierTests
{
    [Fact]
    public void Classify_StatusUpdateWithApprovalRequest_ReturnsTheStateTaskAndRequest()
    {
        // Arrange
        var taskId = Generated.LowercaseToken(12);
        var approvalId = Generated.LowercaseToken(12);
        var tool = Generated.LowercaseToken(8);
        var update = new TaskStatusUpdateEvent
        {
            TaskId = taskId,
            ContextId = Generated.LowercaseToken(12),
            Status = new TaskStatus { State = TaskState.InputRequired, Message = AgentMessage(Part.FromText(Generated.NewMessageText()), ApprovalPart(approvalId, tool)) },
        };

        // Act
        var classified = ListenerUpdateClassifier.Classify(update);

        // Assert
        Assert.Equal(TaskState.InputRequired, classified.State);
        Assert.Equal(taskId, classified.TaskId);
        Assert.Equal(approvalId, classified.Approval?.ApprovalId);
        Assert.Equal(tool, classified.Approval?.Tool);
        Assert.Equal(ApprovalRisk.High, classified.Approval?.Risk);
    }

    [Fact]
    public void Classify_TaskWithoutApproval_ReturnsTheStateOnly()
    {
        // Arrange
        var task = new AgentTask { Id = Generated.LowercaseToken(12), ContextId = Generated.LowercaseToken(12), Status = new TaskStatus { State = TaskState.Completed } };

        // Act
        var classified = ListenerUpdateClassifier.Classify(task);

        // Assert
        Assert.Equal(new ListenerUpdate(TaskState.Completed, task.Id, null), classified);
    }

    [Fact]
    public void Classify_Message_HasNoTaskState()
    {
        // Arrange
        var message = AgentMessage(Part.FromText(Generated.NewMessageText()));

        // Act
        var classified = ListenerUpdateClassifier.Classify(message);

        // Assert
        Assert.Null(classified.State);
        Assert.Null(classified.Approval);
    }

    [Fact]
    public void Classify_ArtifactUpdate_CarriesTheTaskId()
    {
        // Arrange
        var update = new TaskArtifactUpdateEvent
        {
            TaskId = Generated.LowercaseToken(12),
            ContextId = Generated.LowercaseToken(12),
            Artifact = new Artifact { ArtifactId = Generated.LowercaseToken(8), Parts = [Part.FromText(Generated.NewMessageText())] },
        };

        // Act
        var classified = ListenerUpdateClassifier.Classify(update);

        // Assert
        Assert.Equal(new ListenerUpdate(null, update.TaskId, null), classified);
    }

    [Fact]
    public void Classify_UnknownRepresentation_ReturnsNone()
    {
        // Act
        var classified = ListenerUpdateClassifier.Classify(Generated.NewMessageText());

        // Assert
        Assert.Same(ListenerUpdate.None, classified);
    }

    [Fact]
    public void FindApproval_DataPartOfAnotherKind_IsIgnored()
    {
        // Arrange
        var other = Part.FromData(JsonSerializer.SerializeToElement(new Dictionary<string, string> { [ApprovalContract.KindProperty] = Generated.LowercaseToken(10) }));

        // Act
        var approval = ListenerUpdateClassifier.FindApproval([other, Part.FromText(Generated.NewMessageText())]);

        // Assert
        Assert.Null(approval);
    }

    [Fact]
    public void ResponsePart_CarriesTheListenersApprovalIdDecisionAndOperator()
    {
        // Arrange
        var listenerApprovalId = Generated.LowercaseToken(12);
        var approval = Approval(listenerApprovalId);
        var reason = Generated.NewMessageText();

        // Act
        var part = ListenerUpdateClassifier.ResponsePart(approval, true, reason);

        // Assert
        var response = part.Data?.Deserialize<ApprovalResponsePart>(SentinelJson.Options);
        Assert.Equal(new ApprovalResponsePart(ApprovalContract.ResponseKind, listenerApprovalId, true, reason, approval.Conversation.OperatorId), response);
    }

    private static Message AgentMessage(params Part[] parts) => new()
    {
        MessageId = Generated.LowercaseToken(12),
        Role = Role.Agent,
        Parts = [.. parts],
    };

    private static Part ApprovalPart(string approvalId, string tool) => Part.FromData(JsonSerializer.SerializeToElement(
        new ApprovalRequestPart(
            ApprovalContract.RequestKind,
            approvalId,
            tool,
            JsonSerializer.SerializeToElement(new Dictionary<string, string> { [Generated.LowercaseToken(5)] = Generated.LowercaseToken(5) }),
            ApprovalRisk.High,
            Generated.NewUtcTimestamp()),
        SentinelJson.Options));

    private static PendingApproval Approval(string listenerApprovalId)
    {
        var approvalId = Guid.NewGuid();
        return new PendingApproval(
            approvalId,
            ApprovalKind.ListenerInput,
            new ConversationKey(Generated.NewUserId(), Generated.LowercaseToken(8)),
            null,
            Generated.LowercaseToken(8),
            JsonSerializer.Serialize(new Dictionary<string, string>()),
            ApprovalRisk.Low,
            Generated.NewMessageText(),
            Generated.NewUtcTimestamp(),
            null,
            listenerApprovalId,
            null);
    }
}
