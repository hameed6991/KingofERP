using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services
{
    public static class InvoiceTemplateSeeder
    {
        public static async Task EnsureSystemTemplatesAsync(AppDbContext db)
        {
            // Already seeded?
            if (await db.InvoiceTemplates.AnyAsync(x => x.IsSystem && x.IsActive))
                return;

            var samples = new List<InvoiceTemplate>
            {
                new InvoiceTemplate
                {
                    CompanyId = 0,
                    Name = "Clinic • Classic Ledger v1",
                    IndustryTag = "Clinic",
                    BaseKey = "classic",
                    IsSystem = true,
                    IsActive = true,
                    Settings = new InvoiceTemplateSettings
                    {
                        Style = "Classic",
                        HeaderStyle = "Split",
                        AccentHex = "#3b82f6",
                        ShowQRCode = true,
                        ShowTRN = true,
                        ShowNotes = true,
                        FooterNote = "Thank you for your business."
                    }
                },
                new InvoiceTemplate
                {
                    CompanyId = 0,
                    Name = "Clinic • Compact POS v1",
                    IndustryTag = "Clinic",
                    BaseKey = "compact",
                    IsSystem = true,
                    IsActive = true,
                    Settings = new InvoiceTemplateSettings
                    {
                        Style = "Modern",
                        HeaderStyle = "Compact",
                        AccentHex = "#8b5cf6",
                        ShowQRCode = false,
                        ShowTRN = true,
                        ShowNotes = false
                    }
                },
                new InvoiceTemplate
                {
                    CompanyId = 0,
                    Name = "Trading • Modern Pro v1",
                    IndustryTag = "Trading",
                    BaseKey = "modern",
                    IsSystem = true,
                    IsActive = true,
                    Settings = new InvoiceTemplateSettings
                    {
                        Style = "Modern",
                        HeaderStyle = "Split",
                        AccentHex = "#06b6d4",
                        ShowQRCode = true,
                        ShowNotes = true
                    }
                }
            };

            foreach (var t in samples)
            {
                t.CreatedAtUtc = DateTime.UtcNow;
                t.UpdatedAtUtc = DateTime.UtcNow;
                t.SettingsJson = t.Settings.ToJson();
                db.InvoiceTemplates.Add(t);
            }

            await db.SaveChangesAsync();
        }
    }
}
