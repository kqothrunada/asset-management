using AssetManagement.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<AppUser, AppRole, string>
    {
        private readonly IDataProtector _protector;

        public AppDbContext(DbContextOptions<AppDbContext> options, IDataProtectionProvider dataProtection)
            : base(options)
        {
            _protector = dataProtection.CreateProtector("Asset.AssetValueTotal");
        }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<Division> Divisions => Set<Division>();
        public DbSet<Menu> Menus => Set<Menu>();
        public DbSet<RoleMenuPermission> RoleMenuPermissions => Set<RoleMenuPermission>();
        public DbSet<Asset> Assets => Set<Asset>();
        public DbSet<AssetMaintenance> AssetMaintenances => Set<AssetMaintenance>();
        public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);   // wajib: membuat tabel Identity

            if (Database.IsSqlServer())
                b.HasSequence<int>("AssetNumberSeq").StartsAt(1).IncrementsBy(1);

            b.Entity<Menu>().HasKey(m => m.Key);
            b.Entity<RoleMenuPermission>().HasKey(p => new { p.RoleId, p.MenuKey });

            b.Entity<AppUser>(e =>
            {
                e.Property(u => u.FullName).HasMaxLength(100);
                e.HasOne(u => u.Division).WithMany().HasForeignKey(u => u.DivisionId);
            });

            b.Entity<Asset>(e =>
            {
                e.Property(a => a.AssetCode).HasMaxLength(10);
                e.HasIndex(a => a.AssetCode).IsUnique();
                e.Property(a => a.Name).HasMaxLength(200).IsRequired();

                e.Property(a => a.Category).HasConversion<string>().HasMaxLength(30);
                e.Property(a => a.Subcategory).HasConversion<string>().HasMaxLength(30);
                e.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);

                // Poin 15: kolom terenkripsi
                e.Property(a => a.AssetValueTotal)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(new EncryptedDecimalConverter(_protector));
            });

            b.Entity<AssetMaintenance>(e =>
            {
                e.Property(m => m.Cost).HasPrecision(18, 2);
                e.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
                e.HasOne(m => m.Asset).WithMany(a => a.Maintenances)
                 .HasForeignKey(m => m.AssetId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<ApprovalRequest>(e =>
            {
                e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
                e.HasOne(a => a.Asset).WithMany(x => x.Approvals)
                 .HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
