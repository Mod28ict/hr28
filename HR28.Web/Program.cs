using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Keeps a working person signed in: refreshes the 1-hour sign-in token before it runs out.
    options.Filters.Add<HR28.Web.Filters.SessionTokenRefreshFilter>();

    // Re-checks the signed-in user's role with the API about once a minute.
    options.Filters.Add<HR28.Web.Filters.SessionRoleRefreshFilter>();

    // Every POST/PUT/DELETE must carry the anti-forgery token (forms add it automatically).
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());

    // A failed check goes back to the page with a plain message, not an empty 400 page.
    options.Filters.Add<HR28.Web.Filters.FriendlyAntiforgeryFailureFilter>();
});
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));


builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<SessionKeeper>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<BrandingService>();
builder.Services.AddDistributedMemoryCache();

// Session holds the API token: HTTPS-only, hidden from scripts, not sent cross-site,
// and ended after an hour without activity. The page asks "Stay signed in?" a minute
// before and signs out itself (site.js); the server keeps it 2 minutes longer so the
// page always decides first.
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "HR28.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = SessionKeeper.IdleLimit + TimeSpan.FromMinutes(2);
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// Behind Cloudflare: the visitor's address comes from CF-Connecting-IP, trusted only
// from Cloudflare's networks (ReverseProxy:Cloudflare = true). Off by default.
var cloudflareClientIp = HR28.Web.Middleware.CloudflareClientIp.CreateOptions(builder.Configuration);

// Behind Nginx on the same server (ReverseProxy:BehindLocalProxy = true): Nginx passes the
// visitor's address and "came over HTTPS"; trusted only from the server itself (loopback).
// Nginx takes the visitor's address from Cloudflare, so ReverseProxy:Cloudflare stays off.
var localProxy = builder.Configuration.GetValue<bool>("ReverseProxy:BehindLocalProxy")
    ? new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    }
    : null;

// The keys that protect sign-in sessions and forms. On a server they must survive restarts
// and updates, or everyone is signed out: kept in DataProtection:KeysPath (a folder only the
// web app's account can read). Not set (development): the default per-user location.
var keysPath = builder.Configuration["DataProtection:KeysPath"];

if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("HR28.Web")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

// First, so everything after it (including the address forwarded to the API) sees the visitor.
if (cloudflareClientIp != null)
    app.UseForwardedHeaders(cloudflareClientIp);
else if (localProxy != null)
    app.UseForwardedHeaders(localProxy);

app.UseMiddleware<HR28.Web.Middleware.SecurityHeadersMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
