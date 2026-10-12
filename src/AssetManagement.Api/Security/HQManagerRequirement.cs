using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AssetManagement.Api.Security
{
    public class HQManagerRequirement : IAuthorizationRequirement { }
    
    public class HQManagerHandler : AuthorizationHandler<HQManagerRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HQManagerRequirement requirement)
        {
            var isHq = context.User.FindFirstValue("division_name") == "Headquarters";
            var isManager = context.User.FindFirstValue("is_manager") == "True";

            if (isHq && isManager)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
