using HR28.API.Extensions;
using HR28.API.Middleware;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Data.Seed;
using HR28.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;


var builder = WebApplication.CreateBuilder(args);

// Older .xls voter lists use legacy code pages; the Excel reader needs them registered.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
// Secrets never live in committed files. Development: the connection string uses
// Windows sign-in (appsettings.Development.json) and the JWT key is in User Secrets.
// Production: both come from Azure Key Vault / app settings. Fail fast if missing.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. " +
        "Set it in appsettings.Development.json (Windows sign-in, no password) or Key Vault.");
}

// Readable SMS codes are a testing aid until a real SMS provider exists; never on a live server.
if (builder.Configuration.GetValue<bool>(AuthService.StoreReadableOtpSetting) &&
    !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"{AuthService.StoreReadableOtpSetting} is only allowed in Development.");
}

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 bytes. " +
        "Set it with 'dotnet user-secrets' (development) or Key Vault (production).");
}

// Add services to the container.
builder.Services.AddDbContext<HR28DbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVoterService, VoterService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IInfluencerService, InfluencerService>();
builder.Services.AddScoped<IInfluencerCategoryService, InfluencerCategoryService>();
builder.Services.AddScoped<IPoliticalPartyService, PoliticalPartyService>();
builder.Services.AddScoped<IVoterPhotoService, VoterPhotoService>();
builder.Services.AddScoped<IPartyMembershipImportService, PartyMembershipImportService>();
builder.Services.AddScoped<IEncounterService, EncounterService>();
builder.Services.AddScoped<IPledgeService, PledgeService>();
builder.Services.AddScoped<IVoterImportService, VoterImportService>();
builder.Services.AddScoped<IConstituencyService, ConstituencyService>();
builder.Services.AddScoped<IIslandService, IslandService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IReportingService, ReportService>();
builder.Services.AddScoped<IAccessScopeService, AccessScopeService>();
builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Development: SMS is written to the log (never sent). Elsewhere, sending fails
// safely until a real provider is connected; codes are never logged.
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<ISmsSender, DevelopmentSmsSender>();
else
    builder.Services.AddSingleton<ISmsSender, UnconfiguredSmsSender>();

// Authorization codes are stored as a keyed hash. The key comes from User Secrets
// (development) or Key Vault (production); startup fails if it is missing.
builder.Services.AddSingleton<IAuthorizationCodeHasher>(
    new HR28.Infrastructure.Helpers.AuthorizationCodeHasher(
        builder.Configuration["Security:AuthorizationCodeKey"] ?? string.Empty));
builder.Services.AddHr28AuthorizationPolicies();
builder.Services.AddHr28RateLimiting(builder.Configuration);
builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };

        // "End all sessions": a sign-in made at or before that moment is refused on its
        // next request (401), whatever the endpoint. Uses the cached access scope, which
        // is cleared when sessions are ended.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                if (context.Principal?.GetUserId() is not Guid userId)
                    return;

                var scopes = context.HttpContext.RequestServices
                    .GetRequiredService<HR28.Application.Interfaces.IAccessScopeService>();

                if ((await scopes.GetAsync(userId)).SessionsEndedAt is not { } endedAt)
                    return;

                var signedIn = long.TryParse(
                    context.Principal.FindFirst(HR28.Application.Interfaces.ITokenService.SignedInAtClaim)?.Value,
                    out var unix) ? unix : long.MinValue;

                if (signedIn <= new DateTimeOffset(DateTime.SpecifyKind(endedAt, DateTimeKind.Utc)).ToUnixTimeSeconds())
                    context.Fail("This session was ended by an administrator.");
            }
        };
    });




builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HR28 API",
        Version = "v1"
    });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT token only. Do not add the word Bearer."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearer", document)] = []
        });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HR28 API v1");
    });
}

// Must run first so rate limits see the real client IP, not the web server's.
app.UseForwardedHeaders();

// The API returns JSON only: forbid framing, sniffing and any page content.
// Swagger (Development only) needs scripts, so it is left out of the strict policy.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        return Task.CompletedTask;
    });

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseMiddleware<ApiExceptionMiddleware>();

app.UseAuthentication();

// After authentication so per-user limits know who is calling.
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<HR28DbContext>();

    // Built-in roles (with their default rights) on a new database. Constituencies and
    // islands are the client's own data (Settings → Constituencies, or the voter upload);
    // the old placeholder seeders were removed: they crashed on an empty database and
    // recreated the placeholder areas deleted from the dev data on 2026-10-04.
    await RoleSeeder.SeedRolesAsync(dbContext);

    var converted = await AuthorizationCodeBackfill.RunAsync(
        dbContext,
        scope.ServiceProvider.GetRequiredService<IAuthorizationCodeHasher>());

    if (converted > 0)
    {
        app.Logger.LogInformation(
            "Converted {Count} authorization codes to secure storage.", converted);
    }

    // Erase readable codes, unless they are deliberately kept for testing (Development only).
    var clearedOtps = app.Configuration.GetValue<bool>(AuthService.StoreReadableOtpSetting)
        ? 0
        : await AuthorizationCodeBackfill.ClearPlainOtpCodesAsync(dbContext);

    if (clearedOtps > 0)
    {
        app.Logger.LogInformation(
            "Erased {Count} SMS codes stored before hashing.", clearedOtps);
    }
}

app.Run();
// Lets the test project start the API in memory (WebApplicationFactory<Program>).
public partial class Program;
