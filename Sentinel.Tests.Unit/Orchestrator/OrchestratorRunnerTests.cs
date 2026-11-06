namespace Sentinel.Tests.Unit.Orchestrator;

using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentinel.Orchestrator;

[Trait("Category", "Unit")]
public sealed class OrchestratorRunnerTests
{
    [Fact]
    public void ToolCallOf_FunctionCall_CapturesTheRequestCallNameAndArguments()
    {
        // Arrange
        var requestId = Generated.LowercaseToken(10);
        var call = new FunctionCallContent(Generated.LowercaseToken(10), ListenerToolset.RelayToMasterName, new Dictionary<string, object?> { ["message"] = Generated.NewMessageText() });

        // Act
        var toolCall = OrchestratorRunner.ToolCallOf(new ToolApprovalRequestContent(requestId, call));

        // Assert
        Assert.Equal((requestId, call.CallId, call.Name), (toolCall.RequestId, toolCall.CallId, toolCall.Name));
        Assert.Equal(call.Arguments?["message"], JsonDocument.Parse(toolCall.ArgumentsJson).RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResponseOf_StoredToolCall_RebuildsTheResponseForTheSameCall(bool approved)
    {
        // Arrange
        var call = new FunctionCallContent(Generated.LowercaseToken(10), ListenerToolset.RelayToMasterName, new Dictionary<string, object?> { ["message"] = Generated.NewMessageText() });
        var toolCall = OrchestratorRunner.ToolCallOf(new ToolApprovalRequestContent(Generated.LowercaseToken(10), call));
        var reason = Generated.NewMessageText();

        // Act
        var response = OrchestratorRunner.ResponseOf(toolCall, approved, reason);

        // Assert
        Assert.Equal((approved, reason, call.CallId), (response.Approved, response.Reason, (response.ToolCall as FunctionCallContent)?.CallId));
    }
}
