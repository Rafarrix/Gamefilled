using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ Razor Pages + Proteção por pasta
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddFolderApplicationModelConvention("/Settings", model =>
    {
        model.Filters.Add(new RequireLoginFilter());
    });
});

// ✅ EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===============================
// ✅ IGDB: Options + Token + Client
// ===============================

// Lê "IGDB" do appsettings.json
builder.Services.Configure<IgdbOptions>(builder.Configuration.GetSection("IGDB"));

// HttpClient para token Twitch
builder.Services.AddHttpClient<IgdbTokenProvider>();

// HttpClient para IGDB (base address v4)
builder.Services.AddHttpClient<IgdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ✅ Sessão
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

// (mantém se já tinhas)
builder.Services.AddScoped<RequireLoginFilter>();

var app = builder.Build();

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

app.MapRazorPages();

app.Run();
