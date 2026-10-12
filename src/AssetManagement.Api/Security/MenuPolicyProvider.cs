using AssetManagement.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AssetManagement.Api.Security
{
    public class MenuPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public MenuPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            var parts = policyName.Split(':');
            if (parts.Length == 2 && Enum.TryParse<MenuAction>(parts[1], ignoreCase: true, out var action))
            {
                return new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new MenuPermissionRequirement(parts[0], action))
                    .Build();
            }

            return await base.GetPolicyAsync(policyName);
        }
    }
}
