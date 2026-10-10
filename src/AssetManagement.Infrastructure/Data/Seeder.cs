using AssetManagement.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Data
{
    public static class Seeder
    {
        public const string DefaultPassword = "Pass1234!";

        public static async Task SeedAsync(AppDbContext db, UserManager<AppUser> um, RoleManager<AppRole> rm)
        {
            if (await db.Organizations.AnyAsync()) return;

            //Organizations
            var org = new Organization { Name = "Headquarters" };
            db.Organizations.Add(org);
            await db.SaveChangesAsync();

            //Divisions
            var hq = new Division { Name = "Headquarters", OrganizationId = org.Id };
            var it = new Division { Name = "IT Department", OrganizationId = org.Id };
            var fac = new Division { Name = "Facilities Department", OrganizationId = org.Id };
            db.Divisions.AddRange(hq, it, fac);

            //Menus
            db.Menus.AddRange(
            new Menu { Key = "dashboard", Title = "Dashboard", Route = "/", Order = 1 },
            new Menu { Key = "assets", Title = "Asset Management", Route = "/assets", Order = 2 },
            new Menu { Key = "users", Title = "User Management", Route = "/users", Order = 3 });
            await db.SaveChangesAsync();

            //Roles
            foreach (var name in new[] { "Manager", "User", "CanViewAllAssets", "AssetManager", "Admin" })
                await rm.CreateAsync(new AppRole { Name = name });

            var roleIds = await rm.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id);

            //Mapping Role Permissions
            void Perm(string role, string menu, bool v, bool a, bool e, bool d) =>
            db.RoleMenuPermissions.Add(new RoleMenuPermission
            {
                RoleId = roleIds[role],
                MenuKey = menu,
                CanView = v,
                CanAdd = a,
                CanEdit = e,
                CanDelete = d
            });

            Perm("Manager", "dashboard", true, false, false, false);
            Perm("Manager", "assets", true, true, true, true);

            Perm("User", "dashboard", true, false, false, false);
            Perm("User", "assets", true, true, true, false);

            Perm("CanViewAllAssets", "assets", true, false, false, false);
            Perm("AssetManager", "assets", true, false, true, false);

            Perm("Admin", "dashboard", true, true, true, true);
            Perm("Admin", "assets", true, true, true, true);
            Perm("Admin", "users", true, true, true, true);
            await db.SaveChangesAsync();

            async Task Make(string userName, string fullName, Division d, bool isManager, params string[] roles)
            {
                var u = new AppUser
                {
                    UserName = userName,
                    Email = $"{userName}@corp.local",
                    FullName = fullName,
                    DivisionId = d.Id,
                    IsManager = isManager,
                    EmailConfirmed = true
                };
                var result = await um.CreateAsync(u, DefaultPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Gagal membuat {userName}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
                await um.AddToRolesAsync(u, roles);
            }

            await Make("sarah", "Sarah", hq, true, "Manager", "CanViewAllAssets", "AssetManager", "Admin");
            await Make("mike", "Mike", hq, false, "User");
            await Make("leo", "Leo", it, true, "Manager");
            await Make("jane", "Jane", it, false, "User");
            await Make("emma", "Emma", fac, true, "Manager");
            await Make("bob", "Bob", fac, false, "User");
        }
    }
}
