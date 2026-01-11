using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Forms;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.CRM
{
    public class CustomerAttachmentsService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IWebHostEnvironment _env;

        public CustomerAttachmentsService(IDbContextFactory<AppDbContext> dbFactory, IWebHostEnvironment env)
        {
            _dbFactory = dbFactory;
            _env = env;
        }

        public async Task<List<CustomerAttachment>> ListAsync(int companyId, int customerId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.CustomerAttachments.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.CustomerId == customerId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public string GetPublicUrl(CustomerAttachment a)
        {
            // StoredPath is relative under wwwroot (uploads/...)
            return "/" + a.StoredPath.Replace("\\", "/").TrimStart('/');
        }

        public async Task UploadAsync(int companyId, int customerId, IBrowserFile file, string fileType, string? uploadedBy = null)
        {
            if (file == null) throw new Exception("File not selected.");

            const long maxBytes = 20L * 1024 * 1024; // 20MB
            if (file.Size > maxBytes)
                throw new Exception("Max file size is 20MB.");

            // allow PDF + common images
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            var allowed = new HashSet<string> { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
            if (!allowed.Contains(ext))
                throw new Exception("Only PDF / PNG / JPG / WEBP allowed.");

            fileType = string.IsNullOrWhiteSpace(fileType) ? "Other" : fileType.Trim();

            var safeName = MakeSafeFileName(Path.GetFileNameWithoutExtension(file.Name));
            var finalName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{safeName}{ext}";

            // wwwroot/uploads/{company}/customers/{customer}/finalName
            var relDir = Path.Combine("uploads", companyId.ToString(), "customers", customerId.ToString());
            var absDir = Path.Combine(_env.WebRootPath, relDir);
            Directory.CreateDirectory(absDir);

            var absPath = Path.Combine(absDir, finalName);
            await using (var fs = File.Create(absPath))
            {
                await using var stream = file.OpenReadStream(maxBytes);
                await stream.CopyToAsync(fs);
            }

            var storedPath = Path.Combine(relDir, finalName).Replace("\\", "/");

            await using var db = await _dbFactory.CreateDbContextAsync();
            db.CustomerAttachments.Add(new CustomerAttachment
            {
                CompanyId = companyId,
                CustomerId = customerId,
                FileType = fileType,
                FileName = file.Name,
                StoredPath = storedPath,
                SizeBytes = file.Size,
                UploadedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int companyId, int attachmentId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var a = await db.CustomerAttachments.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CustomerAttachmentId == attachmentId);
            if (a == null) return;

            // delete physical file
            var abs = Path.Combine(_env.WebRootPath, a.StoredPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (File.Exists(abs)) File.Delete(abs);

            db.CustomerAttachments.Remove(a);
            await db.SaveChangesAsync();
        }

        static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }
    }
}
