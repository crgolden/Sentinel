namespace Sentinel.Feeds;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record AuditRow(string Skill, DateTimeOffset Run, string Summary, Uri? Report, DateTimeOffset At);
