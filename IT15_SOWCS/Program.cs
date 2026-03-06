using IT15_SOWCS.Data;
using IT15_SOWCS.Filters;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<UserActionAuditFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<UserActionAuditFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<NotificationService>();

// Identity setup 
builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    options.User.RequireUniqueEmail = true;

    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["GoogleKeys:ClientId"];
        options.ClientSecret = builder.Configuration["GoogleKeys:ClientSecret"];
        options.CallbackPath = "/signin-google";

    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();

    dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[NotificationItem]', N'U') IS NULL
BEGIN
    CREATE TABLE [NotificationItem] (
        [notification_id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [recipient_email] NVARCHAR(450) NOT NULL,
        [title] NVARCHAR(120) NOT NULL,
        [message] NVARCHAR(500) NOT NULL,
        [action_url] NVARCHAR(255) NULL,
        [category] NVARCHAR(40) NOT NULL,
        [is_read] BIT NOT NULL DEFAULT(0),
        [created_at] DATETIME2 NOT NULL
    );
    CREATE INDEX [IX_NotificationItem_recipient_email_is_read_created_at]
    ON [NotificationItem]([recipient_email], [is_read], [created_at]);
END");

    var superAdminUser = await userManager.FindByEmailAsync("yuzkiega@gmail.com");
    if (superAdminUser != null && !string.Equals(superAdminUser.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
    {
        superAdminUser.Role = "superadmin";
        superAdminUser.UpdatedDate = DateTime.UtcNow;
        await userManager.UpdateAsync(superAdminUser);
    }
}

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
