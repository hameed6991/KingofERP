using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UaeEInvoice.Data.Models;
using UaeEInvoice.Services.Auth;   // ✅ ICurrentCompany

namespace UaeEInvoice.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    // ✅ Tenant resolver (CompanyId from logged-in user claim)
    private readonly ICurrentCompany? _currentCompany;

    // ✅ This is used in Global Query Filters
    public int CurrentCompanyId => _currentCompany?.CompanyId ?? 0;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentCompany currentCompany)
        : base(options)
    {
        _currentCompany = currentCompany;
    }

    // ---------------------------
    // DbSets
    // ---------------------------
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Item> Items => Set<Item>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();

    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();

    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<InvoiceReceipt> InvoiceReceipts => Set<InvoiceReceipt>();

    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<GeneralLedgerEntry> GeneralLedgerEntries => Set<GeneralLedgerEntry>();
    public DbSet<LedgerEntry> LedgerEntries { get; set; } = default!;
    public DbSet<AccountRoleMap> AccountRoleMaps => Set<AccountRoleMap>();
    public DbSet<RecurringCashRule> RecurringCashRules => Set<RecurringCashRule>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();

    public DbSet<LoginMaster> LoginMasters => Set<LoginMaster>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<PettyCashClaim> PettyCashClaims => Set<PettyCashClaim>();
    public DbSet<PettyCashClaimLine> PettyCashClaimLines => Set<PettyCashClaimLine>();
    public DbSet<PettyCashVoucher> PettyCashVouchers => Set<PettyCashVoucher>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    public DbSet<EInvoiceDocument> EInvoiceDocuments => Set<EInvoiceDocument>();
    public DbSet<EInvoiceProfile> EInvoiceProfiles => Set<EInvoiceProfile>();

    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();

    public DbSet<CompanyFeature> CompanyFeatures => Set<CompanyFeature>();

    public DbSet<ChequeBook> ChequeBooks => Set<ChequeBook>();
    public DbSet<ChequeTransaction> ChequeTransactions => Set<ChequeTransaction>();

    public DbSet<ConstructionProject> ConstructionProjects => Set<ConstructionProject>();

    public DbSet<ConstructionSubcontractor> ConstructionSubcontractors => Set<ConstructionSubcontractor>();
    public DbSet<SubcontractorPC> SubcontractorPCs => Set<SubcontractorPC>();
    public DbSet<SubcontractorPCLine> SubcontractorPCLines => Set<SubcontractorPCLine>();
    public DbSet<ConstructionAccountsMap> ConstructionAccountsMaps => Set<ConstructionAccountsMap>();

    public DbSet<ConstructionPurchaseBill> ConstructionPurchaseBills => Set<ConstructionPurchaseBill>();
    public DbSet<ConstructionPurchaseBillLine> ConstructionPurchaseBillLines => Set<ConstructionPurchaseBillLine>();
    public DbSet<DocSequence> DocSequences => Set<DocSequence>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();








    // ---------------------------
    // ✅ STEP-6: Write Guard
    // ---------------------------
    public override int SaveChanges()
    {
        ApplyCompanyIdRules();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyCompanyIdRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyCompanyIdRules()
    {
        var cid = CurrentCompanyId;

        // If not logged in (or system/seed)
        if (cid <= 0) return;

        // ✅ Force CompanyId on inserts; block CompanyId updates
        foreach (var entry in ChangeTracker.Entries<ICompanyEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CompanyId = cid;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.CompanyId).IsModified = false;
            }
        }
    }

    // ---------------------------
    // ✅ STEP-5: Global Query Filters
    // ---------------------------
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<BankAccount>()
       .HasIndex(x => new { x.CompanyId, x.AccountNo })
       .IsUnique(false);

        modelBuilder.Entity<BankStatementImport>()
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.BankStatementImportId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseInvoice>()
    .HasIndex(x => new { x.CompanyId, x.PurchaseNoSeq })
    .IsUnique();


        modelBuilder.Entity<InvoiceTemplate>(e =>
        {
            e.HasKey(x => x.InvoiceTemplateId);

            e.HasIndex(x => new { x.CompanyId, x.IsSystem, x.IsActive });

            // Optional: avoid duplicates for system templates
            e.HasIndex(x => new { x.CompanyId, x.BaseKey })
             .IsUnique(false);

            e.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CustomCss).HasColumnType("nvarchar(max)");
            e.Property(x => x.CustomHtml).HasColumnType("nvarchar(max)");
        });


        modelBuilder.Entity<ConstructionPurchaseBill>()
    .HasMany(x => x.Lines)
    .WithOne(x => x.ConstructionPurchaseBill)
    .HasForeignKey(x => x.ConstructionPurchaseBillId)
    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConstructionPurchaseBill>()
            .HasIndex(x => new { x.CompanyId, x.BillNo })
            .IsUnique();

        modelBuilder.Entity<DocSequence>()
            .HasIndex(x => new { x.CompanyId, x.DocType })
            .IsUnique();


        modelBuilder.Entity<ConstructionSubcontractor>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.Name });
            e.HasIndex(x => new { x.CompanyId, x.IsActive });
        });

        modelBuilder.Entity<SubcontractorPC>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.PCNo }).IsUnique();
            e.HasIndex(x => new { x.CompanyId, x.ConstructionProjectId, x.ConstructionSubcontractorId, x.PeriodMonth });

            e.HasMany(x => x.Lines)
             .WithOne(x => x.PC!)
             .HasForeignKey(x => x.SubcontractorPCId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubcontractorPCLine>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.SubcontractorPCId });
        });

        modelBuilder.Entity<ConstructionAccountsMap>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.IsActive });
        });
        modelBuilder.Entity<ConstructionProject>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectCode }).IsUnique();
            e.HasIndex(x => new { x.CompanyId, x.Status });
        });

        modelBuilder.Entity<ChequeBook>()
           .HasIndex(x => new { x.CompanyId, x.BankAccountNo, x.StartNo, x.EndNo });

        modelBuilder.Entity<ChequeTransaction>()
            .HasIndex(x => new { x.CompanyId, x.Direction, x.Status, x.ChequeDate });

        modelBuilder.Entity<ChequeTransaction>()
            .HasIndex(x => new { x.CompanyId, x.ChequeBookId, x.ChequeNo });
        modelBuilder.Entity<CompanyFeature>()
       .HasKey(x => new { x.CompanyId, x.FeatureKey });

        modelBuilder.Entity<CompanyFeature>()
            .HasIndex(x => new { x.CompanyId, x.FeatureKey })
            .IsUnique();


        modelBuilder.Entity<BankStatementLine>()
            .HasIndex(x => new { x.CompanyId, x.BankAccountId, x.TxnDate });

        modelBuilder.Entity<BankStatementLine>()
            .Property(x => x.Amount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<BankStatementLine>()
            .Property(x => x.RunningBalance)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<BankStatementLine>()
            .Property(x => x.Confidence)
            .HasColumnType("decimal(5,2)");


        modelBuilder.Entity<PettyCashClaim>()
    .HasMany(x => x.Lines)
    .WithOne(x => x.Claim!)
    .HasForeignKey(x => x.PettyCashClaimId)
    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PettyCashClaim>()
            .HasIndex(x => new { x.CompanyId, x.ClaimNo })
            .IsUnique(false);

        modelBuilder.Entity<PettyCashVoucher>()
            .HasIndex(x => new { x.CompanyId, x.VoucherNo })
            .IsUnique(false);

        // ✅ Company precision
        modelBuilder.Entity<Company>()
            .Property(x => x.DefaultVatRate)
            .HasPrecision(5, 4); // 0.0500

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>()
            .Property(x => x.CompanyId)
            .ValueGeneratedNever();



        // ✅ Prices / amounts precision
        modelBuilder.Entity<Item>().Property(x => x.CostPrice).HasPrecision(18, 4);
        modelBuilder.Entity<Item>().Property(x => x.SellingPrice).HasPrecision(18, 4);
        modelBuilder.Entity<Item>().Property(x => x.VatRate).HasPrecision(5, 4);

        modelBuilder.Entity<Invoice>().Property(x => x.SubTotal).HasPrecision(18, 4);
        modelBuilder.Entity<Invoice>().Property(x => x.VatTotal).HasPrecision(18, 4);
        modelBuilder.Entity<Invoice>().Property(x => x.GrandTotal).HasPrecision(18, 4);

        modelBuilder.Entity<InvoiceLine>().Property(x => x.Qty).HasPrecision(18, 4);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.Rate).HasPrecision(18, 4);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.VatRate).HasPrecision(5, 4);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.LineSubTotal).HasPrecision(18, 4);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.LineVat).HasPrecision(18, 4);
        modelBuilder.Entity<InvoiceLine>().Property(x => x.LineTotal).HasPrecision(18, 4);

        // ✅ Invoice lines cascade
        modelBuilder.Entity<Invoice>()
            .HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ✅ Purchase relations
        modelBuilder.Entity<PurchaseInvoiceLine>()
            .HasOne(x => x.PurchaseInvoice)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.PurchaseInvoiceId);

        modelBuilder.Entity<PurchasePayment>()
            .HasOne(x => x.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InvoiceReceipt>()
            .HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ✅ COA unique per company
        modelBuilder.Entity<ChartOfAccount>()
            .HasIndex(x => new { x.CompanyId, x.AccountNo })
            .IsUnique();

        // ✅ GL index
        modelBuilder.Entity<GeneralLedgerEntry>()
            .HasIndex(x => new { x.CompanyId, x.TxnDate, x.VoucherType, x.VoucherNo });

        // ✅ Payroll relations
        modelBuilder.Entity<PayrollLine>()
            .HasOne(x => x.PayrollRun)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PayrollLine>()
            .HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PayrollRun>()
            .HasIndex(x => new { x.CompanyId, x.PeriodMonth });

        modelBuilder.Entity<PayrollRun>()
            .HasIndex(x => new { x.CompanyId, x.RunNo })
            .IsUnique(false);

        modelBuilder.Entity<LoginMaster>()
            .HasIndex(x => x.SessionGuid)
            .IsUnique();

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>()
            .Property(x => x.CompanyId)
            .ValueGeneratedNever();
        // -----------------------------------------
        // ✅ Tenant Isolation Filters (THE MAIN PART)
        // -----------------------------------------

        // ✅ Identity users also isolated
        modelBuilder.Entity<ApplicationUser>()
            .HasQueryFilter(u => CurrentCompanyId == 0 || u.CompanyId == CurrentCompanyId);

        // ✅ Apply to all entities implementing ICompanyEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == null) continue;

            if (typeof(ICompanyEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ApplyCompanyFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var generic = method?.MakeGenericMethod(entityType.ClrType);
                generic?.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyCompanyFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ICompanyEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => CurrentCompanyId == 0 || e.CompanyId == CurrentCompanyId);
    }
}


// ---------------------------
// Your models (same as before)
// ---------------------------

public class Company
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int CompanyId { get; set; }
    public string LegalName { get; set; } = "";
    public string TRN { get; set; } = "";
    public string Emirate { get; set; } = "Dubai";
    public string City { get; set; } = "Dubai";
    public string InvoicePrefix { get; set; } = "INV";
    public int NextInvoiceNumber { get; set; } = 1;
    public decimal DefaultVatRate { get; set; } = 0.05m;

    [StringLength(10)]
    public string? ShortName { get; set; }  // ✅ "AT"


    [MaxLength(120)]
    public string? Industry { get; set; }

    [MaxLength(120)]
    public string? BusinessType { get; set; } // Trading / Services / Manufacturing etc

    [MaxLength(200), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    // ✅ NEW: Address details
    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(20)]
    public string? POBox { get; set; }

    [MaxLength(60)]
    public string? Country { get; set; } = "United Arab Emirates";

    // ✅ NEW: Invoice preferences / branding
    [MaxLength(10)]
    public string CurrencyCode { get; set; } = "AED";

    [MaxLength(2000)]
    public string? InvoiceFooterNote { get; set; } // bank details / thank you note

    public int PaymentTermsDays { get; set; } = 0; // 0 = due on receipt

    [MaxLength(300)]
    public string? LogoPath { get; set; } // e.g., /uploads/company/11/logo.png


    public DateTime? OpeningBalanceDate { get; set; }

    [Range(0, 999999999)]
    public decimal OpeningCash { get; set; } = 0m;

    [Range(0, 999999999)]
    public decimal OpeningBank { get; set; } = 0m;

    [Range(0, 999999999)]
    public decimal OpeningAR { get; set; } = 0m;

    [Range(0, 999999999)]
    public decimal OpeningAP { get; set; } = 0m;

    [Range(0, 999999999)]
    public decimal OpeningInventory { get; set; } = 0m;

    public bool IsOpeningPosted { get; set; } = false;
}

public class Customer
{
    public int CustomerId { get; set; }
    public int CompanyId { get; set; }

    [Required]
    public string CustomerType { get; set; } = "B2B";

    [Required]
    public string Name { get; set; } = "";

    public string? TRN { get; set; }
    public string? Email { get; set; }
    public string? Mobile { get; set; }

    public string? AddressLine1 { get; set; }
    public string Emirate { get; set; } = "Dubai";
    public string City { get; set; } = "Dubai";
    public string Country { get; set; } = "UAE";


    public DateTime? NextFollowUpDate { get; set; }
    public string? NextFollowUpNote { get; set; }
}
