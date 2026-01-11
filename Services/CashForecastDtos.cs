using System;
using System.Collections.Generic;
using System.Linq;

namespace UaeEInvoice.Services;

public sealed class ForecastWhatIfItem
{
    public DateTime Date { get; set; }
    public string Direction { get; set; } = "OUT"; // IN / OUT
    public decimal Amount { get; set; }
    public string Label { get; set; } = "";
}

public sealed class ForecastDay
{
    public DateTime Date { get; set; }

    public decimal BaseIn { get; set; }
    public decimal BaseOut { get; set; }

    public decimal RecurringIn { get; set; }
    public decimal RecurringOut { get; set; }

    public decimal WhatIfIn { get; set; }
    public decimal WhatIfOut { get; set; }

    public decimal TotalIn => BaseIn + RecurringIn + WhatIfIn;
    public decimal TotalOut => BaseOut + RecurringOut + WhatIfOut;

    public decimal Net => TotalIn - TotalOut;

    public decimal RunningCash { get; set; }

    public List<string> Notes { get; set; } = new();

    public string NotesText => Notes.Count == 0 ? "-" : string.Join("  •  ", Notes);
}

public sealed class ForecastResult
{
    public int CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public int Days { get; set; }
    public int LookbackDays { get; set; }

    public List<int> CashAccounts { get; set; } = new();
    public List<int> BankAccounts { get; set; } = new();

    public decimal OpeningCash { get; set; }
    public decimal ForecastCashIn { get; set; }
    public decimal ForecastCashOut { get; set; }

    public decimal ForecastNet => ForecastCashIn - ForecastCashOut;
    public decimal ClosingCash => OpeningCash + ForecastNet;

    public decimal RecurringNet { get; set; }
    public decimal WhatIfNet { get; set; }

    public int GlRowsUsed { get; set; }
    public int RulesUsed { get; set; }

    public List<ForecastWhatIfItem> WhatIfItems { get; set; } = new();
    public List<ForecastDay> DaysList { get; set; } = new();

    public DateTime ToDate => FromDate.AddDays(Math.Max(1, Days) - 1).Date;
}
