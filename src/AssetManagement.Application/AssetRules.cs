using AssetManagement.Domain;
using System.Text.RegularExpressions;

namespace AssetManagement.Application
{
    public static class AssetRules
    {
        public static readonly IReadOnlyDictionary<AssetCategory, AssetSubcategory[]> SubcategoriesByCategory =
        new Dictionary<AssetCategory, AssetSubcategory[]>
        {
            [AssetCategory.ITEquipment] = new[] { AssetSubcategory.Laptop, AssetSubcategory.Monitor, AssetSubcategory.Printer },
            [AssetCategory.Furniture] = new[] { AssetSubcategory.Chair, AssetSubcategory.Table, AssetSubcategory.Cabinet },
        };

        public static bool IsValidPair(AssetCategory c, AssetSubcategory s) =>
            SubcategoriesByCategory.TryGetValue(c, out var list) && list.Contains(s);

        public static bool IsFurniture(AssetSubcategory s) =>
            s is AssetSubcategory.Chair or AssetSubcategory.Table or AssetSubcategory.Cabinet;

        public static bool HasMaintenance(AssetSubcategory s) => !IsFurniture(s);

        public static bool HasText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return false;
            var text = Regex.Replace(html, "<[^>]*>", "").Replace("&nbsp;", " ");
            return !string.IsNullOrWhiteSpace(text);
        }
    }
}
