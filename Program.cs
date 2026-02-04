using Gamefilled.Data;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ Razor Pages
builder.Services.AddRazorPages();

// ✅ EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Sessão: precisa de cache + AddSession
builder.Services.AddDistributedMemoryCache();

// ✅ Cookie Policy (ajuda a controlar SameSite / consent, etc.)
// Nota: não é obrigatório, mas é bom para "arrumar" regras de cookies.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // Lax costuma ser o equilíbrio certo (não quebra navegação normal e ajuda contra CSRF)
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddSession(options =>
{
    // ✅ Segurança básica
    options.Cookie.HttpOnly = true;     // cookie não acessível por JS
    options.Cookie.IsEssential = true;  // essencial (útil em dev)

    // ✅ Ajuda contra CSRF
    options.Cookie.SameSite = SameSiteMode.Lax;

    // ✅ HTTPS:
    // - Em produção querias Always
    // - Em desenvolvimento, SameAsRequest evita problemas se estiveres em http
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    // ✅ Expiração da sessão
    options.IdleTimeout = TimeSpan.FromHours(1);
});

var app = builder.Build();

// ✅ Erros / HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ Aplica política de cookies (antes da sessão)
app.UseCookiePolicy();

// ✅ Sessão tem de vir antes de MapRazorPages
app.UseSession();

app.MapRazorPages();
app.Run();