using Microsoft.AspNetCore.Identity;

namespace AssetManagement.Domain
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = "";
        public int DivisionId { get; set; }
        public Division Division { get; set; } = null!;
        public bool IsManager { get; set; }
    }
}
