using Gamefilled.Application.Games;
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

// Cliente usado apenas para obter o token OAuth da Twitch.
builder.Services.AddHttpClient("TwitchAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Singleton para o token ser reutilizado entre pedidos e serviços.
builder.Services.AddSingleton<IgdbTokenProvider>();

// Cliente de domínio já usado pelas páginas atuais.
builder.Services.AddHttpClient<IgdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Cliente de baixo nível usado pelo novo motor de descoberta V2.
builder.Services.AddHttpClient<IgdbApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<IGameDiscoveryService, GameDiscoveryService>();

/* ============================================================================
   CACHE + SESSÃO
   ============================================================================ */
// Cache de metadados IGDB, como plataformas e géneros.
builder.Services.AddMemoryCache();

// Cache distribuído local usado pela sessão.
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
   FILTROS CUSTOM
   ============================================================================ */
builder.Services.AddScoped<RequireLoginFilter>();

var app = builder.Build();

/* ============================================================================
   PIPELINE HTTP
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
app.MapRazorPages();

app.Run();
