namespace Sentinel.Components;

public static class PageRoutes
{
    public const string Approvals = "approvals";

    public const string Logout = "logout";

    public static string Listener(string listenerId) => $"listeners/{listenerId}";
}
