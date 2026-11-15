using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Components;
using FinanceTracker.Components.Account;
using FinanceTracker.Data;

System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "Data"));
var identityConnection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
if (!Path.IsPathRooted(identityConnection.DataSource))
    identityConnection.DataSource = Path.Combine(builder.Environment.ContentRootPath, identityConnection.DataSource);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(identityConnection.ToString()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddDbContextFactory<FinanceDb>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("FinanceConnection") ??
    new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(builder.Environment.ContentRootPath, "finance.db"),
        ForeignKeys = true
    }.ToString()));
builder.Services.AddScoped<Ledger>();
builder.Services.AddScoped<AccountDeletion>();
builder.Services.AddScoped<FinanceTracker.Domain.Demo>();
builder.Services.AddScoped<FinanceTracker.Domain.MonthState>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await identity.Database.MigrateAsync();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FinanceDb>>();
    await using var ledger = await factory.CreateDbContextAsync();
    await ledger.Database.EnsureCreatedAsync();
    // Both files use rollback journals for atomic account/finance deletion.
    await identity.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
    await ledger.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapFinanceExport();

app.Run();



public partial class Program { }
