namespace AssetManagement.Domain
{
    public class Menu
    {
        public string Key { get; set; } = "";      // "dashboard", "assets", "users"
        public string Title { get; set; } = "";
        public string Route { get; set; } = "";
        public int Order { get; set; }
    }
}
