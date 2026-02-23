using IT15_SOWCS.Data;
using IT15_SOWCS.Models;
using IT15_SOWCS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IT15_SOWCS.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DocumentsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile uploadedFile, string? category)
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
                title = Path.GetFileNameWithoutExtension(uploadedFile.FileName),
                file_name = uploadedFile.FileName,
                file_path = $"/uploads/{safeFileName}",
                category = string.IsNullOrWhiteSpace(category) ? "Other" : category,
                status = "Pending",
                file_size_bytes = uploadedFile.Length,
                uploaded_date = DateTime.UtcNow,
                uploaded_by_email = User.Identity?.Name
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            await AddAuditLog("upload", "Document", $"Uploaded document: {document.file_name}");

            return RedirectToAction(nameof(Documents));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int documentId, string title, string category)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            document.title = title.Trim();
            document.category = category.Trim();

            await _context.SaveChangesAsync();
            await AddAuditLog("update", "Document", $"Updated document: {document.file_name}");

            return RedirectToAction(nameof(Documents));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int documentId)
        {
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
                reason = "Archived from Documents module",
                serialized_data = JsonSerializer.Serialize(document)
            });

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            await AddAuditLog("archive", "Document", $"Archived document: {document.file_name}");

            return RedirectToAction(nameof(Documents));
        }

        private async Task AddAuditLog(string action, string entity, string description)
        {
            var email = User.Identity?.Name ?? "system@local";
            _context.AuditLogs.Add(new AuditLogEntry
            {
                action = action,
                entity = entity,
                description = description,
                user_email = email,
                user_name = email
            });

            await _context.SaveChangesAsync();
        }
    }
}
