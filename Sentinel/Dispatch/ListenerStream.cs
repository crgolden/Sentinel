namespace Sentinel.Dispatch;

using System.Text;
using A2A;
using Microsoft.Agents.AI;
using Sentinel.Approvals;

internal sealed class ListenerStream
{
    private readonly StringBuilder _text = new();

    public TaskState? State { get; private set; }

    public ApprovalRequestPart? Approval { get; private set; }

    public string? StatusText { get; private set; }

    public string? Reply => _text.Length > 0 ? _text.ToString() : null;

    public ListenerUpdate Apply(AgentResponseUpdate update, Action<string> publishDelta)
    {
        var classified = ListenerUpdateClassifier.Classify(update.RawRepresentation);
        State = classified.State ?? State;
        if (classified.Approval is not null)
        {
            Approval = classified.Approval;
            StatusText = update.Text;
        }
        else if (!IsNullOrWhiteSpace(update.Text))
        {
            _text.Append(update.Text);
            publishDelta(update.Text);
        }

        return classified;
    }
}
