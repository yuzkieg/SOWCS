using IT15_SOWCS.Data;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IT15_SOWCS.Controllers
{
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService;

        public ReportsController(AppDbContext context, IWebHostEnvironment environment, NotificationService notificationService)
        {
            _context = context;
            _environment = environment;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Reports(string? tab, DateTime? from, DateTime? to)
        {
            var model = await BuildModelAsync(tab, from, to, previewOnly: true);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? tab, DateTime? from, DateTime? to, bool all = false)
        {
            var model = await BuildModelAsync(tab, from, to, previewOnly: false);
            var generatedByEmail = User.Identity?.Name ?? "system@local";
            var generatedByUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Email == generatedByEmail);
            var generatedByEmployeeName = generatedByUser == null
                ? null
                : await _context.Employees
                    .AsNoTracking()
                    .Where(employee => employee.user_id == generatedByUser.Id)
                    .Select(employee => employee.full_name)
                    .FirstOrDefaultAsync();
            var generatedByName = !string.IsNullOrWhiteSpace(generatedByEmployeeName)
                ? generatedByEmployeeName
                : generatedByUser?.FullName;
            var generatedBy = string.IsNullOrWhiteSpace(generatedByName)
                ? generatedByEmail
                : $"{generatedByName} ({generatedByEmail})";
            var generatedAt = DateTime.Now;
            var logoBytes = GetLogoBytes();

            QuestPDF.Settings.License = LicenseType.Community;
            var pdfBytes = GeneratePdf(model, all, generatedBy, generatedAt, logoBytes);

            var fileNamePrefix = all ? "All-Reports" : $"{NormalizeTab(tab)}-Report";
            var fileName = $"{fileNamePrefix}-{generatedAt:yyyyMMdd-HHmmss}.pdf";

            if (generatedByUser != null)
            {
                var employeeRole = await _context.Employees
                    .AsNoTracking()
                    .Where(employee => employee.user_id == generatedByUser.Id)
                    .Select(employee => employee.employee_role)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(employeeRole) &&
                    (string.Equals(employeeRole, "project manager", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(employeeRole, "manager", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(employeeRole, "hr manager", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(employeeRole, "hr", StringComparison.OrdinalIgnoreCase)))
                {
                    await _notificationService.AddForRoleGroupAsync(
                        "superadmin",
                        "Report Exported",
                        $"{generatedBy} exported reports ({(all ? "All Tabs" : NormalizeTab(tab))}).",
                        "Reports",
                        "/Reports/Reports");
                }
            }

            return File(pdfBytes, "application/pdf", fileName);
        }

        private byte[]? GetLogoBytes()
        {
            var logoPath = Path.Combine(_environment.WebRootPath, "images", "SOWCS.png");
            if (!System.IO.File.Exists(logoPath))
            {
                return null;
            }

            return System.IO.File.ReadAllBytes(logoPath);
        }

        private static byte[] GeneratePdf(ReportsPageViewModel model, bool exportAll, string generatedBy, DateTime generatedAt, byte[]? logoBytes)
        {
            var sections = exportAll
                ? new[] { "projects", "tasks", "employees", "leave" }
                : new[] { model.ActiveTab };

            return Document.Create(document =>
            {
                foreach (var section in sections)
                {
                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);
                        page.DefaultTextStyle(text => text.FontSize(10));

                        page.Header().Element(header =>
                        {
                            header.Background("#1f2d49").Padding(12).Row(row =>
                            {
                                row.RelativeItem().Column(column =>
                                {
                                    column.Item().Text("Syncora").FontSize(20).Bold().FontColor(Colors.White);
                                    column.Item().Text(GetSectionTitle(section)).FontSize(13).FontColor(Colors.White);
                                    column.Item().Text($"Generated: {generatedAt:MMMM d, yyyy}   By: {generatedBy}")
                                        .FontSize(9)
                                        .FontColor(Colors.White);
                                });

                                row.ConstantItem(70).AlignMiddle().AlignRight().Element(container =>
                                {
                                    if (logoBytes != null && logoBytes.Length > 0)
                                    {
                                        container.Height(54).Image(logoBytes);
                                    }
                                    else
                                    {
                                        container.Height(54).Width(54);
                                    }
                                });
                            });
                        });

                        page.Content().PaddingTop(8).Column(column =>
                        {
                            RenderSectionTable(column, section, model);
                            column.Item().PaddingTop(10);
                            RenderSectionSummary(column, section, model);
                        });
                    });
                }
            }).GeneratePdf();
        }

        private static string GetSectionTitle(string section) => section switch
        {
            "tasks" => "Tasks Summary",
            "employees" => "Employee Directory",
            "leave" => "Leave Requests",
            _ => "Projects Overview"
        };

        private static string CellValue(object? value) => value?.ToString() ?? "-";

        private static void RenderSectionTable(ColumnDescriptor column, string section, ReportsPageViewModel model)
        {
            column.Item().Text(GetSectionTitle(section)).FontSize(12).Bold().FontColor("#4f46e5");
            column.Item().PaddingTop(6).Table(table =>
            {
                switch (section)
                {
                    case "tasks":
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Title");
                            header.Cell().Element(HeaderCellStyle).Text("Project");
                            header.Cell().Element(HeaderCellStyle).Text("Status");
                            header.Cell().Element(HeaderCellStyle).Text("Priority");
                        });

                        foreach (var row in model.TaskRows)
                        {
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Title));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Project));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Status));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Priority));
                        }
                        break;

                    case "employees":
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Name");
                            header.Cell().Element(HeaderCellStyle).Text("Department");
                            header.Cell().Element(HeaderCellStyle).Text("Position");
                            header.Cell().Element(HeaderCellStyle).Text("Role");
                        });

                        foreach (var row in model.EmployeeRows)
                        {
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Name));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Department));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Position));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Role));
                        }
                        break;

                    case "leave":
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Employee");
                            header.Cell().Element(HeaderCellStyle).Text("Type");
                            header.Cell().Element(HeaderCellStyle).Text("Start Date");
                            header.Cell().Element(HeaderCellStyle).Text("End Date");
                            header.Cell().Element(HeaderCellStyle).Text("Days");
                            header.Cell().Element(HeaderCellStyle).Text("Status");
                            header.Cell().Element(HeaderCellStyle).Text("Reviewed By");
                        });

                        foreach (var row in model.LeaveRows)
                        {
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Employee));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Type));
                            table.Cell().Element(BodyCellStyle).Text(row.StartDate.ToString("MMM d, yyyy"));
                            table.Cell().Element(BodyCellStyle).Text(row.EndDate.ToString("MMM d, yyyy"));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Days));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Status));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.ReviewedBy));
                        }
                        break;

                    default:
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Project Name");
                            header.Cell().Element(HeaderCellStyle).Text("Status");
                            header.Cell().Element(HeaderCellStyle).Text("Priority");
                            header.Cell().Element(HeaderCellStyle).Text("Progress");
                        });

                        foreach (var row in model.ProjectRows)
                        {
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Name));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Status));
                            table.Cell().Element(BodyCellStyle).Text(CellValue(row.Priority));
                            table.Cell().Element(BodyCellStyle).Text($"{row.Progress}%");
                        }
                        break;
                }
            });
        }

        private static void RenderSectionSummary(ColumnDescriptor column, string section, ReportsPageViewModel model)
        {
            column.Item().Text("Summary").FontSize(12).Bold().FontColor("#4f46e5");
            column.Item().PaddingTop(6).Table(table =>
            {
                switch (section)
                {
                    case "tasks":
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Total Tasks");
                            header.Cell().Element(HeaderCellStyle).Text("Completed");
                            header.Cell().Element(HeaderCellStyle).Text("In Progress");
                            header.Cell().Element(HeaderCellStyle).Text("Pending Review");
                        });
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TotalTasks));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TasksCompleted));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TasksInProgress));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TasksPendingReview));
                        break;

                    case "employees":
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Total Employees");
                            header.Cell().Element(HeaderCellStyle).Text("Active");
                            header.Cell().Element(HeaderCellStyle).Text("Departments");
                            header.Cell().Element(HeaderCellStyle).Text("Managers");
                        });
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TotalEmployees));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.ActiveEmployees));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TotalDepartments));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TotalManagers));
                        break;

                    case "leave":
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Total Requests");
                            header.Cell().Element(HeaderCellStyle).Text("Pending");
                            header.Cell().Element(HeaderCellStyle).Text("Approved");
                            header.Cell().Element(HeaderCellStyle).Text("Rejected");
                        });
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TotalLeaveRequests));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.PendingLeaveRequests));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.ApprovedLeaveRequests));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.RejectedLeaveRequests));
                        break;

                    default:
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Total Projects");
                            header.Cell().Element(HeaderCellStyle).Text("In Progress");
                            header.Cell().Element(HeaderCellStyle).Text("Completed");
                            header.Cell().Element(HeaderCellStyle).Text("On Hold");
                        });
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.TotalProjects));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.ProjectsInProgress));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.ProjectsCompleted));
                        table.Cell().Element(BodyCellStyle).Text(CellValue(model.ProjectsOnHold));
                        break;
                }
            });
        }

        private static IContainer HeaderCellStyle(IContainer container)
        {
            return container
                .Background("#f8fafc")
                .BorderBottom(1)
                .BorderColor("#e5e7eb")
                .Padding(6)
                .DefaultTextStyle(text => text.SemiBold().FontSize(9).FontColor("#475569"));
        }

        private static IContainer BodyCellStyle(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor("#f1f5f9")
                .Padding(6)
                .DefaultTextStyle(text => text.FontSize(9).FontColor("#0f172a"));
        }

        private async Task<ReportsPageViewModel> BuildModelAsync(string? tab, DateTime? from, DateTime? to, bool previewOnly)
        {
            var activeTab = NormalizeTab(tab);
            var takeCount = previewOnly ? 8 : 200;
            if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
            {
                to = from.Value.Date;
            }

            var startDate = from?.Date;
            var endDate = to?.Date;

            var projectsQuery = _context.Projects.AsQueryable();
            if (startDate.HasValue && endDate.HasValue)
            {
                var rangeStart = startDate.Value;
                var rangeEnd = endDate.Value;
                projectsQuery = projectsQuery.Where(project =>
                    project.start_date.Date <= rangeEnd &&
                    project.due_date.Date >= rangeStart);
            }
            else if (startDate.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.due_date.Date >= startDate.Value);
            }
            else if (endDate.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.start_date.Date <= endDate.Value);
            }
            var projects = await projectsQuery
                .OrderByDescending(project => project.project_id)
                .ToListAsync();

            var projectIds = projects.Select(project => project.project_id).ToList();
            var projectProgressById = projectIds.Count == 0
                ? new Dictionary<int, int>()
                : await _context.Tasks
                    .Where(task => projectIds.Contains(task.project_id))
                    .GroupBy(task => task.project_id)
                    .Select(group => new
                    {
                        ProjectId = group.Key,
                        Total = group.Count(),
                        Completed = group.Count(task => task.status == "Completed")
                    })
                    .ToDictionaryAsync(
                        item => item.ProjectId,
                        item => item.Total == 0 ? 0 : (int)Math.Round((item.Completed * 100.0) / item.Total));

            var tasksQuery = _context.Tasks
                .Include(task => task.Project)
                .AsQueryable();
            if (startDate.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.due_date.Date >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.due_date.Date <= endDate.Value);
            }
            var tasks = await tasksQuery
                .OrderByDescending(task => task.task_id)
                .ToListAsync();

            var employeesQuery = _context.Employees.AsQueryable();
            if (startDate.HasValue)
            {
                employeesQuery = employeesQuery.Where(employee => employee.hire_date.Date >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                employeesQuery = employeesQuery.Where(employee => employee.hire_date.Date <= endDate.Value);
            }
            var employees = await employeesQuery
                .OrderBy(employee => employee.full_name)
                .ToListAsync();

            var leavesQuery = _context.LeaveRequests.AsQueryable();
            if (startDate.HasValue)
            {
                leavesQuery = leavesQuery.Where(leave => leave.start_date.Date >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                leavesQuery = leavesQuery.Where(leave => leave.start_date.Date <= endDate.Value);
            }
            var leaves = await leavesQuery
                .OrderByDescending(leave => leave.LR_id)
                .ToListAsync();

            var model = new ReportsPageViewModel
            {
                ActiveTab = activeTab,
                StartDate = startDate,
                EndDate = endDate,

                TotalProjects = projects.Count,
                ProjectsInProgress = projects.Count(project => string.Equals(project.status, "Active", StringComparison.OrdinalIgnoreCase)),
                ProjectsCompleted = projects.Count(project => string.Equals(project.status, "Completed", StringComparison.OrdinalIgnoreCase)),
                ProjectsOnHold = projects.Count(project => string.Equals(project.status, "On Hold", StringComparison.OrdinalIgnoreCase)),

                TotalTasks = tasks.Count,
                TasksCompleted = tasks.Count(task => string.Equals(task.status, "Completed", StringComparison.OrdinalIgnoreCase)),
                TasksInProgress = tasks.Count(task => string.Equals(task.status, "In Progress", StringComparison.OrdinalIgnoreCase)),
                TasksPendingReview = tasks.Count(task => string.Equals(task.status, "Review", StringComparison.OrdinalIgnoreCase)),

                TotalEmployees = employees.Count,
                ActiveEmployees = employees.Count(employee => employee.is_active),
                TotalDepartments = employees.Select(employee => employee.department).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                TotalManagers = employees.Count(employee => string.Equals(employee.employee_role, "Manager", StringComparison.OrdinalIgnoreCase)),

                TotalLeaveRequests = leaves.Count,
                PendingLeaveRequests = leaves.Count(leave => string.Equals(leave.status, "Pending", StringComparison.OrdinalIgnoreCase)),
                ApprovedLeaveRequests = leaves.Count(leave => string.Equals(leave.status, "Approved", StringComparison.OrdinalIgnoreCase)),
                RejectedLeaveRequests = leaves.Count(leave => string.Equals(leave.status, "Rejected", StringComparison.OrdinalIgnoreCase))
            };

            model.RecordsMatch = activeTab switch
            {
                "tasks" => tasks.Count,
                "employees" => employees.Count,
                "leave" => leaves.Count,
                _ => projects.Count
            };

            model.ProjectRows = projects
                .Take(takeCount)
                .Select(project => new ProjectReportRow
                {
                    Name = project.name,
                    Status = project.status,
                    Priority = project.priority,
                    Progress = projectProgressById.TryGetValue(project.project_id, out var liveProgress)
                        ? liveProgress
                        : 0
                })
                .ToList();

            model.TaskRows = tasks
                .Take(takeCount)
                .Select(task => new TaskReportRow
                {
                    Title = task.title,
                    Project = task.Project?.name ?? task.project_name ?? "-",
                    Status = string.Equals(task.status, "Pending", StringComparison.OrdinalIgnoreCase) ? "To Do" : task.status,
                    Priority = task.priority
                })
                .ToList();

            model.EmployeeRows = employees
                .Take(takeCount)
                .Select(employee => new EmployeeReportRow
                {
                    Name = employee.full_name,
                    Department = string.IsNullOrWhiteSpace(employee.department) ? "-" : employee.department,
                    Position = string.IsNullOrWhiteSpace(employee.position) ? "-" : employee.position,
                    Role = string.IsNullOrWhiteSpace(employee.employee_role) ? "-" : employee.employee_role.ToLowerInvariant()
                })
                .ToList();

            model.LeaveRows = leaves
                .Take(takeCount)
                .Select(leave => new LeaveReportRow
                {
                    Employee = leave.employee_name,
                    Type = leave.leave_type,
                    StartDate = leave.start_date,
                    EndDate = leave.end_date,
                    Days = leave.days_count,
                    Status = leave.status.ToLowerInvariant(),
                    ReviewedBy = string.IsNullOrWhiteSpace(leave.reviewed_by) ? "-" : leave.reviewed_by
                })
                .ToList();

            return model;
        }

        private static string NormalizeTab(string? tab)
        {
            return (tab ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "tasks" => "tasks",
                "employees" => "employees",
                "leave" => "leave",
                _ => "projects"
            };
        }
    }
}
