using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

/* ============================================================================
   RAZOR PAGES
   ----------------------------------------------------------------------------
   Regista Razor Pages e aplica proteção automática à pasta /Settings
   através do filtro RequireLoginFilter.
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
   ----------------------------------------------------------------------------
   Regista o AppDbContext para acesso à base de dados SQL Server.
   A connection string é lida de appsettings.json.
   ============================================================================ */
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

/* ============================================================================
   IGDB / TWITCH
   ----------------------------------------------------------------------------
   Regista:
   - opções da IGDB (ClientId / ClientSecret)
   - provider de token Twitch
   - cliente HTTP principal da IGDB
   ============================================================================ */

// Liga a secção "IGDB" do appsettings à classe IgdbOptions
builder.Services.Configure<IgdbOptions>(builder.Configuration.GetSection("IGDB"));

// HttpClient para obter token OAuth do Twitch
builder.Services.AddHttpClient<IgdbTokenProvider>();

// HttpClient tipado para a API IGDB
builder.Services.AddHttpClient<IgdbClient>(client =>
{
    client.BaseAddress = new Uri("https://api.igdb.com/v4/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

/* ============================================================================
   SESSÃO
   ----------------------------------------------------------------------------
   Usa memória distribuída local e cookies de sessão.
   ============================================================================ */
builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddSession(options =>
{
    // Cookie da sessão não é acessível por JavaScript
    options.Cookie.HttpOnly = true;

    // Necessário para a app funcionar mesmo sem consentimento explícito
    options.Cookie.IsEssential = true;

    // Proteção CSRF / navegação normal
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Só obriga HTTPS quando o pedido já for HTTPS
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    // Duração máxima de inatividade da sessão
    options.IdleTimeout = TimeSpan.FromHours(1);
});

/* ============================================================================
   FILTROS CUSTOM
   ----------------------------------------------------------------------------
   Regista o filtro RequireLoginFilter no DI container.
   ============================================================================ */
builder.Services.AddScoped<RequireLoginFilter>();

var app = builder.Build();

/* ============================================================================
   PIPELINE HTTP
   ============================================================================ */

// Tratamento de erros em produção
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Força HTTPS
app.UseHttpsRedirection();

// Permite servir ficheiros estáticos (css, js, imgs, lib, etc.)
app.UseStaticFiles();

// Routing base
app.UseRouting();

// Política de cookies
app.UseCookiePolicy();

// Sessão (tem de vir antes de usar páginas que dependem da sessão)
app.UseSession();

// Mapeia Razor Pages
app.MapRazorPages();

app.Run();