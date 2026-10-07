using AssetManagement.Application.Abstractions;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
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

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }