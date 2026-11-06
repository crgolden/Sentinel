namespace Sentinel.Tests.Unit.TestSupport;

using System.Security.Claims;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sentinel.Approvals;
using Sentinel.Conversations;
using Sentinel.Dispatch;
using Sentinel.Feeds;
using Sentinel.Listeners;

internal sealed class ComponentHarness : IDisposable
{
    internal ComponentHarness()
    {
        Context = new BunitContext();
        Context.Services.AddSingleton(Directory.Object);
        Context.Services.AddSingleton(Feeds.Object);
        Context.Services.AddSingleton(Approvals.Object);
        Context.Services.AddSingleton(Reader.Object);
        Context.Services.AddSingleton(Queue.Object);
        Context.Services.AddSingleton(Audit.Object);
        Context.Services.AddSingleton<IConversationEvents>(Hub);
        var authorization = Context.AddAuthorization();
        authorization.SetAuthorized(Generated.NewDisplayName());
        authorization.SetClaims(
            new Claim(AuthorizationPolicies.SubjectClaimType, OperatorId.ToString()),
            new Claim(AuthorizationPolicies.RoleClaimType, AuthorizationPolicies.OperatorRole));
    }

    internal BunitContext Context { get; }

    internal Guid OperatorId { get; } = Generated.NewUserId();

    internal Mock<IListenerDirectory> Directory { get; } = new(MockBehavior.Strict);

    internal Mock<IFeedReader> Feeds { get; } = new(MockBehavior.Strict);

    internal Mock<IApprovalService> Approvals { get; } = new(MockBehavior.Strict);

    internal Mock<IConversationReader> Reader { get; } = new(MockBehavior.Strict);

    internal Mock<IDispatchQueue> Queue { get; } = new(MockBehavior.Strict);

    internal Mock<IAuditReader> Audit { get; } = new(MockBehavior.Strict);

    internal ConversationEventHub Hub { get; } = new();

    public void Dispose() => Context.Dispose();

    internal void EmptyConversation(ConversationKey conversation)
    {
        Reader.Setup(reader => reader.GetTranscriptAsync(conversation)).ReturnsAsync([]);
        Reader.Setup(reader => reader.GetRunAsync(conversation)).ReturnsAsync((RunRecord?)null);
    }

    internal void NoFeeds(string listenerId)
    {
        Feeds.Setup(feeds => feeds.GetStatusAsync(listenerId)).ReturnsAsync((ListenerStatusFeed?)null);
        Feeds.Setup(feeds => feeds.GetArtifactsAsync(listenerId)).ReturnsAsync((ArtifactFeed?)null);
        Feeds.Setup(feeds => feeds.GetBoardAsync(listenerId)).ReturnsAsync((BoardSnapshot?)null);
    }

    internal void Listeners(params ListenerSnapshot[] snapshots)
    {
        Directory.SetupAdd(directory => directory.Changed += It.IsAny<Action>());
        Directory.SetupRemove(directory => directory.Changed -= It.IsAny<Action>());
        Directory.Setup(directory => directory.Listeners).Returns(snapshots);
        foreach (var snapshot in snapshots)
        {
            Directory.Setup(directory => directory.Find(snapshot.Id)).Returns(snapshot);
        }
    }
}
