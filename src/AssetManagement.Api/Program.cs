using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AssetManagement.Api.Clients;
using AssetManagement.Api.Infrastructure;
using AssetManagement.Api.Security;
using AssetManagement.Api.Soap;
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
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using SoapCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.MinimumLevel.Information()
       .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
       .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("Application", "AssetManagement.Api")
       .WriteTo.Console();

    var es = ctx.Configuration["Elastic:Uri"];
    if (!string.IsNullOrWhiteSpace(es))
        cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(es))
        {
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            IndexFormat = "assetmgmt-logs-{0:yyyy.MM.dd}",
            TypeName = "_doc"
        });
});

// ---------- Data & Identity ----------
builder.Services.AddDataProtection()
    .SetApplicationName("AssetManagement")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"] ?? "./keys"));

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<AppUser>(o => o.Password.RequireNonAlphanumeric = true)
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// ---------- Auth ----------
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
    o.AddPolicy("assets:export", p => p.RequireAuthenticatedUser().AddRequirements(new HQManagerRequirement())));

// ---------- Aplikasi ----------
builder.Services.AddScoped<IAssetNumberGenerator, SqlAssetNumberGenerator>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<MaintenanceService>();
builder.Services.AddValidatorsFromAssemblyContaining<AssetUpsertValidator>();

// ---------- Protokol & klien ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSoapCore();
builder.Services.AddScoped<IAssetSoapService, AssetSoapService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAssetClient, RestAssetClient>();
builder.Services.AddScoped<IAssetClient, JsonRpcAssetClient>();
builder.Services.AddScoped<IAssetClient, XmlRpcAssetClient>();
builder.Services.AddScoped<IAssetClient, SoapAssetClient>();

// ---------- MVC, Swagger, error ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
            = Array.Empty<string>()
    });
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Migrate + seed 
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch when (attempt < 15)
        {
            app.Logger.LogWarning("Database belum siap (percobaan {Attempt}/15)...", attempt);
            await Task.Delay(3000);
        }
    }
    await Seeder.SeedAsync(db, sp.GetRequiredService<UserManager<AppUser>>(), sp.GetRequiredService<RoleManager<AppRole>>());
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    using (LogContext.PushProperty("UserName", ctx.User.Identity?.Name ?? "anonymous"))
        await next();
});
app.UseAuthorization();

((IEndpointRouteBuilder)app).UseSoapEndpoint<IAssetSoapService>(
    "/soap/assets", new SoapEncoderOptions(), SoapSerializer.DataContractSerializer);

app.MapControllers();

app.Run();

public partial class Program { }