using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Api.Security
{
    public class MenuPermissionHandler : AuthorizationHandler<MenuPermissionRequirement>
    {
        private readonly AppDbContext _db;
        public MenuPermissionHandler(AppDbContext db) => _db = db;

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MenuPermissionRequirement requirement)
        {
            var roleIds = context.User.FindAll("role_id").Select(c => c.Value).ToList();
            if (roleIds.Count == 0) return;

            var permission = await _db.RoleMenuPermissions
                .Where(p => roleIds.Contains(p.RoleId) && p.MenuKey == requirement.MenuKey)
                .ToListAsync();

            var ok = requirement.Action switch
            {
                MenuAction.View => permission.Any(p => p.CanView),
                MenuAction.Add => permission.Any(p => p.CanAdd),
                MenuAction.Edit => permission.Any(p => p.CanEdit),
                MenuAction.Delete => permission.Any(p => p.CanDelete),
                _ => false
            };

            if (ok)
                context.Succeed(requirement);
        }
    }
}
