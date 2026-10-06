namespace AssetManagement.Domain
{
    public class Division
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;
    }
}
