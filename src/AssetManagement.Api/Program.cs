using AssetManagement.Api.Infrastructure;
using AssetManagement.Api.Security;
using AssetManagement.Application.Abstractions;
using AssetManagement.Application.Validators;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using AssetManagement.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

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

var jwt = builder.Configuration.GetSection("Jwt");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;  
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddScoped<IAuthorizationHandler, MenuPermissionHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, HQManagerHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, MenuPolicyProvider>();

builder.Services.AddAuthorization(o =>
    o.AddPolicy("assets:export", p => p
        .RequireAuthenticatedUser()
        .AddRequirements(new HQManagerRequirement())));

builder.Services.AddScoped<IAssetNumberGenerator, SqlAssetNumberGenerator>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidatorsFromAssemblyContaining<AssetUpsertValidator>();
builder.Services.AddScoped<AssetService>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Tempel token saja (tanpa kata 'Bearer')."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();
app.UseExceptionHandler();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }