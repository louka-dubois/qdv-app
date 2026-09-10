using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QDVapp.Data;
using QDVapp.Models;
using QDVapp.Services;
using System.Security.Claims;
using System.Text.Json;

namespace QDVapp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                               throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.AllowedUserNameCharacters += "é ";
        })
            .AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddScoped<IEmailSender<ApplicationUser>, SmtpEmailSender>();
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder.Services.AddRazorPages();
        builder.Services.AddScoped<ExcelUploadService>();
        builder.Services.AddScoped<CorrectionService>();
        builder.Services.AddScoped<CorrectedExcelExportService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapPost("/api/theme", async (HttpContext context, ApplicationDbContext db) =>
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<ThemePayload>(body);
            if (payload is null)
                return Results.BadRequest();

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            user.Theme = payload.Bright;
            await db.SaveChangesAsync();
            return Results.Ok();
        });
        app.Run();
    }

    private sealed class ThemePayload
    {
        public bool Bright { get; set; }
    }
}
