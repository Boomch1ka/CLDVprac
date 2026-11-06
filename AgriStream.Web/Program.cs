using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- MVC + global anti-forgery for all form posts ----------
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// ---------- Monitoring (uses APPLICATIONINSIGHTS_CONNECTION_STRING when set) ----------
builder.Services.AddApplicationInsightsTelemetry();

// ---------- Azure SQL Database (users, roles, device registry) ----------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AgriStreamDb"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// ---------- Azure Storage (Blob + Table) ----------
var storageConnection = builder.Configuration.GetConnectionString("AzureStorage");
if (string.IsNullOrWhiteSpace(storageConnection))
{
    storageConnection = "UseDevelopmentStorage=true"; // Azurite for local development
}
builder.Services.AddSingleton(new BlobServiceClient(storageConnection));
builder.Services.AddSingleton(new TableServiceClient(storageConnection));
builder.Services.AddSingleton<BlobMediaService>();
builder.Services.AddSingleton<TelemetryService>();
builder.Services.AddSingleton<AuditService>();

// ---------- Azure Functions client (HTTP GET + HTTP POST functions) ----------
builder.Services.AddHttpClient<FunctionsClient>(client => client.Timeout = TimeSpan.FromSeconds(30));

// ---------- Authentication (stateless cookie auth - safe for scale-out) ----------
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Create database schema, seed roles/admin/devices and create storage containers/tables.
await StartupInitializer.InitializeAsync(app.Services);

// Retry SQL initialisation automatically once the Azure SQL firewall is opened.
app.Use(async (ctx, next) =>
{
    if (!StartupInitializer.IsSqlReady)
    {
        await StartupInitializer.TryEnsureSqlAsync(ctx.RequestServices);
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
