using System;

namespace Kawin_67091861_002.Models;

public class AccountingTransaction
{
    public int TransactionId { get; set; }
    public string Type { get; set; } = "Income"; // Income or Expense
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    public string ReferenceNo { get; set; } = "";
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "Head Accountant";
}

