using IT15_SOWCS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Projects> Projects => Set<Projects>();
        public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
        public DbSet<WorkTask> Tasks => Set<WorkTask>();
        public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
        public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
        public DbSet<ArchiveItem> ArchiveItems => Set<ArchiveItem>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Users>()
                .HasIndex(user => user.Email)
                .IsUnique()
                .HasFilter("[Email] IS NOT NULL");

            builder.Entity<Employee>().ToTable("Employee");
            builder.Entity<Projects>().ToTable("Project");
            builder.Entity<LeaveRequest>().ToTable("LeaveRequest");
            builder.Entity<WorkTask>().ToTable("Task");
            builder.Entity<DocumentRecord>().ToTable("Document");
            builder.Entity<AuditLogEntry>().ToTable("AuditLog");
            builder.Entity<ArchiveItem>().ToTable("ArchiveItem");

            builder.Entity<Employee>()
                .Property(employee => employee.annual_leave_balance)
                .HasPrecision(18, 2);

            builder.Entity<Employee>()
                .Property(employee => employee.sick_leave_balance)
                .HasPrecision(18, 2);

            builder.Entity<Employee>()
                .Property(employee => employee.personal_leave_balance)
                .HasPrecision(18, 2);

            builder.Entity<Employee>()
                .HasOne(employee => employee.User)
                .WithMany(user => user.Employees)
                .HasForeignKey(employee => employee.user_id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Employee>()
                .HasOne(employee => employee.ManagerUser)
                .WithMany(user => user.ManagedEmployees)
                .HasForeignKey(employee => employee.manager_email)
                .HasPrincipalKey(user => user.Email)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Projects>()
                .HasOne(project => project.ManagerUser)
                .WithMany(user => user.ManagedProjects)
                .HasForeignKey(project => project.manager_email)
                .HasPrincipalKey(user => user.Email)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LeaveRequest>()
                .HasOne(request => request.EmployeeUser)
                .WithMany(user => user.LeaveRequests)
                .HasForeignKey(request => request.employee_email)
                .HasPrincipalKey(user => user.Email)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<WorkTask>()
                .HasOne(task => task.Employee)
                .WithMany(employee => employee.Tasks)
                .HasForeignKey(task => task.employee_id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<WorkTask>()
                .HasOne(task => task.Project)
                .WithMany(project => project.Tasks)
                .HasForeignKey(task => task.project_id)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
