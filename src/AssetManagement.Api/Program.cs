using AssetManagement.Application.Abstractions;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Data Protection: key ring disimpan di file agar enkripsi AssetValueTotal tetap bisa didekripsi
builder.Services.AddDataProtection()
    .SetApplicationName("AssetManagement")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyPath"] ?? "./keys"));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<AppUser>(o =>
{
    o.Password.RequireNonAlphanumeric = true;
})
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<IAssetNumberGenerator, SqlAssetNumberGenerator>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();

    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch when (attempt < 10)
        {
            app.Logger.LogWarning("Database belum siap (percobaan {Attempt}/10), mencoba lagi...", attempt);
            await Task.Delay(3000);
        }
    }

    await Seeder.SeedAsync(db,
        sp.GetRequiredService<UserManager<AppUser>>(),
        sp.GetRequiredService<RoleManager<AppRole>>());
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }