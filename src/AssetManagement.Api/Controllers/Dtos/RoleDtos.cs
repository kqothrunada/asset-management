using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Api.Controllers.Dtos
{
    public record RoleListDto(string Id, string Name, int UserCount);

    public class RoleUpsertDto
    {
        [Required, StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = "";
    }
}
