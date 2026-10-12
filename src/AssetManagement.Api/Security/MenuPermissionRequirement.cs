using AssetManagement.Domain;
using Microsoft.AspNetCore.Authorization;

namespace AssetManagement.Api.Security
{
    public record MenuPermissionRequirement(string MenuKey, MenuAction Action) : IAuthorizationRequirement;
}
