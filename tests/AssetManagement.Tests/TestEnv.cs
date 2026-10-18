using AssetManagement.Application.Abstractions;
using AssetManagement.Application.Validators;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Security.Claims;

namespace AssetManagement.Tests
{
    public class FakeAssetNumberGenerator : IAssetNumberGenerator
    {
        private int _n;
        public Task<string> NextAsync(CancellationToken ct = default) => Task.FromResult($"AST-{++_n:000}");
    }

    public sealed class TestEnv : IDisposable
    {
        private readonly SqliteConnection _conn = new("DataSource=:memory:");
        private readonly FakeAssetNumberGenerator _gen = new();
        public AppDbContext Db { get; }
        public Division Hq = null!, It = null!, Fac = null!;
        public AppUser Sarah = null!, Mike = null!, Leo = null!, Jane = null!, Emma = null!, Bob = null!;

        public TestEnv()
        {
            _conn.Open();
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options,
                                  new EphemeralDataProtectionProvider());
            Db.Database.EnsureCreated();
            Seed();
        }

        private void Seed()
        {
            var org = new Organization { Name = "Headquarters" };
            Db.Organizations.Add(org); Db.SaveChanges();
            Hq = new Division { Name = "Headquarters", OrganizationId = org.Id };
            It = new Division { Name = "IT Department", OrganizationId = org.Id };
            Fac = new Division { Name = "Facilities Department", OrganizationId = org.Id };
            Db.Divisions.AddRange(Hq, It, Fac); Db.SaveChanges();

            AppUser U(string n, Division d, bool mgr) => new()
            { Id = Guid.NewGuid().ToString(), UserName = n, NormalizedUserName = n.ToUpper(), FullName = n, DivisionId = d.Id, IsManager = mgr };
            Sarah = U("sarah", Hq, true); Mike = U("mike", Hq, false);
            Leo = U("leo", It, true); Jane = U("jane", It, false);
            Emma = U("emma", Fac, true); Bob = U("bob", Fac, false);
            Db.Users.AddRange(Sarah, Mike, Leo, Jane, Emma, Bob);

            foreach (var r in new[] { "Manager", "User", "CanViewAllAssets", "AssetManager" })
                Db.Roles.Add(new AppRole { Id = Guid.NewGuid().ToString(), Name = r, NormalizedName = r.ToUpper() });
            Db.SaveChanges();

            Db.Menus.Add(new Menu { Key = "assets", Title = "Assets", Route = "/assets", Order = 1 });
            Db.RoleMenuPermissions.AddRange(
                new RoleMenuPermission { RoleId = RoleId("Manager"), MenuKey = "assets", CanView = true, CanAdd = true, CanEdit = true, CanDelete = true },
                new RoleMenuPermission { RoleId = RoleId("User"), MenuKey = "assets", CanView = true, CanAdd = true, CanEdit = true, CanDelete = false });
            Db.UserRoles.Add(new IdentityUserRole<string> { UserId = Sarah.Id, RoleId = RoleId("AssetManager") });
            Db.SaveChanges();
        }

        private string RoleId(string name) => Db.Roles.Single(r => r.Name == name).Id;

        public ClaimsPrincipal Principal(AppUser u, params string[] roles)
        {
            var div = new[] { Hq, It, Fac }.First(d => d.Id == u.DivisionId);
            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id), new(ClaimTypes.Name, u.FullName),
            new("division_id", u.DivisionId.ToString()), new("division_name", div.Name),
            new("is_manager", u.IsManager.ToString())
        };
            foreach (var r in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
                claims.Add(new Claim("role_id", RoleId(r)));
            }
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        public AssetService Assets() => new(Db, _gen, new AssetUpsertValidator(),
            new ApprovalService(Db, NullLogger<ApprovalService>.Instance), NullLogger<AssetService>.Instance);
        public ApprovalService Approvals() => new(Db, NullLogger<ApprovalService>.Instance);
        public MaintenanceService Maint() => new(Db, Assets(), new MaintenanceUpsertValidator(), NullLogger<MaintenanceService>.Instance);

        public void Dispose() { Db.Dispose(); _conn.Dispose(); }
    }
}
