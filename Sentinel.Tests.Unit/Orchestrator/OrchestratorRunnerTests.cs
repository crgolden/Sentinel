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
        var argumentName = Generated.LowercaseToken(7);
        var argumentValue = Generated.NewMessageText();
        var call = new FunctionCallContent(Generated.LowercaseToken(10), ListenerToolset.RelayToMasterName, new Dictionary<string, object?> { [argumentName] = argumentValue });

        // Act
        var toolCall = OrchestratorRunner.ToolCallOf(new ToolApprovalRequestContent(requestId, call));

        // Assert
        Assert.Equal((requestId, call.CallId, call.Name), (toolCall.RequestId, toolCall.CallId, toolCall.Name));
        Assert.Equal(argumentValue, JsonDocument.Parse(toolCall.ArgumentsJson).RootElement.GetProperty(argumentName).GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResponseOf_StoredToolCall_RebuildsTheResponseForTheSameCall(bool approved)
    {
        // Arrange
        var argumentName = Generated.LowercaseToken(7);
        var call = new FunctionCallContent(Generated.LowercaseToken(10), ListenerToolset.RelayToMasterName, new Dictionary<string, object?> { [argumentName] = Generated.NewMessageText() });
        var toolCall = OrchestratorRunner.ToolCallOf(new ToolApprovalRequestContent(Generated.LowercaseToken(10), call));
        var reason = Generated.NewMessageText();

        // Act
        var response = OrchestratorRunner.ResponseOf(toolCall, approved, reason);

        // Assert
        Assert.Equal((approved, reason, call.CallId), (response.Approved, response.Reason, (response.ToolCall as FunctionCallContent)?.CallId));
    }
}
