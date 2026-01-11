using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using UaeEInvoice.Components;
using UaeEInvoice.Data;
using UaeEInvoice.Services;
using UaeEInvoice.Services.Auth;
using UaeEInvoice.Services.Cheques;
using UaeEInvoice.Services.CRM;
using UaeEInvoice.Services.Einvoicing;
using UaeEInvoice.Services.Payroll;

var builder = WebApplication.CreateBuilder(args);

// Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Db
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// HttpContext + Antiforgery
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();

// Identity
builder.Services
    .AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireLowercase = false;
        opt.Password.RequireDigit = false;
        opt.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Cookies
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath = "/login";
    opt.AccessDeniedPath = "/access-denied";
    opt.ReturnUrlParameter = "ReturnUrl";

    opt.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/_blazor") ||
                ctx.Request.Path.StartsWithSegments("/_framework") ||
                ctx.Request.Path.StartsWithSegments("/ai") ||
                ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/_blazor") ||
                ctx.Request.Path.StartsWithSegments("/_framework") ||
                ctx.Request.Path.StartsWithSegments("/ai"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Sales", p => p.RequireRole("Admin", "Sales"));
    options.AddPolicy("Purchase", p => p.RequireRole("Admin", "Purchase"));
    options.AddPolicy("Inventory", p => p.RequireRole("Admin", "Inventory"));
    options.AddPolicy("Setup", p => p.RequireRole("Admin"));
});

// App Services
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<CashFlowService>();
builder.Services.AddScoped<CashForecastService>();
builder.Services.AddScoped<AccountRoleService>();

builder.Services.AddSingleton<OpenAiTestService>();

builder.Services.AddScoped<AiReportRouter>();
builder.Services.AddScoped<AiReportRunner>();

builder.Services.AddScoped<AiSalesQueryRouter>();
builder.Services.AddScoped<AiSalesQueryRunner>();
builder.Services.AddScoped<UaeEInvoice.Services.CashflowCopilot.CashflowCopilotService>();

builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<UaeEInvoice.Services.Import.ExcelImportService>();

// Auth / Session / Current Company
builder.Services.AddScoped<UaeEInvoice.Services.Auth.SessionService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<ICurrentCompany, CurrentCompany>();
builder.Services.AddScoped<UaeEInvoice.Services.InventoryPostingService>();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(o => o.DetailedErrors = true);

builder.Services.AddScoped<PettyCashService>();
builder.Services.AddScoped<BankReconciliationService>();
builder.Services.AddScoped<EinvoiceService>();
builder.Services.AddScoped<UaeEInvoice.Services.Reports.AgingService>();
builder.Services.AddScoped<UaeEInvoice.Services.Reports.VatReportService>();
builder.Services.AddScoped<UaeEInvoice.Services.Reports.VatReturnExportService>();
builder.Services.AddScoped<UaeEInvoice.Services.Reports.VatReconciliationService>();
builder.Services.AddScoped<UaeEInvoice.Services.CRM.CustomerHubService>();
builder.Services.AddScoped<CustomerNotesService>();
builder.Services.AddScoped<UaeEInvoice.Services.CRM.CustomerTasksService>();
builder.Services.AddScoped<UaeEInvoice.Services.CRM.CustomerAttachmentsService>();
builder.Services.AddScoped<UaeEInvoice.Services.Analytics.AnalyticsService>();
builder.Services.AddScoped<CompanyFeatureService>();
builder.Services.AddScoped<UaeEInvoice.Services.Cheques.ChequeAccountingService>();
builder.Services.AddScoped<ChequeWorkflowService>();
builder.Services.AddScoped<UaeEInvoice.Services.DocSequenceService>();
builder.Services.AddScoped<UaeEInvoice.Services.ConstructionPurchasePostingService>();
builder.Services.AddScoped<SubcontractorPCPostingService>();
builder.Services.AddScoped<ICompanySetupGate, CompanySetupGate>();
builder.Services.AddScoped<ICompanyContextService, CompanyContextService>();
builder.Services.AddScoped<UaeEInvoice.Services.InvoiceTemplateService>();





builder.Services.AddScoped<
    IUserClaimsPrincipalFactory<ApplicationUser>,
    UaeEInvoice.Services.Auth.AppUserClaimsFactory>();

builder.Services.AddControllers();

// HttpClient
builder.Services.AddHttpClient("ServerAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7193/");
});

// ✅ HttpClient for Blazor pages calling same server APIs
builder.Services.AddHttpClient();
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});

var app = builder.Build();

QuestPDF.Settings.License = LicenseType.Community;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

//
// ✅ OPTIONAL BUT RECOMMENDED (Zoho style)
// If user logged-in but 2FA not enabled -> force enable page
//
app.Use(async (ctx, next) =>
{
    if (ctx.User?.Identity?.IsAuthenticated == true)
    {
        var path = ctx.Request.Path;

        if (!path.StartsWithSegments("/account/enable-2fa")
            && !path.StartsWithSegments("/login")
            && !path.StartsWithSegments("/login-2fa")
            && !path.StartsWithSegments("/auth")
            && !path.StartsWithSegments("/logout")
            && !path.StartsWithSegments("/_framework")
            && !path.StartsWithSegments("/_blazor")
            && !path.StartsWithSegments("/css")
            && !path.StartsWithSegments("/js")
            && !path.StartsWithSegments("/favicon"))
        {
            var userMgr = ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var u = await userMgr.GetUserAsync(ctx.User);

            if (u != null && !u.TwoFactorEnabled)
            {
                var returnUrl = (ctx.Request.Path + ctx.Request.QueryString).ToString();
                ctx.Response.Redirect($"/account/enable-2fa?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }
        }
    }

    await next();
});

app.UseAntiforgery();
app.MapControllers();

// --------------------
// AI endpoints
// --------------------
app.MapGet("/ai/ping", async (OpenAiTestService ai) => await ai.PingAsync())
   .DisableAntiforgery()
   .RequireAuthorization();

app.MapPost("/ai/ask", async (
    AiAskDto dto,
    AiReportRouter router,
    AiReportRunner runner,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("AiAsk");

    try
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Text))
            return Results.BadRequest(new { error = "Text is required" });

        var req = await router.RouteAsync(dto.Text);

        if (!string.IsNullOrWhiteSpace(req.ClarifyQuestion))
            return Results.Ok(new { clarify = req.ClarifyQuestion, request = req });

        var result = await runner.RunAsync(req);

        return Results.Ok(new { request = req, result });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "AI /ai/ask failed. CompanyId={CompanyId}, Text={Text}", dto?.CompanyId, dto?.Text);

        if (app.Environment.IsDevelopment())
        {
            return Results.Problem(
                title: "AI Ask failed (DEV)",
                detail: ex.ToString(),
                statusCode: 500);
        }

        return Results.Problem(
            title: "AI Ask failed",
            detail: "Internal error while generating report.",
            statusCode: 500);
    }
})
.DisableAntiforgery()
.RequireAuthorization();

// Sales route (semantic)
app.MapPost("/ai/sales/route", async (AiSalesAskDto dto, AiSalesQueryRouter router) =>
{
    var env = await router.RouteAsync(dto.Text);

    return Results.Ok(env);
})
.DisableAntiforgery()
.RequireAuthorization();

// Sales run
app.MapPost("/ai/sales/run", async (AiSalesAskDto dto, AiSalesQueryRouter router, AiSalesQueryRunner runner) =>
{
    var env = await router.RouteAsync(dto.Text);

    if (!env.Supported || env.Spec is null || !env.Spec.IsSalesQuery)
        return Results.BadRequest(env);

    var result = await runner.RunAsync(env.Spec);

    return Results.Ok(result);
})
.DisableAntiforgery()
.RequireAuthorization();


// --------------------
// Login/Logout endpoints
// --------------------

// ✅ LOGIN (keeps your RememberMe fix + adds 2FA flow)
app.MapPost("/auth/login", async (
    HttpContext http,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await http.Request.ReadFormAsync();

    var Email = (form["Email"].ToString() ?? "").Trim();
    var Password = form["Password"].ToString() ?? "";
    var ReturnUrl = form["ReturnUrl"].ToString();

    // RememberMe comes as "false,true" -> take last value
    var rememberStr = form["RememberMe"].ToArray().LastOrDefault() ?? "false";
    var RememberMe = string.Equals(rememberStr, "true", StringComparison.OrdinalIgnoreCase);

    var ru = string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl!;
    if (!ru.StartsWith("/")) ru = "/";

    var result = await signInManager.PasswordSignInAsync(
        Email,
        Password,
        RememberMe,
        lockoutOnFailure: false);

    // ✅ If user has 2FA enabled -> OTP step required
    if (result.RequiresTwoFactor)
    {
        return Results.LocalRedirect($"/login-2fa?ReturnUrl={Uri.EscapeDataString(ru)}&RememberMe={(RememberMe ? "1" : "0")}");
    }

    if (!result.Succeeded)
    {
        return Results.LocalRedirect($"/login?error=1&ReturnUrl={Uri.EscapeDataString(ru)}");
    }

    // ✅ Force all users to setup 2FA (first time)
    var user = await userManager.FindByEmailAsync(Email);
    if (user != null && !user.TwoFactorEnabled)
    {
        return Results.LocalRedirect($"/account/enable-2fa?ReturnUrl={Uri.EscapeDataString(ru)}");
    }

    return Results.LocalRedirect(ru);
})
.AllowAnonymous()
.DisableAntiforgery();


// ✅ OTP VERIFY (new)
app.MapPost("/auth/2fa", async (
    HttpContext http,
    SignInManager<ApplicationUser> signInManager) =>
{
    var form = await http.Request.ReadFormAsync();

    var Code = form["Code"].ToString() ?? "";
    var ReturnUrl = form["ReturnUrl"].ToString();

    // RememberMe value from query/form: "true/false"
    var rememberStr = form["RememberMe"].ToArray().LastOrDefault() ?? "false";
    var RememberMe = string.Equals(rememberStr, "true", StringComparison.OrdinalIgnoreCase);

    var ru = string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl!;
    if (!ru.StartsWith("/")) ru = "/";

    var clean = (Code ?? "").Replace(" ", "").Replace("-", "");

    var res = await signInManager.TwoFactorAuthenticatorSignInAsync(
        clean,
        RememberMe,
        rememberClient: false   // ✅ strict: OTP every login
    );

    if (res.Succeeded)
        return Results.LocalRedirect(ru);

    return Results.LocalRedirect($"/login-2fa?error=1&ReturnUrl={Uri.EscapeDataString(ru)}&RememberMe={(RememberMe ? "1" : "0")}");
})
.AllowAnonymous()
.DisableAntiforgery();


app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/login");
});


// --------------------
// ✅ PDF endpoint (protected) - DYNAMIC & FIXED (NO CurrentCompany)
// --------------------
app.MapGet("/invoice-pdf/{id:int}", async (
    int id,
    AppDbContext db,
    HttpContext http,
    UserManager<ApplicationUser> userMgr) =>
{
    if (http.User?.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    // ✅ Always reliable companyId (from logged-in user row)
    var userId = userMgr.GetUserId(http.User);
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized();

    var companyId = await db.Users
        .AsNoTracking()
        .Where(u => u.Id == userId)
        .Select(u => u.CompanyId)
        .FirstOrDefaultAsync();

    if (companyId <= 0)
        return Results.NotFound("Company not found for logged-in user.");

    var company = await db.Companies
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.CompanyId == companyId);

    if (company == null)
        return Results.NotFound("Company setup not found.");

    var inv = await db.Invoices
        .AsNoTracking()
        .AsSplitQuery()
        .Include(x => x.Lines)
        .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.InvoiceId == id);

    if (inv == null)
        return Results.NotFound("Invoice not found.");

    var lines = inv.Lines.OrderBy(x => x.InvoiceLineId).ToList();
    var pdfBytes = InvoicePdfGenerator.Generate(company, inv, lines);

    return Results.File(pdfBytes, "application/pdf", $"{inv.InvoiceNo}.pdf");
})
.RequireAuthorization();


// Blazor
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();


// Migrate + Seed
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "Admin", "Sales", "Purchase", "Inventory" };

    foreach (var r in roles)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    var adminEmail = "admin@uae.com";
    var admin = await userMgr.FindByEmailAsync(adminEmail);

    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            CompanyId = 1
        };

        await userMgr.CreateAsync(admin, "Admin@123");
        await userMgr.AddToRoleAsync(admin, "Admin");
    }

    var ledger = scope.ServiceProvider.GetRequiredService<LedgerService>();

    var companyIds = await db.Companies.Select(x => x.CompanyId).ToListAsync();
    foreach (var cid in companyIds)
        await ledger.EnsureDefaultAccountsAsync(cid);
}

using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var db = scope.ServiceProvider.GetRequiredService<UaeEInvoice.Data.AppDbContext>();
    await UaeEInvoice.Services.InvoiceTemplateSeeder.EnsureSystemTemplatesAsync(db);

    // Create roles
    foreach (var r in new[] { "Admin", "Sales", "Purchase", "Inventory" })
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    // ✅ make your email admin (change email)
    var adminEmail = "admin@gmail.com";
    var admin = await userMgr.FindByEmailAsync(adminEmail);
    if (admin != null && !await userMgr.IsInRoleAsync(admin, "Admin"))
        await userMgr.AddToRoleAsync(admin, "Admin");
}

app.Run();
