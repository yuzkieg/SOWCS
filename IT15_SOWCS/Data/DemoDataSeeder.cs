using IT15_SOWCS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IT15_SOWCS.Data
{
    public static class DemoDataSeeder
    {
        private const string DefaultPassword = "Syncora123";
        private const string SuperAdminEmail = "yuzkiega@gmail.com";

        private static readonly SeedProfile[] ProjectManagers =
        {
            new("Carlos Vega", "carlos.vega@syncora.com", "Project Management", "Project Manager", "Project Manager", true),
            new("Lia Santos", "lia.santos@syncora.com", "Project Management", "Project Manager", "Project Manager", true),
            new("Nina Reynolds", "nina.reynolds@syncora.com", "Project Management", "Project Manager", "Project Manager", true)
        };

        private static readonly SeedProfile[] HrManagers =
        {
            new("Amara Castillo", "amara.castillo@syncora.com", "Human Resources", "HR Manager", "HR Officer", true),
            new("Rafael Mendoza", "rafael.mendoza@syncora.com", "Human Resources", "HR Manager", "HR Officer", true)
        };

        private static readonly SeedProfile[] Employees =
        {
            new("Will Doe", "will.doe@syncora.com", "Production", "Employee", "Team Member", false),
            new("Jasmine Cruz", "jasmine.cruz@syncora.com", "Production", "Employee", "Team Member", false),
            new("Ethan Park", "ethan.park@syncora.com", "Production", "Employee", "Team Member", false),
            new("Sofia Reyes", "sofia.reyes@syncora.com", "Production", "Employee", "Team Member", true),
            new("Marco Lim", "marco.lim@syncora.com", "Production", "Employee", "Team Member", true),
            new("Avery Cole", "avery.cole@syncora.com", "Production", "Employee", "Team Member", true),
            new("Tessa Yu", "tessa.yu@syncora.com", "Production", "Employee", "Team Member", true),
            new("Jude Navarro", "jude.navarro@syncora.com", "Production", "Employee", "Team Member", true)
        };

        private static readonly string[] ProjectNames =
        {
            "Client Onboarding Portal",
            "HR Self-Service Revamp",
            "Document Workflow Upgrade",
            "Compliance Tracker",
            "Leave Management Refresh",
            "Performance Pulse",
            "Inventory Sync",
            "Customer Success Dashboard",
            "Mobile Field Toolkit",
            "Analytics Command Center"
        };

        private static readonly string[] TaskTitles =
        {
            "Design wireframes", "Implement API endpoints", "QA verification",
            "Finalize requirements", "Set up dashboards", "Conduct user training",
            "Deploy to staging", "Review security checklist", "Prepare release notes",
            "Run performance test"
        };

        private static readonly string[] DocumentTitles =
        {
            "Employee Handbook", "Expense Policy Update", "Client Contract Draft",
            "Operations Checklist", "Monthly Performance Summary", "Onboarding Kit",
            "Risk Assessment", "Leave Policy FAQ", "Project Plan", "Meeting Minutes",
            "Training Guide", "Security SOP", "Budget Proposal", "Audit Report",
            "Release Overview"
        };

        public static async Task SeedAsync(AppDbContext dbContext, UserManager<Users> userManager)
        {
            var rng = new Random(2026);

            await RemoveLegacySeedAsync(dbContext, userManager);

            var seedProfiles = ProjectManagers.Concat(HrManagers).Concat(Employees).ToArray();
            var seedEmails = seedProfiles.Select(profile => profile.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingUsers = await userManager.Users
                .Where(user => user.Email != null)
                .ToDictionaryAsync(user => user.Email!, StringComparer.OrdinalIgnoreCase);

            var employees = await dbContext.Employees.Include(employee => employee.User).ToListAsync();

            async Task<Users> EnsureUserAsync(SeedProfile profile)
            {
                if (existingUsers.TryGetValue(profile.Email, out var existing))
                {
                    if (string.IsNullOrWhiteSpace(existing.FullName))
                    {
                        existing.FullName = profile.FullName;
                    }
                    existing.Role = "user";
                    existing.UpdatedDate = DateTime.UtcNow;

                    if (!profile.IsActive)
                    {
                        existing.LockoutEnabled = true;
                        existing.LockoutEnd = DateTimeOffset.UtcNow.AddYears(5);
                    }
                    else
                    {
                        existing.LockoutEnd = null;
                    }

                    await userManager.UpdateAsync(existing);
                    return existing;
                }

                var user = new Users
                {
                    UserName = profile.Email,
                    Email = profile.Email,
                    FullName = profile.FullName,
                    Role = "user",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, DefaultPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create seed user {profile.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                existingUsers[profile.Email] = user;
                return user;
            }

            foreach (var profile in seedProfiles)
            {
                var user = await EnsureUserAsync(profile);
                var employee = employees.FirstOrDefault(item => item.user_id == user.Id);
                if (employee == null)
                {
                    employee = new Employee
                    {
                        user_id = user.Id,
                        full_name = profile.FullName,
                        department = profile.Department,
                        employee_role = profile.Role,
                        position = profile.Position,
                        contact_number = RandomPhone(rng),
                        hire_date = DateTime.Today.AddDays(-rng.Next(30, 400)),
                        annual_leave_balance = 15,
                        sick_leave_balance = 10,
                        personal_leave_balance = 5,
                        is_active = profile.IsActive
                    };
                    dbContext.Employees.Add(employee);
                    employees.Add(employee);
                }
                else
                {
                    employee.full_name = profile.FullName;
                    employee.department = profile.Department;
                    employee.employee_role = profile.Role;
                    employee.position = profile.Position;
                    employee.is_active = profile.IsActive;
                }
            }

            await dbContext.SaveChangesAsync();

            employees = await dbContext.Employees.Include(employee => employee.User).ToListAsync();

            var pmEmployees = employees
                .Where(emp => NormalizeRole(emp.employee_role) == "project manager" || NormalizeRole(emp.employee_role) == "manager")
                .ToList();
            var regularEmployees = employees
                .Where(emp => NormalizeRole(emp.employee_role) == "employee")
                .ToList();

            var projectsExistingCount = await dbContext.Projects.CountAsync();
            var projectsToAdd = Math.Max(0, 10 - projectsExistingCount);

            var newProjects = new List<Projects>();
            for (var i = 0; i < projectsToAdd; i++)
            {
                var manager = pmEmployees.Count == 0 ? null : pmEmployees[rng.Next(pmEmployees.Count)];
                var managerEmail = manager?.User?.Email ?? SuperAdminEmail;
                var managerName = manager?.full_name ?? "Project Manager";

                var teamMembers = new List<string>();
                if (regularEmployees.Count > 0)
                {
                    var maxTake = Math.Min(5, regularEmployees.Count);
                    var takeCount = regularEmployees.Count < 2 ? regularEmployees.Count : rng.Next(2, maxTake + 1);
                    teamMembers = regularEmployees
                        .OrderBy(_ => rng.Next())
                        .Take(takeCount)
                        .Select(emp => emp.full_name)
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(managerName) && !teamMembers.Contains(managerName))
                {
                    teamMembers.Insert(0, managerName);
                }

                var startDate = DateTime.Today.AddDays(-rng.Next(10, 80));
                var dueDate = startDate.AddDays(rng.Next(40, 140));

                var projectName = ProjectNames.Length > i
                    ? ProjectNames[i]
                    : $"Operations Initiative {projectsExistingCount + i + 1}";

                var project = new Projects
                {
                    name = projectName,
                    description = "Project seeded for functional validation.",
                    manager_email = managerEmail,
                    manager_name = managerName,
                    status = RandomPick(rng, "Active", "On Hold", "Completed"),
                    priority = RandomPick(rng, "low", "medium", "high", "urgent"),
                    start_date = startDate,
                    due_date = dueDate,
                    team_members = string.Join(", ", teamMembers),
                    progress = 0
                };

                newProjects.Add(project);
                dbContext.Projects.Add(project);
            }

            await dbContext.SaveChangesAsync();

            var taskStatuses = new[] { "To Do", "In Progress", "Review", "Completed" };
            var newTasks = new List<WorkTask>();

            foreach (var project in newProjects)
            {
                var taskCount = rng.Next(5, 11);
                for (var i = 0; i < taskCount; i++)
                {
                    var employee = regularEmployees.Count == 0 ? employees.FirstOrDefault() : regularEmployees[rng.Next(regularEmployees.Count)];
                    if (employee == null)
                    {
                        continue;
                    }

                    var status = taskStatuses[rng.Next(taskStatuses.Length)];
                    var dueDate = project.start_date.AddDays(rng.Next(7, 120));
                    var completedDate = status == "Completed" ? dueDate.AddDays(-rng.Next(0, 12)) : (DateTime?)null;
                    var title = TaskTitles[rng.Next(TaskTitles.Length)];

                    newTasks.Add(new WorkTask
                    {
                        employee_id = employee.employee_id,
                        project_id = project.project_id,
                        title = $"{title} - {project.name}",
                        description = "Task seeded for workflow validation.",
                        project_name = project.name,
                        assigned_to = employee.User?.Email ?? SuperAdminEmail,
                        assigned_name = employee.full_name,
                        status = status,
                        priority = RandomPick(rng, "low", "medium", "high", "urgent"),
                        due_date = dueDate,
                        completed_date = completedDate
                    });
                }
            }

            if (newTasks.Count > 0)
            {
                dbContext.Tasks.AddRange(newTasks);
                await dbContext.SaveChangesAsync();
            }

            foreach (var project in newProjects)
            {
                var projectTasks = newTasks.Where(task => task.project_id == project.project_id).ToList();
                var total = projectTasks.Count;
                var completed = projectTasks.Count(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase));
                project.progress = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total);
            }

            await dbContext.SaveChangesAsync();

            var documentsExistingCount = await dbContext.Documents.CountAsync();
            var docsToAdd = Math.Max(0, 15 - documentsExistingCount);
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadDir);

            var documentStatuses = new[] { "Pending", "Approved", "Rejected" };
            for (var i = 0; i < docsToAdd; i++)
            {
                var uploader = employees.Count == 0 ? null : employees[rng.Next(employees.Count)];
                var status = documentStatuses[rng.Next(documentStatuses.Length)];
                var fileName = $"document-{Guid.NewGuid():N}.pdf";
                var filePath = Path.Combine(uploadDir, fileName);
                if (!File.Exists(filePath))
                {
                    await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());
                }

                var title = DocumentTitles.Length > i ? DocumentTitles[i] : $"Operational File {documentsExistingCount + i + 1}";

                var record = new DocumentRecord
                {
                    title = title,
                    file_name = fileName,
                    file_path = $"/uploads/{fileName}",
                    category = RandomPick(rng, "Policy", "Report", "Form", "Other"),
                    status = status,
                    file_size_bytes = 0,
                    uploaded_date = DateTime.UtcNow.AddDays(-rng.Next(1, 60)),
                    uploaded_by_email = uploader?.User?.Email,
                    reviewed_by = status == "Pending" ? null : SuperAdminEmail,
                    reviewed_date = status == "Pending" ? null : DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
                    review_notes = status == "Rejected" ? "Requires revision." : null
                };

                dbContext.Documents.Add(record);
            }

            await dbContext.SaveChangesAsync();

            var leaveTypes = new[] { "Annual Leave", "Sick Leave", "Personal Leave" };
            var leaveStatuses = new[] { "Pending", "Approved", "Rejected" };
            var leaveRequests = new List<LeaveRequest>();

            foreach (var employee in employees.Where(emp => emp.User != null && seedEmails.Contains(emp.User.Email ?? string.Empty)))
            {
                var existingForEmployee = await dbContext.LeaveRequests.AnyAsync(request =>
                    request.employee_email == employee.User!.Email);
                if (existingForEmployee)
                {
                    continue;
                }

                var requestCount = rng.Next(1, 3);
                for (var i = 0; i < requestCount; i++)
                {
                    var status = leaveStatuses[rng.Next(leaveStatuses.Length)];
                    var leaveType = leaveTypes[rng.Next(leaveTypes.Length)];
                    var startDate = DateTime.Today.AddDays(-rng.Next(10, 60));
                    var days = rng.Next(1, 4);
                    var endDate = startDate.AddDays(days);

                    leaveRequests.Add(new LeaveRequest
                    {
                        employee_email = employee.User!.Email ?? string.Empty,
                        employee_name = employee.full_name,
                        leave_type = leaveType,
                        start_date = startDate,
                        end_date = endDate,
                        days_count = days,
                        reason = "Personal request",
                        status = status,
                        reviewed_by = status == "Pending" ? null : SuperAdminEmail,
                        reviewed_date = status == "Pending" ? null : DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
                        review_notes = status == "Rejected" ? "Insufficient balance." : null
                    });
                }
            }

            if (leaveRequests.Count > 0)
            {
                dbContext.LeaveRequests.AddRange(leaveRequests);
                await dbContext.SaveChangesAsync();
            }

            foreach (var employee in employees.Where(emp => emp.User != null && seedEmails.Contains(emp.User.Email ?? string.Empty)))
            {
                var approvedLeaves = await dbContext.LeaveRequests
                    .Where(request => request.employee_email == employee.User!.Email && request.status == "Approved")
                    .ToListAsync();

                var annualUsed = approvedLeaves.Where(item => item.leave_type == "Annual Leave").Sum(item => item.days_count);
                var sickUsed = approvedLeaves.Where(item => item.leave_type == "Sick Leave").Sum(item => item.days_count);
                var personalUsed = approvedLeaves.Where(item => item.leave_type == "Personal Leave").Sum(item => item.days_count);

                employee.annual_leave_balance = Math.Max(0, 15 - annualUsed);
                employee.sick_leave_balance = Math.Max(0, 10 - sickUsed);
                employee.personal_leave_balance = Math.Max(0, 5 - personalUsed);
            }

            await dbContext.SaveChangesAsync();
        }

        private static async Task RemoveLegacySeedAsync(AppDbContext dbContext, UserManager<Users> userManager)
        {
            var legacyUsers = await userManager.Users
                .Where(user =>
                    (user.Email != null && user.Email.EndsWith("@syncora.demo")) ||
                    (!string.IsNullOrWhiteSpace(user.FullName) && user.FullName.StartsWith("Demo ")))
                .ToListAsync();

            if (legacyUsers.Count == 0)
            {
                return;
            }

            var legacyEmails = legacyUsers
                .Select(user => user.Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var legacyEmployeeIds = await dbContext.Employees
                .Where(emp => legacyUsers.Select(user => user.Id).Contains(emp.user_id))
                .Select(emp => emp.employee_id)
                .ToListAsync();

            var legacyProjectIds = await dbContext.Projects
                .Where(project => legacyEmails.Contains(project.manager_email))
                .Select(project => project.project_id)
                .ToListAsync();

            var tasks = await dbContext.Tasks
                .Where(task =>
                    legacyEmployeeIds.Contains(task.employee_id) ||
                    legacyProjectIds.Contains(task.project_id) ||
                    legacyEmails.Contains(task.assigned_to))
                .ToListAsync();
            dbContext.Tasks.RemoveRange(tasks);

            var documents = await dbContext.Documents
                .Where(doc =>
                    (doc.uploaded_by_email != null && legacyEmails.Contains(doc.uploaded_by_email)) ||
                    EF.Functions.Like(doc.file_name, "demo-doc-%"))
                .ToListAsync();
            dbContext.Documents.RemoveRange(documents);

            var leaves = await dbContext.LeaveRequests
                .Where(leave => legacyEmails.Contains(leave.employee_email))
                .ToListAsync();
            dbContext.LeaveRequests.RemoveRange(leaves);

            var projects = await dbContext.Projects
                .Where(project => legacyEmails.Contains(project.manager_email))
                .ToListAsync();
            dbContext.Projects.RemoveRange(projects);

            var employees = await dbContext.Employees
                .Where(emp => legacyEmployeeIds.Contains(emp.employee_id))
                .ToListAsync();
            dbContext.Employees.RemoveRange(employees);

            await dbContext.SaveChangesAsync();

            foreach (var legacy in legacyUsers)
            {
                await userManager.DeleteAsync(legacy);
            }

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (Directory.Exists(uploadsDir))
            {
                foreach (var file in Directory.GetFiles(uploadsDir, "demo-doc-*.pdf"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }

        private static string NormalizeRole(string? role)
        {
            return (role ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string RandomPick(Random rng, params string[] values)
        {
            return values[rng.Next(values.Length)];
        }

        private static string RandomPhone(Random rng)
        {
            return $"09{rng.Next(100000000, 999999999)}";
        }

        private sealed record SeedProfile(
            string FullName,
            string Email,
            string Department,
            string Role,
            string Position,
            bool IsActive);
    }
}
