using AssetManagement.Application.Exceptions;
using System.Security.Claims;

namespace AssetManagement.Application
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal u) =>
        u.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new ForbiddenException("User Identity is not found.");

        public static int GetDivisionId(this ClaimsPrincipal u) =>
            int.TryParse(u.FindFirst("division_id")?.Value, out var id) ? id : 0;

        public static bool IsItDepartment(this ClaimsPrincipal u) =>
            u.FindFirst("division_name")?.Value == AppConstants.ITDepartment;
    }
}
