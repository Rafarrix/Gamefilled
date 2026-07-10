using Gamefilled.Application.Companies;
using Gamefilled.Application.Games;
using Gamefilled.Application.Social;
using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

/* ============================================================================
   RAZOR PAGES
   ============================================================================ */
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddFolderApplicationModelConvention("/Settings", model =>
    {
        model.Filters.Add(new RequireLoginFilter());
    });
});

/* ============================================================================
   ENTITY FRAMEWORK CORE + SQL SERVER
   ============================================================================ */
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

/* ============================================================================
   IGDB / TWITCH
   ============================================================================ */
builder.Services.Configure<IgdbOptions>(builder.Configuration.GetSection("IGDB"));

builder.Services.AddHttpClient("TwitchAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<IgdbTokenProvider>();

builder.Services.AddHttpClient<IgdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IgdbApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<IGameDiscoveryService, GameDiscoveryService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<SocialGraphService>();
builder.Services.AddScoped<SocialListService>();

/* ============================================================================
   CACHE + SESSION
   ============================================================================ */
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(1);
});

/* ============================================================================
   CUSTOM FILTERS
   ============================================================================ */
builder.Services.AddScoped<RequireLoginFilter>();

var app = builder.Build();

/* ============================================================================
   HTTP PIPELINE
   ============================================================================ */
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCookiePolicy();
app.UseSession();
app.UseMiddleware<UserPresenceMiddleware>();
app.MapRazorPages();

app.Run();
