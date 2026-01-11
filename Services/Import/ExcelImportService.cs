using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services.Import;

public class ExcelImportService
{
    private readonly AppDbContext _db;
    public ExcelImportService(AppDbContext db) => _db = db;

    // =========================
    // PUBLIC METHODS
    // =========================

    public async Task<ImportResult> ImportEmployeesAsync(Stream excel, int companyId, bool overwrite)
    {
        // 🔥 Runtime property resolve (no hardcode)
        var companyProp = RequireProp<Employee>("CompanyId");
        var codeProp = RequireProp<Employee>("EmpCode", "EmployeeCode", "EmpNo", "Code");
        var nameProp = RequireProp<Employee>("EmpName", "EmployeeName", "Name", "FullName");

        // Optional props (if exists)
        var mobileProp = FindProp<Employee>("Mobile", "MobileNo", "Phone", "PhoneNo", "ContactNo");
        var emailProp = FindProp<Employee>("Email", "EmailId", "Mail", "EmailAddress");
        var basicProp = FindProp<Employee>("BasicSalary", "Salary", "Basic", "BasicPay");
        var allowProp = FindProp<Employee>("Allowance", "Allow", "Allowances");
        var activeProp = FindProp<Employee>("IsActive", "Active", "IsEnabled", "Enabled");

        var res = new ImportResult();

        using var wb = new XLWorkbook(excel);
        var ws = wb.Worksheets.FirstOrDefault() ?? throw new Exception("Excel sheet not found.");
        var header = ReadHeaderMap(ws);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            res.TotalRows++;

            // Read from Excel by header names (support aliases)
            var empCode = Get(ws, r, header, "EmpCode", "EmployeeCode", "EmpNo", "Code").Trim();
            var empName = Get(ws, r, header, "EmpName", "EmployeeName", "Name", "FullName").Trim();

            if (string.IsNullOrWhiteSpace(empCode) || string.IsNullOrWhiteSpace(empName))
            {
                res.Skipped++;
                continue;
            }

            try
            {
                var existing = await _db.Employees.FirstOrDefaultAsync(x =>
                    EF.Property<int>(x, companyProp) == companyId &&
                    EF.Property<string>(x, codeProp) == empCode
                );

                if (existing == null)
                {
                    var e = new Employee();
                    SetValue(e, companyProp, companyId);
                    SetValue(e, codeProp, empCode);
                    SetValue(e, nameProp, empName);

                    SetIfExists(e, mobileProp, Get(ws, r, header, "Mobile", "MobileNo", "Phone", "ContactNo"));
                    SetIfExists(e, emailProp, Get(ws, r, header, "Email", "EmailId", "Mail"));
                    SetIfExists(e, basicProp, Get(ws, r, header, "BasicSalary", "Salary", "BasicPay"));
                    SetIfExists(e, allowProp, Get(ws, r, header, "Allowance", "Allow", "Allowances"));
                    SetIfExists(e, activeProp, Get(ws, r, header, "IsActive", "Active"));

                    _db.Employees.Add(e);
                    res.Inserted++;
                }
                else
                {
                    if (!overwrite)
                    {
                        res.Skipped++;
                        continue;
                    }

                    SetValue(existing, nameProp, empName);
                    SetIfExists(existing, mobileProp, Get(ws, r, header, "Mobile", "MobileNo", "Phone", "ContactNo"));
                    SetIfExists(existing, emailProp, Get(ws, r, header, "Email", "EmailId", "Mail"));
                    SetIfExists(existing, basicProp, Get(ws, r, header, "BasicSalary", "Salary", "BasicPay"));
                    SetIfExists(existing, allowProp, Get(ws, r, header, "Allowance", "Allow", "Allowances"));
                    SetIfExists(existing, activeProp, Get(ws, r, header, "IsActive", "Active"));

                    res.Updated++;
                }
            }
            catch (Exception ex)
            {
                res.Errors.Add($"Employee Row {r}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return res;
    }

    public async Task<ImportResult> ImportItemsAsync(Stream excel, int companyId, bool overwrite)
    {
        var companyProp = RequireProp<Item>("CompanyId");
        var codeProp = RequireProp<Item>("ItemCode", "ItemNo", "Code", "SKU");
        var nameProp = RequireProp<Item>("ItemName", "Name", "Description", "Item");

        var unitProp = FindProp<Item>("Unit", "Uom", "UOM");
        var saleProp = FindProp<Item>("SalePrice", "SellingPrice", "Price", "Rate");
        var purProp = FindProp<Item>("PurchasePrice", "CostPrice", "Cost", "PurchaseRate");
        var taxProp = FindProp<Item>("TaxPercent", "VATPercent", "Vat", "Tax", "VatRate");
        var activeProp = FindProp<Item>("IsActive", "Active", "IsEnabled", "Enabled");

        var res = new ImportResult();

        using var wb = new XLWorkbook(excel);
        var ws = wb.Worksheets.FirstOrDefault() ?? throw new Exception("Excel sheet not found.");
        var header = ReadHeaderMap(ws);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            res.TotalRows++;

            var itemCode = Get(ws, r, header, "ItemCode", "ItemNo", "Code", "SKU").Trim();
            var itemName = Get(ws, r, header, "ItemName", "Name", "Description", "Item").Trim();

            if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(itemName))
            {
                res.Skipped++;
                continue;
            }

            try
            {
                var existing = await _db.Items.FirstOrDefaultAsync(x =>
                    EF.Property<int>(x, companyProp) == companyId &&
                    EF.Property<string>(x, codeProp) == itemCode
                );

                if (existing == null)
                {
                    var it = new Item();
                    SetValue(it, companyProp, companyId);
                    SetValue(it, codeProp, itemCode);
                    SetValue(it, nameProp, itemName);

                    SetIfExists(it, unitProp, Get(ws, r, header, "Unit", "Uom", "UOM"));
                    SetIfExists(it, saleProp, Get(ws, r, header, "SalePrice", "SellingPrice", "Rate", "Price"));
                    SetIfExists(it, purProp, Get(ws, r, header, "PurchasePrice", "CostPrice", "Cost", "PurchaseRate"));
                    SetIfExists(it, taxProp, Get(ws, r, header, "TaxPercent", "VATPercent", "Vat", "Tax"));
                    SetIfExists(it, activeProp, Get(ws, r, header, "IsActive", "Active"));

                    _db.Items.Add(it);
                    res.Inserted++;
                }
                else
                {
                    if (!overwrite)
                    {
                        res.Skipped++;
                        continue;
                    }

                    SetValue(existing, nameProp, itemName);
                    SetIfExists(existing, unitProp, Get(ws, r, header, "Unit", "Uom", "UOM"));
                    SetIfExists(existing, saleProp, Get(ws, r, header, "SalePrice", "SellingPrice", "Rate", "Price"));
                    SetIfExists(existing, purProp, Get(ws, r, header, "PurchasePrice", "CostPrice", "Cost", "PurchaseRate"));
                    SetIfExists(existing, taxProp, Get(ws, r, header, "TaxPercent", "VATPercent", "Vat", "Tax"));
                    SetIfExists(existing, activeProp, Get(ws, r, header, "IsActive", "Active"));

                    res.Updated++;
                }
            }
            catch (Exception ex)
            {
                res.Errors.Add($"Item Row {r}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return res;
    }

    public async Task<ImportResult> ImportCustomersAsync(Stream excel, int companyId, bool overwrite)
    {
        var companyProp = RequireProp<Customer>("CompanyId");
        var codeProp = RequireProp<Customer>("CustomerCode", "CustCode", "Code", "PartyCode");
        var nameProp = RequireProp<Customer>("CustomerName", "CustName", "Name", "PartyName");

        var mobileProp = FindProp<Customer>("Mobile", "MobileNo", "Phone", "PhoneNo", "ContactNo");
        var emailProp = FindProp<Customer>("Email", "EmailId", "Mail", "EmailAddress");
        var trnProp = FindProp<Customer>("TRN", "VatNo", "VATNo", "TRNNo");
        var addrProp = FindProp<Customer>("Address", "Addr", "Address1");
        var activeProp = FindProp<Customer>("IsActive", "Active", "IsEnabled", "Enabled");

        var res = new ImportResult();

        using var wb = new XLWorkbook(excel);
        var ws = wb.Worksheets.FirstOrDefault() ?? throw new Exception("Excel sheet not found.");
        var header = ReadHeaderMap(ws);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            res.TotalRows++;

            var code = Get(ws, r, header, "CustomerCode", "CustCode", "Code", "PartyCode").Trim();
            var name = Get(ws, r, header, "CustomerName", "CustName", "Name", "PartyName").Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                res.Skipped++;
                continue;
            }

            try
            {
                var existing = await _db.Customers.FirstOrDefaultAsync(x =>
                    EF.Property<int>(x, companyProp) == companyId &&
                    EF.Property<string>(x, codeProp) == code
                );

                if (existing == null)
                {
                    var c = new Customer();
                    SetValue(c, companyProp, companyId);
                    SetValue(c, codeProp, code);
                    SetValue(c, nameProp, name);

                    SetIfExists(c, mobileProp, Get(ws, r, header, "Mobile", "Phone", "ContactNo"));
                    SetIfExists(c, emailProp, Get(ws, r, header, "Email", "EmailId", "Mail"));
                    SetIfExists(c, trnProp, Get(ws, r, header, "TRN", "VatNo", "VATNo"));
                    SetIfExists(c, addrProp, Get(ws, r, header, "Address", "Addr", "Address1"));
                    SetIfExists(c, activeProp, Get(ws, r, header, "IsActive", "Active"));

                    _db.Customers.Add(c);
                    res.Inserted++;
                }
                else
                {
                    if (!overwrite)
                    {
                        res.Skipped++;
                        continue;
                    }

                    SetValue(existing, nameProp, name);
                    SetIfExists(existing, mobileProp, Get(ws, r, header, "Mobile", "Phone", "ContactNo"));
                    SetIfExists(existing, emailProp, Get(ws, r, header, "Email", "EmailId", "Mail"));
                    SetIfExists(existing, trnProp, Get(ws, r, header, "TRN", "VatNo", "VATNo"));
                    SetIfExists(existing, addrProp, Get(ws, r, header, "Address", "Addr", "Address1"));
                    SetIfExists(existing, activeProp, Get(ws, r, header, "IsActive", "Active"));

                    res.Updated++;
                }
            }
            catch (Exception ex)
            {
                res.Errors.Add($"Customer Row {r}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return res;
    }

    // =========================
    // HELPERS
    // =========================

    private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.Row(1);

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            var name = headerRow.Cell(c).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name) && !map.ContainsKey(name))
                map[name] = c;
        }
        return map;
    }

    private static string Get(IXLWorksheet ws, int row, Dictionary<string, int> header, params string[] names)
    {
        foreach (var n in names)
        {
            if (header.TryGetValue(n, out var col))
                return ws.Cell(row, col).GetString();
        }
        return "";
    }

    private static string RequireProp<T>(params string[] candidates)
    {
        var p = FindProp<T>(candidates);
        if (p == null)
            throw new Exception($"{typeof(T).Name} model missing required property: {string.Join(" / ", candidates)}");
        return p;
    }

    private static string? FindProp<T>(params string[] candidates)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var c in candidates)
        {
            var p = props.FirstOrDefault(x => string.Equals(x.Name, c, StringComparison.OrdinalIgnoreCase));
            if (p != null) return p.Name;
        }
        return null;
    }

    private static void SetIfExists(object obj, string? propName, string raw)
    {
        if (string.IsNullOrWhiteSpace(propName)) return;
        if (raw == null) raw = "";
        SetValue(obj, propName, raw);
    }

    private static void SetValue(object obj, string propName, object? rawValue)
    {
        var prop = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null || !prop.CanWrite) return;

        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        object? value = null;

        if (targetType == typeof(string))
        {
            value = (rawValue?.ToString() ?? "").Trim();
        }
        else if (targetType == typeof(int))
        {
            int.TryParse((rawValue?.ToString() ?? "").Trim(), out var n);
            value = n;
        }
        else if (targetType == typeof(decimal))
        {
            decimal.TryParse((rawValue?.ToString() ?? "").Trim(), out var d);
            value = d;
        }
        else if (targetType == typeof(bool))
        {
            var s = (rawValue?.ToString() ?? "").Trim().ToLowerInvariant();
            value = s is "1" or "true" or "yes" or "y";
        }
        else
        {
            // fallback
            try { value = Convert.ChangeType(rawValue, targetType); }
            catch { return; }
        }

        prop.SetValue(obj, value);
    }
}
