namespace Sentinel.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

public abstract class OperatorComponentBase : ComponentBase
{
    [CascadingParameter]
    public Task<AuthenticationState>? AuthenticationState { get; set; }

    protected Guid OperatorId { get; private set; }

    protected override async Task OnParametersSetAsync()
    {
        var authenticationState = AuthenticationState ?? throw new InvalidOperationException("The authentication state is not cascaded.");
        var state = await authenticationState;
        OperatorId = AuthorizationPolicies.OperatorId(state.User) ?? throw new InvalidOperationException("The operator has no subject id.");
        await OnOperatorReadyAsync();
    }

    protected virtual Task OnOperatorReadyAsync() => Task.CompletedTask;
}
