using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Api.Controllers.Dtos
{
    public record UserListDto(
        string Id, string UserName, string FullName, string? Email,
        int DivisionId, string DivisionName, bool IsManager, List<string> Roles);

    public class CreateUserDto
    {
        [Required, StringLength(50, MinimumLength = 3)]
        public string UserName { get; set; } = "";

        [Required, StringLength(100)]
        public string FullName { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string Password { get; set; } = "";

        [Required]
        public int DivisionId { get; set; }

        public bool IsManager { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class UpdateUserDto
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public int DivisionId { get; set; }

        public bool IsManager { get; set; }
        public List<string> Roles { get; set; } = new();

        public string? NewPassword { get; set; }
    }
}
