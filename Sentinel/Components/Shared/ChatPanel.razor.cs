namespace Sentinel.Components.Shared;

using System.Text;
using Microsoft.AspNetCore.Components;
using Sentinel.Conversations;
using Sentinel.Dispatch;

public sealed partial class ChatPanel : IDisposable
{
    private readonly StringBuilder _streaming = new();
    private readonly List<TranscriptEntry> _entries = [];
    private IDisposable? _subscription;
    private ConversationKey? _subscribedKey;

    [Parameter]
    [EditorRequired]
    public ConversationKey? Conversation { get; set; }

    [Parameter]
    [EditorRequired]
    public string? Title { get; set; }

    [Parameter]
    public bool Enabled { get; set; } = true;

    [Inject]
    public required IConversationReader Reader { get; set; }

    [Inject]
    public required IConversationEvents Events { get; set; }

    [Inject]
    public required IDispatchQueue Dispatch { get; set; }

    public IReadOnlyList<TranscriptEntry> Entries => _entries;

    public string Streaming => _streaming.ToString();

    public RunRecord? Run { get; private set; }

    public string? Draft { get; set; }

    public bool IsRunning => Run?.State == RunState.Running;

    public bool CanSend => Enabled && !IsRunning && Run?.State != RunState.AwaitingApproval;

    public void Dispose() => _subscription?.Dispose();

    protected override async Task OnParametersSetAsync()
    {
        if (Conversation is not { } conversation || conversation.Equals(_subscribedKey))
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedKey = conversation;
        _entries.Clear();
        _streaming.Clear();
        _entries.AddRange(await Reader.GetTranscriptAsync(conversation));
        Run = await Reader.GetRunAsync(conversation);
        _subscription = Events.Subscribe(conversation, OnEvent);
    }

    private async Task SendAsync()
    {
        if (Conversation is not { } conversation || IsNullOrWhiteSpace(Draft) || !CanSend)
        {
            return;
        }

        var text = Draft.Trim();
        Draft = null;
        await Dispatch.EnqueueAsync(DispatchCommand.Message(conversation, text), CancellationToken.None);
    }

    private void Cancel()
    {
        if (Conversation is { } conversation)
        {
            Dispatch.Cancel(conversation);
        }
    }

    private void OnEvent(ConversationEvent conversationEvent) =>
        _ = InvokeAsync(() =>
        {
            Apply(conversationEvent);
            StateHasChanged();
        });

    private void Apply(ConversationEvent conversationEvent)
    {
        switch (conversationEvent)
        {
            case { Kind: ConversationEventKind.EntryAppended, Entry: { } entry }:
                if (entry.Role == TranscriptRole.Agent)
                {
                    _streaming.Clear();
                }

                _entries.Add(entry);
                break;
            case { Kind: ConversationEventKind.Delta, Delta: { } delta }:
                _streaming.Append(delta);
                break;
            case { Kind: ConversationEventKind.RunStateChanged, State: { } state }:
                Run = Run is null ? null : Run with { State = state };
                Run ??= new RunRecord(Guid.Empty, state, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                if (state != RunState.Running)
                {
                    _streaming.Clear();
                }

                break;
        }
    }
}
