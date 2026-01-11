using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UaeEInvoice.Data;

namespace UaeEInvoice.Services;

public sealed class CashForecastService
{
    private readonly AppDbContext _db;

    public CashForecastService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ForecastResult> BuildAsync(
        int companyId,
        DateTime fromDate,
        int days,
        IReadOnlyList<ForecastWhatIfItem>? whatIfItems = null,
        int lookbackDays = 90,
        IReadOnlyList<int>? cashAccounts = null,
        IReadOnlyList<int>? bankAccounts = null)
    {
        if (days <= 0) days = 30;
        if (days > 365) days = 365;

        if (lookbackDays <= 0) lookbackDays = 30;
        if (lookbackDays > 365) lookbackDays = 365;

        fromDate = fromDate.Date;
        var toDate = fromDate.AddDays(days - 1).Date;

        // ✅ Your confirmed COA accounts
        var cashAcc = (cashAccounts?.ToList() ?? new List<int> { 1000 });
        var bankAcc = (bankAccounts?.ToList() ?? new List<int> { 1010 });

        var liquidAccounts = cashAcc.Concat(bankAcc).Distinct().ToHashSet();

        // 1) Opening cash before start date
        var openingRows = await _db.Set<GeneralLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate < fromDate)
            .Where(x => liquidAccounts.Contains(x.DebitAccountNo) || liquidAccounts.Contains(x.CreditAccountNo))
            .ToListAsync();

        var openingCash = ComputeNetCash(openingRows, liquidAccounts);

        // 2) History for averages (lookback window)
        var lookbackFrom = fromDate.AddDays(-lookbackDays);

        var histRows = await _db.Set<GeneralLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.TxnDate >= lookbackFrom && x.TxnDate < fromDate)
            .Where(x => liquidAccounts.Contains(x.DebitAccountNo) || liquidAccounts.Contains(x.CreditAccountNo))
            .Select(x => new
            {
                Date = x.TxnDate.Date,
                x.DebitAccountNo,
                x.CreditAccountNo,
                x.Amount
            })
            .ToListAsync();

        // ✅ IMPORTANT FIX:
        // Total In/Out ÷ calendar days (lookbackDays), NOT average over only "transaction days"
        decimal totalIn = 0m;
        decimal totalOut = 0m;

        foreach (var r in histRows)
        {
            var debitIsLiquid = liquidAccounts.Contains(r.DebitAccountNo);
            var creditIsLiquid = liquidAccounts.Contains(r.CreditAccountNo);

            // Ignore transfers between cash/bank
            if (debitIsLiquid && creditIsLiquid) continue;

            if (debitIsLiquid && !creditIsLiquid) totalIn += r.Amount;
            else if (creditIsLiquid && !debitIsLiquid) totalOut += r.Amount;
        }

        var denomDays = Math.Max(1, (fromDate - lookbackFrom).Days); // usually = lookbackDays
        var avgIn = totalIn / denomDays;
        var avgOut = totalOut / denomDays;

        // 3) Load recurring rules (reflection-safe)
        var rawRules = await _db.Set<RecurringCashRule>()
            .AsNoTracking()
            .ToListAsync();

        var rules = rawRules
            .Where(r => GetInt(r, 0, "CompanyId", "CompId", "Company") == companyId)
            .Where(r => GetBool(r, true, "IsActive", "Active", "Enabled"))
            .ToList();

        // 4) What-if items
        var wi = (whatIfItems ?? Array.Empty<ForecastWhatIfItem>())
            .Select(x => new ForecastWhatIfItem
            {
                Date = x.Date.Date,
                Amount = x.Amount,
                Direction = (x.Direction ?? "OUT").ToUpperInvariant() == "IN" ? "IN" : "OUT",
                Label = x.Label ?? ""
            })
            .Where(x => x.Amount != 0)
            .ToList();

        // Fast lookup by date
        var wiByDate = wi
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 5) Build forecast result
        var result = new ForecastResult
        {
            CompanyId = companyId,
            FromDate = fromDate,
            Days = days,
            LookbackDays = lookbackDays,
            OpeningCash = openingCash,
            CashAccounts = cashAcc,
            BankAccounts = bankAcc,
            GlRowsUsed = openingRows.Count + histRows.Count,
            RulesUsed = rules.Count,
            WhatIfItems = wi
        };

        decimal running = openingCash;

        for (int i = 0; i < days; i++)
        {
            var d = fromDate.AddDays(i);

            var day = new ForecastDay
            {
                Date = d,
                BaseIn = avgIn,
                BaseOut = avgOut
            };

            // Apply recurring rules
            foreach (var rule in rules)
            {
                if (!RuleAppliesOnDate(rule, d)) continue;

                var dir = GetString(rule, "OUT", "Direction", "InOut", "Type", "DrCr").ToUpperInvariant();
                var amt = GetDecimal(rule, 0m, "Amount", "Amt", "Value");
                var label = GetString(rule, "Recurring", "Label", "Name", "Title", "Description");

                if (amt == 0) continue;

                if (dir == "IN")
                {
                    day.RecurringIn += amt;
                    day.Notes.Add($"Recurring IN: {label} ({amt:0.00})");
                }
                else
                {
                    day.RecurringOut += amt;
                    day.Notes.Add($"Recurring OUT: {label} ({amt:0.00})");
                }
            }

            // Apply what-if
            if (wiByDate.TryGetValue(d, out var items))
            {
                foreach (var item in items)
                {
                    if (item.Direction == "IN")
                    {
                        day.WhatIfIn += item.Amount;
                        day.Notes.Add($"What-If IN: {item.Label} ({item.Amount:0.00})");
                    }
                    else
                    {
                        day.WhatIfOut += item.Amount;
                        day.Notes.Add($"What-If OUT: {item.Label} ({item.Amount:0.00})");
                    }
                }
            }

            running += day.Net;
            day.RunningCash = running;

            result.ForecastCashIn += day.TotalIn;
            result.ForecastCashOut += day.TotalOut;

            result.RecurringNet += (day.RecurringIn - day.RecurringOut);
            result.WhatIfNet += (day.WhatIfIn - day.WhatIfOut);

            result.DaysList.Add(day);
        }

        return result;
    }

    private static decimal ComputeNetCash(IEnumerable<GeneralLedgerEntry> rows, HashSet<int> liquidAccounts)
    {
        decimal net = 0m;

        foreach (var x in rows)
        {
            var debitIsLiquid = liquidAccounts.Contains(x.DebitAccountNo);
            var creditIsLiquid = liquidAccounts.Contains(x.CreditAccountNo);

            // transfer cash<->bank => ignore
            if (debitIsLiquid && creditIsLiquid) continue;

            if (debitIsLiquid && !creditIsLiquid) net += x.Amount;      // cash/bank increased
            else if (creditIsLiquid && !debitIsLiquid) net -= x.Amount; // cash/bank decreased
        }

        return net;
    }

    // ----------------------------
    // Recurring rules - reflection safe
    // Works with your UI: Name, Direction, Amount, Frequency, NextDate
    // ----------------------------
    private static bool RuleAppliesOnDate(RecurringCashRule rule, DateTime date)
    {
        var next = GetDate(rule, null,
            "NextDate", "NextRunDate", "Next", "StartDate", "FromDate", "Date");

        if (next == null) return false;

        var start = next.Value.Date;
        if (date.Date < start) return false;

        var freq = GetString(rule, "MONTHLY", "Frequency", "Freq", "Repeat", "RuleType")
            .Trim()
            .ToUpperInvariant();

        // normalize
        if (freq.Contains("MONTH")) freq = "MONTHLY";
        else if (freq.Contains("WEEK")) freq = "WEEKLY";
        else if (freq.Contains("DAY")) freq = "DAILY";
        else if (freq.Contains("YEAR")) freq = "YEARLY";
        else if (freq.Contains("QUART")) freq = "QUARTERLY";

        switch (freq)
        {
            case "DAILY":
                return true;

            case "WEEKLY":
                return date.DayOfWeek == start.DayOfWeek;

            case "MONTHLY":
                return date.Day == start.Day;

            case "QUARTERLY":
                if (date.Day != start.Day) return false;
                var months = (date.Year - start.Year) * 12 + (date.Month - start.Month);
                return months % 3 == 0;

            case "YEARLY":
                return date.Month == start.Month && date.Day == start.Day;

            default:
                // if unknown, treat as monthly
                return date.Day == start.Day;
        }
    }

    private static PropertyInfo? FindProp(object obj, params string[] names)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null) return p;
        }
        return null;
    }

    private static string GetString(object obj, string fallback, params string[] names)
    {
        var p = FindProp(obj, names);
        if (p == null) return fallback;
        var v = p.GetValue(obj);
        return v?.ToString() ?? fallback;
    }

    private static int GetInt(object obj, int fallback, params string[] names)
    {
        var p = FindProp(obj, names);
        if (p == null) return fallback;
        var v = p.GetValue(obj);
        if (v == null) return fallback;
        if (v is int i) return i;
        if (int.TryParse(v.ToString(), out var x)) return x;
        return fallback;
    }

    private static decimal GetDecimal(object obj, decimal fallback, params string[] names)
    {
        var p = FindProp(obj, names);
        if (p == null) return fallback;
        var v = p.GetValue(obj);
        if (v == null) return fallback;
        if (v is decimal d) return d;
        if (v is double dd) return (decimal)dd;
        if (v is float ff) return (decimal)ff;
        if (decimal.TryParse(v.ToString(), out var x)) return x;
        return fallback;
    }

    private static bool GetBool(object obj, bool fallback, params string[] names)
    {
        var p = FindProp(obj, names);
        if (p == null) return fallback;
        var v = p.GetValue(obj);
        if (v == null) return fallback;
        if (v is bool b) return b;
        if (bool.TryParse(v.ToString(), out var x)) return x;
        return fallback;
    }

    private static DateTime? GetDate(object obj, DateTime? fallback, params string[] names)
    {
        var p = FindProp(obj, names);
        if (p == null) return fallback;
        var v = p.GetValue(obj);
        if (v == null) return fallback;
        if (v is DateTime dt) return dt;
        if (DateTime.TryParse(v.ToString(), out var x)) return x;
        return fallback;
    }
}
