using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.ViewModels;

// ==========================================
// 1. Daily Overview ViewModel (ภาพรวมประจำวัน)
// ==========================================
public class DailyOverviewViewModel
{
    public decimal TodayRevenue { get; set; } = 248500;
    public string RevenueChangeText { get; set; } = "+12.4% จากเมื่อวาน";

    public int TodayCustomers { get; set; } = 1247;
    public string CustomersChangeText { get; set; } = "เพิ่มขึ้น 155 คนช่วงบ่าย";

    public int TodayBookings { get; set; } = 892;
    public string BookingsSubtext { get; set; } = "ผ่านระบบออนไลน์ 73%";

    public double AttendanceRate { get; set; } = 87.3;
    public string AttendanceChangeText { get; set; } = "สูงกว่าค่าเฉลี่ย 5.5 จุดร้อยละ";
    public string AttendanceSubtext { get => AttendanceChangeText; set => AttendanceChangeText = value; }

    // Weekly Revenue Trend (Last 7 Days)
    public List<DayRevenueItem> WeeklyRevenueTrend { get; set; } = new();

    // Revenue by Category (สัดส่วนรายรับวันนี้)
    public CategoryRevenueBreakdown CategoryBreakdown { get; set; } = new();

    // Top Grossing Shows Today (ภาพยนตร์ทำรายได้สูงสุดวันนี้)
    public List<TopGrossingMovieItem> TopGrossingMovies { get; set; } = new();
}

public class DayRevenueItem
{
    public string DayName { get; set; } = ""; // จันทร์, อังคาร, ...
    public decimal Revenue { get; set; }
    public string FormattedRevenue => Revenue.ToString("N0");
}

public class CategoryRevenueBreakdown
{
    public decimal TotalRevenue { get; set; }
    public string CenterDisplay { get; set; } = "฿248.5K";

    public decimal TicketsAmount { get; set; } = 161525;
    public double TicketsPercent { get; set; } = 65;

    public decimal ConcessionsAmount { get; set; } = 62325;
    public double ConcessionsPercent { get; set; } = 25;

    public decimal MerchandiseAmount { get; set; } = 24650;
    public double MerchandisePercent { get; set; } = 10;
}

public class TopGrossingMovieItem
{
    public int Rank { get; set; }
    public string MovieTitle { get; set; } = "";
    public int ShowtimesCount { get; set; }
    public decimal NetGross { get; set; }
}

// ==========================================
// 2. Income & Expenses ViewModel (รายรับ-รายจ่าย)
// ==========================================
public class IncomeExpensesViewModel
{
    public decimal NetProfit { get; set; } = 95900;
    public string NetProfitFormatted => (NetProfit >= 0 ? "+" : "-") + "฿" + Math.Abs(NetProfit).ToString("N0");
    public string MarginStatus { get; set; } = "✓ ดีเยี่ยม (Healthy Margin)";

    public decimal TotalIncome { get; set; } = 263500;
    public decimal TotalExpenses { get; set; } = 167600;

    public List<FinancialLineItem> IncomeBreakdown { get; set; } = new();
    public List<FinancialLineItem> ExpenseBreakdown { get; set; } = new();

    // Income vs Expenses past 30 days (4 weeks)
    public List<WeeklyFinancialItem> MonthlyComparison { get; set; } = new();
}

public class FinancialLineItem
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public decimal Amount { get; set; }
    public string AmountFormatted => "฿" + Amount.ToString("N0");
    public bool IsExpense { get; set; }
}

public class WeeklyFinancialItem
{
    public string WeekLabel { get; set; } = ""; // สัปดาห์ 1, สัปดาห์ 2, ...
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
}

// ==========================================
// 3. Invoices & Receipts ViewModel (ใบแจ้งหนี้และใบเสร็จ)
// ==========================================
public class InvoicesListViewModel
{
    public string ActiveTab { get; set; } = "invoices"; // invoices or receipts
    public string SearchQuery { get; set; } = "";
    public string StatusFilter { get; set; } = "all";
    public List<InvoiceItemDto> Invoices { get; set; } = new();
    public List<ReceiptItemDto> Receipts { get; set; } = new();
    public decimal TotalInvoicedAmount { get; set; }
    public int TotalInvoicesCount { get; set; }
}

public class InvoiceItemDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = ""; // e.g. INV-2026-0089
    public string DateText { get; set; } = ""; // e.g. 24/02/2026
    public DateTime Date { get; set; } = DateTime.Now;
    public string Party { get; set; } = ""; // ผู้จ่าย/ผู้รับ
    public string Description { get; set; } = ""; // รายการ
    public decimal Amount { get; set; } // จำนวนเงิน
    public string Status { get; set; } = "รอชำระ"; // รอชำระ, ชำระแล้ว, เกินกำหนด
}

public class ReceiptItemDto
{
    public int PaymentId { get; set; }
    public int BookingId { get; set; }
    public string ReceiptNo { get; set; } = ""; // REC-2026-XXXX
    public string DateText { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public string CustomerName { get; set; } = "";
    public string MovieTitle { get; set; } = "";
    public string CinemaName { get; set; } = "";
    public string Seats { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "PromptPay";
    public string Status { get; set; } = "ชำระแล้ว"; // ชำระแล้ว, คืนเงินแล้ว
    public bool IsRefunded { get; set; }
}

public class CreateInvoiceDto
{
    public string InvoiceNo { get; set; } = "";
    public string Party { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "รอชำระ";
    public string? DateText { get; set; }
}

// ==========================================
// 4. Organization Chat ViewModel (แชทในองค์กร)
// ==========================================
public class OrganizationChatViewModel
{
    public string ActiveChannelId { get; set; } = "accounting";
    public string ActiveChannelName { get; set; } = "ฝ่ายบัญชี (Accounting)";
    public string PinnedNotice { get; set; } = "ส่งสรุปรายงานภาษีมูลค่าเพิ่ม ภ.พ.30 ประจำเดือนกุมภาพันธ์ ภายในวันที่ 5 มีนาคมนี้";
    public List<ChatChannelDto> Channels { get; set; } = new();
    public List<ChatContactDto> DirectMessages { get; set; } = new();
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatChannelDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int UnreadCount { get; set; }
    public bool IsActive { get; set; }
}

public class ChatContactDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsOnline { get; set; } = true;
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public string ChannelId { get; set; } = "accounting";
    public string SenderName { get; set; } = "";
    public string TimeText { get; set; } = "";
    public string MessageText { get; set; } = "";
    public bool IsMine { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ==========================================
// 5. Refund Requests ViewModel (คืนเงิน)
// ==========================================
public class RefundRequestsViewModel
{
    public string PolicyTitle { get; set; } = "นโยบายสิทธิ์การคืนเงิน (Refund Policy)";
    public string PolicyRuleHighlight { get; set; } = "ยกเลิกก่อนฉาย 5 ชั่วโมง — คืนเต็มจำนวน";
    public string PolicyRuleNote { get; set; } = "(หากขอยกเลิกน้อยกว่า 5 ชั่วโมงก่อนภาพยนตร์ฉาย ระบบจะขึ้นสถานะปฏิเสธอัตโนมัติ)";

    public int TodayRequestsCount { get; set; } = 24;
    public int CompletedCount { get; set; } = 21;
    public decimal TotalRefundAmount { get; set; } = 5460;
    public string RefundPercentOfRevenue { get; set; } = "คิดเป็น 2.1% ของรายรับรวม";
    public double CancellationRate { get; set; } = 1.8;
    public string CancellationRateSubtext { get; set; } = "อยู่ในเกณฑ์ปกติ (ต่ำกว่า 3%)";

    public List<RefundItemDto> RefundTransactions { get; set; } = new();
    public RefundItemDto? SelectedRequest { get; set; }
}

public class RefundItemDto
{
    public string RequestId { get; set; } = ""; // REF-4001
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string MovieTitle { get; set; } = "";
    public string CinemaName { get; set; } = "";
    public string SeatNumbers { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "รอดำเนินการ"; // อนุมัติแล้ว, รอดำเนินการ, ปฏิเสธ
    public bool IsConfirmedByCustomer { get; set; }
    public string? ConfirmedAtText { get; set; }

    // Detailed Review Info
    public string BookingCode { get; set; } = ""; // BK-89115
    public string ShowtimeText { get; set; } = ""; // 24/02/2026 เวลา 21:00 น.
    public string CancellationTimeText { get; set; } = ""; // 24/02/2026 เวลา 14:00 น. (ก่อนฉาย 7 ชั่วโมง)
    public string RefundAmountText { get; set; } = ""; // 160.00 (คืนเต็มจำนวน)
    public bool CanApproveOrReject => Status == "รอดำเนินการ";
}

// ==========================================
// 6. Add Transaction Form DTO
// ==========================================
public class AddTransactionDto
{
    public string Type { get; set; } = "Income"; // Income or Expense
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
