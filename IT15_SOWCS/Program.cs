using IT15_SOWCS.Data;
using IT15_SOWCS.Filters;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var documentEncryptionKey = builder.Configuration["Security:DocumentEncryptionKey"];
if (string.IsNullOrWhiteSpace(documentEncryptionKey))
{
    if (builder.Environment.IsDevelopment())
    {
        documentEncryptionKey = "syncora-dev-document-key-change-before-production";
    }
    else
    {
        throw new InvalidOperationException("Missing configuration for Security:DocumentEncryptionKey.");
    }
}

DocumentFieldEncryption.Configure(documentEncryptionKey);

builder.Services.AddScoped<UserActionAuditFilter>();
builder.Services.AddScoped<ModuleAccessFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<UserActionAuditFilter>();
    options.Filters.AddService<ModuleAccessFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<LeaveBalanceService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<ApprovalPredictionService>();
builder.Services.AddMemoryCache();

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

    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["GoogleKeys:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["GoogleKeys:ClientSecret"] ?? string.Empty;
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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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

    dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[PendingInvitation]', N'U') IS NULL
BEGIN
    CREATE TABLE [PendingInvitation] (
        [invitation_id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [email] NVARCHAR(450) NOT NULL,
        [role] NVARCHAR(40) NOT NULL,
        [token] NVARCHAR(120) NOT NULL,
        [invited_by_email] NVARCHAR(450) NOT NULL,
        [created_at] DATETIME2 NOT NULL,
        [expires_at] DATETIME2 NOT NULL,
        [accepted_at] DATETIME2 NULL
    );
    CREATE UNIQUE INDEX [IX_PendingInvitation_token] ON [PendingInvitation]([token]);
    CREATE INDEX [IX_PendingInvitation_email_accepted_at] ON [PendingInvitation]([email], [accepted_at]);
END");

    dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[PredictionAction]', N'U') IS NULL
BEGIN
    CREATE TABLE [PredictionAction] (
        [action_id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [employee_id] INT NOT NULL,
        [employee_name] NVARCHAR(200) NOT NULL,
        [prediction_label] NVARCHAR(50) NOT NULL,
        [action_type] NVARCHAR(50) NOT NULL,
        [action_notes] NVARCHAR(300) NULL,
        [created_by] NVARCHAR(450) NULL,
        [created_at] DATETIME2 NOT NULL,
        [period_type] NVARCHAR(20) NOT NULL,
        [period_start] DATETIME2 NOT NULL,
        [period_end] DATETIME2 NOT NULL
    );
    CREATE INDEX [IX_PredictionAction_employee_id_created_at]
    ON [PredictionAction]([employee_id], [created_at]);
END");

    dbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('AuditLog', 'severity') IS NULL
BEGIN
    ALTER TABLE [AuditLog] ADD [severity] NVARCHAR(20) NOT NULL CONSTRAINT DF_AuditLog_severity DEFAULT('Informational');
END");

    dbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('AuditLog', 'ip_address') IS NULL
BEGIN
    ALTER TABLE [AuditLog] ADD [ip_address] NVARCHAR(45) NOT NULL CONSTRAINT DF_AuditLog_ip_address DEFAULT('');
END");

    dbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('AuditLog', 'audit_type') IS NULL
BEGIN
    ALTER TABLE [AuditLog] ADD [audit_type] NVARCHAR(20) NOT NULL CONSTRAINT DF_AuditLog_audit_type DEFAULT('System');
END");

    dbContext.Database.ExecuteSqlRaw(@"
UPDATE [AuditLog]
SET [audit_type] = 'Security'
WHERE LOWER(ISNULL([entity], '')) = 'account'
   OR LOWER(ISNULL([action], '')) IN ('login', 'logout', 'login_failed')
   OR LOWER(ISNULL([description], '')) LIKE '%google%'
   OR LOWER(ISNULL([description], '')) LIKE '%failed login%'
   OR LOWER(ISNULL([description], '')) LIKE '%logged in%'
   OR LOWER(ISNULL([description], '')) LIKE '%logged out%'");

    var superAdminUser = await userManager.FindByEmailAsync("yuzkiega@gmail.com");
    if (superAdminUser != null && !string.Equals(superAdminUser.Role, "superadmin", StringComparison.OrdinalIgnoreCase))
    {
        superAdminUser.Role = "superadmin";
        superAdminUser.UpdatedDate = DateTime.UtcNow;
        await userManager.UpdateAsync(superAdminUser);
    }

    await DemoDataSeeder.SeedAsync(dbContext, userManager);
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
