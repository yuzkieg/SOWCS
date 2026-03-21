using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.Services;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService;

        public DocumentsController(AppDbContext context, IWebHostEnvironment environment, NotificationService notificationService)
        {
            _context = context;
            _environment = environment;
            _notificationService = notificationService;
        }

        private async Task<bool> IsSuperAdminAsync()
        {
            var currentEmail = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentEmail))
            {
                return false;
            }

            return await _context.Users.AnyAsync(user =>
                user.Email == currentEmail &&
                user.Role != null &&
                user.Role.ToLower() == "superadmin");
        }

        private async Task<bool> IsEmployeeUploaderAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var user = await _context.Users.FirstOrDefaultAsync(item => item.Email == email);
            if (user == null)
            {
                return false;
            }

            var role = await _context.Employees
                .Where(employee => employee.user_id == user.Id)
                .Select(employee => employee.employee_role)
                .FirstOrDefaultAsync();

            return !string.IsNullOrWhiteSpace(role) &&
                   role.Trim().Equals("employee", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public async Task<IActionResult> Documents(string? search, string? category)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(document =>
                    document.title.Contains(search) ||
                    document.file_name.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(document => document.category == category);
            }

            var model = new DocumentsPageViewModel
            {
                Documents = await query.OrderByDescending(document => document.uploaded_date).ToListAsync(),
                Search = search,
                Category = category
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Preview(int documentId)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            var fullPath = ResolveDocumentPath(document);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                if (!string.IsNullOrWhiteSpace(document.file_path) && document.file_path.StartsWith("/"))
                {
                    return Redirect(document.file_path);
                }
                return NotFound();
            }

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            if (!contentTypeProvider.TryGetContentType(document.file_name, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        [HttpGet]
        public async Task<IActionResult> Download(int documentId)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            var fullPath = ResolveDocumentPath(document);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                if (!string.IsNullOrWhiteSpace(document.file_path) && document.file_path.StartsWith("/"))
                {
                    return Redirect(document.file_path);
                }
                return NotFound();
            }

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            if (!contentTypeProvider.TryGetContentType(document.file_name, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(fullPath, contentType, fileDownloadName: document.file_name, enableRangeProcessing: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile uploadedFile, string? category, string? title, string? description, bool visibleToAllEmployees = false)
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
            {
                TempData["DocumentsError"] = "Please select a valid file.";
                return RedirectToAction(nameof(Documents));
            }

            var uploadsDirectory = Path.Combine(_environment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsDirectory);

            var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(uploadedFile.FileName)}";
            var fullPath = Path.Combine(uploadsDirectory, safeFileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await uploadedFile.CopyToAsync(stream);
            }

            var document = new DocumentRecord
            {
                title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(uploadedFile.FileName) : title.Trim(),
                file_name = uploadedFile.FileName,
                file_path = $"/uploads/{safeFileName}",
                category = string.IsNullOrWhiteSpace(category) ? "Other" : category,
                status = "Pending",
                file_size_bytes = uploadedFile.Length,
                uploaded_date = DateTime.UtcNow,
                uploaded_by_email = User.Identity?.Name
            };

            _context.Documents.Add(document);
            if (await IsEmployeeUploaderAsync(document.uploaded_by_email))
            {
                await _notificationService.AddForRoleGroupAsync(
                    "project manager",
                    "New Document Submission",
                    $"{document.title} was uploaded and is waiting for approval.",
                    "DocumentApproval",
                    "/Approvals/Approvals");
            }

            await _notificationService.AddForRoleGroupAsync(
                "superadmin",
                "New Document Submission",
                $"{document.title} was uploaded and is waiting for approval.",
                "DocumentApproval",
                "/Approvals/Approvals");
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Documents));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int documentId, string title, string category, IFormFile? uploadedFile)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            if (uploadedFile != null && uploadedFile.Length > 0)
            {
                var uploadsDirectory = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsDirectory);

                var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(uploadedFile.FileName)}";
                var fullPath = Path.Combine(uploadsDirectory, safeFileName);

                await using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await uploadedFile.CopyToAsync(stream);
                }

                if (!string.IsNullOrWhiteSpace(document.file_path))
                {
                    var oldRelativePath = document.file_path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var oldFullPath = Path.Combine(_environment.WebRootPath, oldRelativePath);
                    if (System.IO.File.Exists(oldFullPath))
                    {
                        System.IO.File.Delete(oldFullPath);
                    }
                }

                document.file_name = uploadedFile.FileName;
                document.file_path = $"/uploads/{safeFileName}";
                document.file_size_bytes = uploadedFile.Length;
            }

            document.title = title.Trim();
            document.category = category.Trim();
            document.status = "Pending";
            document.review_notes = null;
            document.reviewed_by = null;
            document.reviewed_date = null;

            if (await IsEmployeeUploaderAsync(document.uploaded_by_email))
            {
                await _notificationService.AddForRoleGroupAsync(
                    "project manager",
                    "Document Updated",
                    $"{document.title} was updated and needs approval review.",
                    "DocumentApproval",
                    "/Approvals/Approvals");
            }

            await _notificationService.AddForRoleGroupAsync(
                "superadmin",
                "Document Updated",
                $"{document.title} was updated and needs approval review.",
                "DocumentApproval",
                "/Approvals/Approvals");
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Document updated successfully.";

            return RedirectToAction(nameof(Documents));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int documentId)
        {
            if (!await IsSuperAdminAsync())
            {
                return Forbid();
            }

            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            _context.ArchiveItems.Add(new ArchiveItem
            {
                source_id = document.document_id,
                source_type = "Document",
                title = document.title,
                type = "Document",
                archived_by = User.Identity?.Name ?? "System",
                date_archived = DateTime.UtcNow,
                reason = string.IsNullOrWhiteSpace(document.review_notes)
                    ? "Archived from Documents module"
                    : $"Archived from Documents module. Feedback: {document.review_notes}",
                serialized_data = JsonSerializer.Serialize(document)
            });

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Document archived successfully.";

            return RedirectToAction(nameof(Documents));
        }

        private string? ResolveDocumentPath(DocumentRecord document)
        {
            var uploadsDirectory = Path.Combine(_environment.WebRootPath, "uploads");
            var contentUploadsDirectory = Path.Combine(_environment.ContentRootPath, "uploads");

            static string DecodeValue(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                try
                {
                    return Uri.UnescapeDataString(value);
                }
                catch
                {
                    return value;
                }
            }

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(document.file_path))
            {
                var normalizedPath = document.file_path.Trim();
                var decodedPath = DecodeValue(normalizedPath);

                if (Uri.TryCreate(decodedPath, UriKind.Absolute, out var parsedUri))
                {
                    if (parsedUri.IsFile)
                    {
                        var localFilePath = parsedUri.LocalPath;
                        if (!string.IsNullOrWhiteSpace(localFilePath))
                        {
                            candidates.Add(localFilePath);
                        }
                    }
                    else
                    {
                        decodedPath = parsedUri.LocalPath;
                    }
                }

                if (Path.IsPathRooted(decodedPath) && System.IO.File.Exists(decodedPath))
                {
                    candidates.Add(decodedPath);
                }
                else
                {
                    var webRelative = decodedPath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
                    candidates.Add(Path.Combine(_environment.WebRootPath, webRelative));
                    candidates.Add(Path.Combine(uploadsDirectory, Path.GetFileName(decodedPath)));
                    candidates.Add(Path.Combine(contentUploadsDirectory, Path.GetFileName(decodedPath)));
                }
            }

            if (!string.IsNullOrWhiteSpace(document.file_name))
            {
                var decodedFileName = DecodeValue(document.file_name);
                candidates.Add(Path.Combine(uploadsDirectory, Path.GetFileName(decodedFileName)));
                candidates.Add(Path.Combine(contentUploadsDirectory, Path.GetFileName(decodedFileName)));
            }

            foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
            {
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var safeFileName = Path.GetFileName(DecodeValue(document.file_name ?? string.Empty));
            if (string.IsNullOrWhiteSpace(safeFileName) || string.IsNullOrWhiteSpace(Path.GetExtension(safeFileName)))
            {
                return null;
            }

            var matchedFiles = new List<string>();
            if (Directory.Exists(uploadsDirectory))
            {
                matchedFiles.AddRange(Directory.GetFiles(uploadsDirectory, $"*_{safeFileName}"));
                matchedFiles.AddRange(Directory.GetFiles(uploadsDirectory, $"*{Path.GetExtension(safeFileName)}"));
            }
            if (Directory.Exists(contentUploadsDirectory))
            {
                matchedFiles.AddRange(Directory.GetFiles(contentUploadsDirectory, $"*_{safeFileName}"));
                matchedFiles.AddRange(Directory.GetFiles(contentUploadsDirectory, $"*{Path.GetExtension(safeFileName)}"));
            }

            if (matchedFiles.Count == 0)
            {
                return null;
            }

            var preferred = matchedFiles
                .Where(file =>
                {
                    var baseName = Path.GetFileName(file);
                    return baseName.Contains(Path.GetFileNameWithoutExtension(safeFileName), StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (preferred.Count > 0)
            {
                matchedFiles = preferred;
            }

            return matchedFiles
                .OrderByDescending(file => System.IO.File.GetLastWriteTimeUtc(file))
                .FirstOrDefault();
        }
    }
}


