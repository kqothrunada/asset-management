namespace AssetManagement.Domain
{
    public class RoleMenuPermission
    {
        public string RoleId { get; set; } = "";
        public string MenuKey { get; set; } = "";
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
