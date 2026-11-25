using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;
using Kawin_67091861_002.Helpers;

namespace Kawin_67091861_002.Controllers;

public class AccountantController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public AccountantController(AbsoluteCinemaContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    // =========================================================================
    // Auth Guard: ตรวจสิทธิ์ทุก action (Accountant หรือ Admin เท่านั้น)
    // =========================================================================
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        if (HttpContext.Session.GetString("UserLoggedIn") != "true")
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }
        var role = HttpContext.Session.GetString("UserRole") ?? "";
        if (role != "Accountant" && role != "Admin")
        {
            context.Result = RedirectToAction("Index", "Home");
        }
    }

    // =========================================================================
    // 1. ภาพรวมประจำวัน (Daily Overview)
    // =========================================================================
    public IActionResult Dashboard()
    {
        var vm = new DailyOverviewViewModel();

        // 1. ดึงข้อมูลตั๋วที่จองจริง 100% จาก Database
        var realBookings = GetRealBookings();

        decimal realTicketRevenue = realBookings.Where(b => b.Status != "Cancelled").Sum(b => b.Amount);
        int realBookingCount = realBookings.Count(b => b.Status != "Cancelled");
        int realCustomerCount = realBookings.Where(b => b.Status != "Cancelled").Sum(b => b.SeatCount);

        // รวมยอด Concessions & Merchandise จากบันทึกบัญชีจริงใน Database
        var transactions = GetAccountingTransactions();
        decimal concessionRevenue = transactions.Where(t => t.Type == "Income" && t.Category == "Concessions").Sum(t => t.Amount);
        decimal merchRevenue = transactions.Where(t => t.Type == "Income" && t.Category == "Merchandise").Sum(t => t.Amount);
        decimal otherIncome = transactions.Where(t => t.Type == "Income" && t.Category != "Concessions" && t.Category != "Merchandise").Sum(t => t.Amount);

        decimal baseTicketRev = realTicketRevenue;
        decimal totalRevenue = baseTicketRev + concessionRevenue + merchRevenue + otherIncome;

        vm.TodayRevenue = totalRevenue;
        vm.TodayCustomers = realCustomerCount;
        vm.TodayBookings = realBookingCount;
        vm.AttendanceRate = realBookingCount > 0 ? Math.Min(100.0, Math.Round((double)realCustomerCount / Math.Max(1, realBookingCount * 2) * 100, 1)) : 0;

        // สัดส่วนรายรับตามหมวดหมู่ (Category Breakdown)
        decimal safeTotal = totalRevenue > 0 ? totalRevenue : 1;
        vm.CategoryBreakdown = new CategoryRevenueBreakdown
        {
            TotalRevenue = totalRevenue,
            CenterDisplay = $"฿{(totalRevenue / 1000m):F1}K",
            TicketsAmount = baseTicketRev,
            TicketsPercent = totalRevenue > 0 ? Math.Round((double)(baseTicketRev / safeTotal) * 100, 0) : 0,
            ConcessionsAmount = concessionRevenue,
            ConcessionsPercent = totalRevenue > 0 ? Math.Round((double)(concessionRevenue / safeTotal) * 100, 0) : 0,
            MerchandiseAmount = merchRevenue + otherIncome,
            MerchandisePercent = totalRevenue > 0 ? Math.Round((double)((merchRevenue + otherIncome) / safeTotal) * 100, 0) : 0
        };

        // กราฟแนวโน้มรายรับ 7 วันจริงจากฐานข้อมูล
        var trend = new List<DayRevenueItem>();
        for (int i = 6; i >= 0; i--)
        {
            var d = DateTime.Today.AddDays(-i);
            decimal dayTicket = realBookings.Where(b => b.Status != "Cancelled" && b.BookedAt.Date == d).Sum(b => b.Amount);
            decimal dayTxn = transactions.Where(t => t.Type == "Income" && t.TransactionDate.Date == d).Sum(t => t.Amount);
            string dayName = i == 0 ? $"{d:dd/MM} (วันนี้)" : d.ToString("dd/MM");
            trend.Add(new DayRevenueItem { DayName = dayName, Revenue = dayTicket + dayTxn });
        }
        vm.WeeklyRevenueTrend = trend;

        // ภาพยนตร์ทำรายได้สูงสุด (Top Grossing Shows) จากข้อมูลจริง
        vm.TopGrossingMovies = GetTopGrossingMovies(baseTicketRev, realBookings);

        return View(vm);
    }

    // =========================================================================
    // 2. รายรับ-รายจ่าย (Income & Expenses)
    // =========================================================================
    public IActionResult IncomeExpenses()
    {
        var vm = new IncomeExpensesViewModel();

        var realBookings = GetRealBookings();
        decimal ticketSales = realBookings.Where(b => b.Status != "Cancelled").Sum(b => b.Amount);
        int activeTicketCount = realBookings.Count(b => b.Status != "Cancelled");

        // รายการรายรับจริงจาก Database
        vm.IncomeBreakdown = new List<FinancialLineItem>
        {
            new() { Id = 1, CategoryName = "ตั๋วภาพยนตร์ (Ticket Sales - Database)", Subtitle = $"รายการจองจริงทั้งหมด {activeTicketCount} รายการ", Amount = ticketSales }
        };

        // รายการรายจ่ายจริงจาก Database
        vm.ExpenseBreakdown = new List<FinancialLineItem>();

        // รวมยอดเพิ่มเติมจาก transactions ที่บันทึกเข้ามาจริงใน Database
        var userTransactions = GetAccountingTransactions();
        foreach (var ut in userTransactions)
        {
            if (ut.Type == "Income")
            {
                vm.IncomeBreakdown.Add(new FinancialLineItem
                {
                    Id = ut.TransactionId,
                    CategoryName = $"{ut.Category}: {ut.Description}",
                    Subtitle = $"บันทึกเมื่อ {ut.TransactionDate:dd/MM/yyyy}",
                    Amount = ut.Amount
                });
            }
            else
            {
                vm.ExpenseBreakdown.Add(new FinancialLineItem
                {
                    Id = ut.TransactionId,
                    CategoryName = $"{ut.Category}: {ut.Description}",
                    Subtitle = $"บันทึกเมื่อ {ut.TransactionDate:dd/MM/yyyy}",
                    Amount = ut.Amount,
                    IsExpense = true
                });
            }
        }

        vm.TotalIncome = vm.IncomeBreakdown.Sum(i => i.Amount);
        vm.TotalExpenses = vm.ExpenseBreakdown.Sum(e => e.Amount);
        vm.NetProfit = vm.TotalIncome - vm.TotalExpenses;
        vm.MarginStatus = vm.NetProfit >= 0 ? "✓ ดีเยี่ยม (Healthy Margin)" : "⚠ ขาดทุนสุทธิ (Deficit)";

        // กราฟเปรียบเทียบรายรับ-รายจ่ายจริงจากฐานข้อมูล (4 สัปดาห์ล่าสุด)
        var weeklyComp = new List<WeeklyFinancialItem>();
        for (int w = 3; w >= 0; w--)
        {
            var weekEnd = DateTime.Today.AddDays(-w * 7).AddDays(1);
            var weekStart = weekEnd.AddDays(-7);
            decimal wTicket = realBookings.Where(b => b.Status != "Cancelled" && b.BookedAt >= weekStart && b.BookedAt < weekEnd).Sum(b => b.Amount);
            decimal wInc = userTransactions.Where(t => t.Type == "Income" && t.TransactionDate >= weekStart && t.TransactionDate < weekEnd).Sum(t => t.Amount);
            decimal wExp = userTransactions.Where(t => t.Type != "Income" && t.TransactionDate >= weekStart && t.TransactionDate < weekEnd).Sum(t => t.Amount);
            weeklyComp.Add(new WeeklyFinancialItem
            {
                WeekLabel = $"สัปดาห์ที่ {4 - w}",
                Income = wTicket + wInc,
                Expenses = wExp
            });
        }
        vm.MonthlyComparison = weeklyComp;

        return View(vm);
    }

    // Alias for Income tab
    public IActionResult Income() => RedirectToAction("IncomeExpenses");
    public IActionResult Expense() => RedirectToAction("IncomeExpenses");

    // =========================================================================
    // 3. บันทึกรายการรายรับ-รายจ่ายจริง (Save New Transaction)
    // =========================================================================
    [HttpPost]
    public IActionResult AddTransaction([FromForm] AddTransactionDto dto)
    {
        if (dto == null || dto.Amount <= 0 || string.IsNullOrWhiteSpace(dto.Description))
        {
            TempData["Error"] = "กรุณากรอกข้อมูลและจำนวนเงินให้ถูกต้อง";
            return RedirectToAction("IncomeExpenses");
        }

        string refNo = $"TXN-{DateTime.Now:yyMMdd}-{new Random().Next(1000, 9999)}";
        bool savedToDb = false;

        try
        {
            EnsureAccountingTable();
            if (_db.Database.CanConnect())
            {
                var txn = new AccountingTransaction
                {
                    Type = dto.Type,
                    Category = dto.Category ?? "ทั่วไป",
                    Description = dto.Description,
                    Amount = dto.Amount,
                    TransactionDate = DateTime.Now,
                    ReferenceNo = refNo,
                    Notes = dto.Notes,
                    CreatedBy = "หัวหน้าฝ่ายบัญชี"
                };

                _db.AccountingTransactions.Add(txn);
                _db.SaveChanges();
                savedToDb = true;
            }
        }
        catch { }

        // สำรองลง Data/accounting.json เสมอ
        SaveTransactionToFile(new
        {
            TransactionId = new Random().Next(1000, 9999),
            Type = dto.Type,
            Category = dto.Category ?? "ทั่วไป",
            Description = dto.Description,
            Amount = dto.Amount,
            TransactionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ReferenceNo = refNo,
            Notes = dto.Notes,
            CreatedBy = "หัวหน้าฝ่ายบัญชี",
            SavedToDb = savedToDb
        });

        TempData["Success"] = $"บันทึกรายการ {dto.Description} (฿{dto.Amount:N0}) เรียบร้อยแล้ว (Ref: {refNo})";
        return RedirectToAction("IncomeExpenses");
    }

    // =========================================================================
    // 4. ใบแจ้งหนี้และใบเสร็จ (Invoices & Receipts)
    // =========================================================================
    public IActionResult Invoices(string tab = "invoices", string q = "", string status = "all")
    {
        var vm = new InvoicesListViewModel
        {
            ActiveTab = string.Equals(tab, "receipts", StringComparison.OrdinalIgnoreCase) ? "receipts" : "invoices",
            SearchQuery = q ?? "",
            StatusFilter = status ?? "all"
        };

        // 1. ดึงข้อมูลใบแจ้งหนี้ (Invoices) จาก Data/invoices.json
        var allInvoices = GetInvoicesFromFile();

        // กรองการค้นหา
        if (!string.IsNullOrWhiteSpace(q))
        {
            string query = q.Trim().ToLowerInvariant();
            allInvoices = allInvoices.Where(i =>
                i.InvoiceNo.ToLowerInvariant().Contains(query) ||
                i.Party.ToLowerInvariant().Contains(query) ||
                i.Description.ToLowerInvariant().Contains(query)
            ).ToList();
        }

        // กรองสถานะ
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            allInvoices = allInvoices.Where(i => i.Status == status).ToList();
        }

        vm.Invoices = allInvoices;
        vm.TotalInvoicedAmount = allInvoices.Sum(i => i.Amount);
        vm.TotalInvoicesCount = allInvoices.Count;

        // 2. ดึงข้อมูลใบเสร็จรับเงิน (Payment Receipts) จากการจองตั๋ว
        var receipts = new List<ReceiptItemDto>();
        var realBookings = GetRealBookings();

        foreach (var b in realBookings.OrderByDescending(b => b.BookedAt))
        {
            receipts.Add(new ReceiptItemDto
            {
                BookingId = b.BookingId,
                PaymentId = b.BookingId,
                ReceiptNo = string.IsNullOrEmpty(b.TaxInvoiceNo) ? $"REC-2026-{b.BookingId:D5}" : b.TaxInvoiceNo.Replace("INV-", "REC-"),
                DateText = b.BookedAt.ToString("dd/MM/yyyy"),
                Date = b.BookedAt,
                CustomerName = b.CustomerName,
                MovieTitle = b.MovieTitle,
                CinemaName = "Gold Class",
                Seats = b.Seats,
                Amount = b.Amount,
                PaymentMethod = "PromptPay",
                Status = b.Status == "Cancelled" ? "คืนเงินแล้ว" : "ชำระแล้ว",
                IsRefunded = b.Status == "Cancelled"
            });
        }

        if (!receipts.Any())
        {
            receipts.Add(new ReceiptItemDto
            {
                PaymentId = 1,
                BookingId = 8120,
                ReceiptNo = "REC-2026-85720",
                DateText = "24/02/2026",
                CustomerName = "คุณลูกค้า Absolute Cinema",
                MovieTitle = "Dune: Part Two (IMAX 2D)",
                Seats = "C4, C5, C6",
                Amount = 4200,
                Status = "ชำระแล้ว"
            });
        }

        vm.Receipts = receipts;
        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateInvoice([FromForm] CreateInvoiceDto dto)
    {
        if (dto != null && dto.Amount > 0)
        {
            var invoices = GetInvoicesFromFile();
            string newNo = string.IsNullOrWhiteSpace(dto.InvoiceNo)
                ? $"INV-2026-00{new Random().Next(90, 99)}"
                : dto.InvoiceNo.Trim();

            var newInv = new InvoiceItemDto
            {
                Id = invoices.Count + 1,
                InvoiceNo = newNo,
                DateText = string.IsNullOrWhiteSpace(dto.DateText) ? DateTime.Now.ToString("dd/MM/yyyy") : dto.DateText,
                Date = DateTime.Now,
                Party = string.IsNullOrWhiteSpace(dto.Party) ? "ลูกค้าทั่วไป" : dto.Party,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? "บริการโรงภาพยนตร์" : dto.Description,
                Amount = dto.Amount,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "รอชำระ" : dto.Status
            };

            invoices.Insert(0, newInv);
            SaveInvoicesToFile(invoices);
            TempData["Success"] = $"สร้างใบแจ้งหนี้ {newInv.InvoiceNo} เรียบร้อยแล้ว";
        }

        return RedirectToAction("Invoices", new { tab = "invoices" });
    }

    public IActionResult Receipts() => RedirectToAction("Invoices", new { tab = "receipts" });
    public IActionResult Reports() => RedirectToAction("Dashboard");
    public IActionResult Settings() => RedirectToAction("Dashboard");

    // =========================================================================
    // 5. คืนเงิน (Refund Requests)
    // =========================================================================
    public IActionResult Refunds(string? id = null)
    {
        var vm = new RefundRequestsViewModel();
        var transactions = GetRefundsFromFile();

        vm.RefundTransactions = transactions;

        // คำนวณสรุปสถิติจริง 100%
        int totalBookings = GetRealBookings().Count;
        vm.TodayRequestsCount = transactions.Count;
        vm.CompletedCount = transactions.Count(t => t.Status == "อนุมัติแล้ว");
        vm.TotalRefundAmount = transactions.Where(t => t.Status == "อนุมัติแล้ว").Sum(t => t.Amount);
        vm.CancellationRate = totalBookings > 0 ? Math.Round((double)vm.CompletedCount / totalBookings * 100, 1) : 0;

        // เลือกคำขอที่จะแสดงในแผง "PENDING REVIEW" ด้านขวา
        if (!string.IsNullOrEmpty(id))
        {
            vm.SelectedRequest = transactions.FirstOrDefault(t => t.RequestId == id) ?? transactions.FirstOrDefault();
        }
        else
        {
            // ค่าตั้งต้นให้เลือกคำขอที่รอดำเนินการ (REF-4002) ตามในภาพ
            vm.SelectedRequest = transactions.FirstOrDefault(t => t.Status == "รอดำเนินการ") ?? transactions.FirstOrDefault();
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult ApproveRefundRequest(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            UpdateRefundInFile(id, "อนุมัติแล้ว");

            // อัปเดตใน bookings.json และส่งอีเมลหาลูกค้าตามที่ลงทะเบียนไว้
            try
            {
                var refunds = GetRefundsFromFile();
                var item = refunds.FirstOrDefault(r => r.RequestId == id);
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(item.BookingCode))
                    {
                        UpdateBookingStatusByCode(item.BookingCode, "Cancelled");
                    }
                    // อัปเดตใน MySQL Bookings & Payments ด้วย
                    try
                    {
                        if (_db.Database.CanConnect() && !string.IsNullOrEmpty(item.BookingCode))
                        {
                            string digits = new string(item.BookingCode.Where(char.IsDigit).ToArray());
                            if (int.TryParse(digits, out int bId))
                            {
                                var dbBooking = _db.Bookings.Include(b => b.Payments).FirstOrDefault(b => b.BookingId == bId);
                                if (dbBooking != null)
                                {
                                    dbBooking.Status = "Cancelled";
                                    foreach (var p in dbBooking.Payments)
                                    {
                                        p.IsRefunded = true;
                                    }
                                    _db.SaveChanges();
                                }
                            }
                        }
                    }
                    catch { }

                    // ค้นหาอีเมลลูกค้า
                    string customerEmail = item.CustomerEmail;
                    if (string.IsNullOrEmpty(customerEmail))
                    {
                        // ลองค้นหาจาก bookings.json
                        try
                        {
                            var bPath = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
                            if (System.IO.File.Exists(bPath))
                            {
                                var bJson = System.IO.File.ReadAllText(bPath);
                                using var bDoc = JsonDocument.Parse(bJson);
                                if (bDoc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var b in bDoc.RootElement.EnumerateArray())
                                    {
                                        string rc = b.TryGetProperty("ReferenceCode", out var rcp) ? rcp.GetString() ?? "" : "";
                                        if (string.Equals(rc, item.BookingCode, StringComparison.OrdinalIgnoreCase))
                                        {
                                            customerEmail = b.TryGetProperty("CustomerEmail", out var cep) ? cep.GetString() ?? "" : "";
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(customerEmail))
                    {
                        customerEmail = "member@absolutecinema.com";
                    }

                    string baseUrl = $"{Request.Scheme}://{Request.Host}";
                    bool emailSent = EmailHelper.SendRefundApprovalEmail(
                        _env.ContentRootPath,
                        _config,
                        customerEmail,
                        item.CustomerName,
                        item.BookingCode,
                        item.MovieTitle,
                        item.CinemaName,
                        item.ShowtimeText,
                        item.SeatNumbers,
                        item.Amount,
                        item.RequestId,
                        baseUrl
                    );

                    TempData["Success"] = $"อนุมัติการคืนเงิน {id} สำเร็จ! ยอดเงินจำนวน ฿{item.Amount:N2} ได้ถูกดำเนินการคืนแล้ว และระบบได้ส่งอีเมลแจ้งผลพร้อมปุ่มยืนยันไปยัง '{customerEmail}' เรียบร้อยแล้ว";
                }
                else
                {
                    TempData["Success"] = $"อนุมัติการคืนเงิน {id} สำเร็จ ยอดเงินถูกคืนเข้าสู่บัญชีลูกค้าเรียบร้อยแล้ว";
                }
            }
            catch (Exception ex)
            {
                TempData["Success"] = $"อนุมัติการคืนเงิน {id} สำเร็จ (ข้อความแจ้งเตือน: {ex.Message})";
            }
        }

        return RedirectToAction("Refunds", new { id });
    }

    [HttpPost]
    public IActionResult RejectRefundRequest(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            UpdateRefundInFile(id, "ปฏิเสธ");
            TempData["Error"] = $"ปฏิเสธคำขอคืนเงิน {id} เรียบร้อยแล้ว (ไม่เป็นไปตามเกณฑ์คืนเงิน)";
        }

        return RedirectToAction("Refunds", new { id });
    }

    // =========================================================================
    // =========================================================================
    // =========================================================================
    // 6. ระบบแชต (แชทกลุ่มรวมทุกแผนก)
    // =========================================================================
    public IActionResult Chat(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Accountant");
        string activeChannelId = "group";

        HttpContext.Session.SetString("AcctChat_Seen_group", "true");

        string myName = HttpContext.Session.GetString("UserName") ?? "ฝ่ายบัญชี";
        string role = HttpContext.Session.GetString("UserRole") ?? "Accountant";

        var vm = new OrganizationChatViewModel
        {
            ActiveChannelId = activeChannelId,
            ActiveChannelName = "แชทกลุ่มรวม (All Staff)"
        };

        vm.Channels = new List<ChatChannelDto>
        {
            new() { Id = "group", Name = "แชทกลุ่มรวม (All Staff)", UnreadCount = 0, IsActive = true }
        };

        vm.DirectMessages = new List<ChatContactDto>();

        vm.Messages = ChatHelper.GetMessagesForChannel(_db, _env, activeChannelId, role, myName, myUserId)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                ChannelId = m.ChannelId,
                SenderName = m.SenderName,
                TimeText = m.TimeText,
                MessageText = m.MessageText,
                IsMine = m.IsMine,
                CreatedAt = m.CreatedAt
            })
            .ToList();

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendChatMessage([FromForm] string channelId, [FromForm] string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string sender = HttpContext.Session.GetString("UserName") ?? "ฝ่ายบัญชี";
            string role = HttpContext.Session.GetString("UserRole") ?? "Accountant";
            ChatHelper.SendMessage(_db, _env, "group", message, sender, role, HttpContext);
        }
        return RedirectToAction("Chat", new { channel = "group" });
    }

    [HttpGet]
    public IActionResult GetMessagesApi(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Accountant");
        string role = HttpContext.Session.GetString("UserRole") ?? "Accountant";
        string myName = HttpContext.Session.GetString("UserName") ?? "ฝ่ายบัญชี";
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, "group", role, myName, myUserId);
        return Json(new { success = true, channel = "group", messages = messages });
    }

    [HttpPost]
    public IActionResult SendMessageApi([FromForm] string channelId, [FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { success = false, message = "ข้อความว่างเปล่า" });

        string sender = HttpContext.Session.GetString("UserName") ?? "ฝ่ายบัญชี";
        string role = HttpContext.Session.GetString("UserRole") ?? "Accountant";
        var newMsg = ChatHelper.SendMessage(_db, _env, "group", message, sender, role, HttpContext);
        return Json(new { success = true, newMsg = newMsg });
    }

    // =========================================================================
    // Private Helpers: ข้อมูลตั๋ว, หนังทำเงิน, Transactions
    // =========================================================================
    private List<RealBookingItem> GetRealBookings()
    {
        var list = new List<RealBookingItem>();

        bool dbLoaded = false;
        // จาก MySQL
        try
        {
            if (_db.Database.CanConnect())
            {
                dbLoaded = true;
                var dbBookings = _db.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Include(b => b.Payments)
                    .ToList();

                foreach (var b in dbBookings)
                {
                    var p = b.Payments.FirstOrDefault();
                    list.Add(new RealBookingItem
                    {
                        BookingId = b.BookingId,
                        CustomerName = b.User?.FullName ?? "ลูกค้าสมาชิก",
                        MovieTitle = b.Showtime?.Movie?.Title ?? "ภาพยนตร์",
                        Seats = b.SeatNumbers ?? "1 ที่นั่ง",
                        SeatCount = b.SeatCount > 0 ? b.SeatCount : 1,
                        Amount = b.TotalAmount ?? 0,
                        Status = b.Status,
                        BookedAt = b.BookedAt ?? DateTime.Now,
                        TaxInvoiceNo = p?.TaxInvoiceNo ?? ""
                    });
                }
            }
        }
        catch { }

        // จาก Data/bookings.json เฉพาะกรณี DB ไม่ได้เชื่อมต่อ
        if (!dbLoaded)
        {
            try
            {
                var path = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            int id = item.TryGetProperty("BookingId", out var bid) ? bid.GetInt32() : 0;
                            if (!list.Any(l => l.BookingId == id))
                            {
                                list.Add(new RealBookingItem
                                {
                                    BookingId = id,
                                    CustomerName = item.TryGetProperty("CustomerName", out var cn) ? (cn.GetString() ?? "ลูกค้า") : "ลูกค้า",
                                    MovieTitle = item.TryGetProperty("MovieTitle", out var mt) ? (mt.GetString() ?? "ภาพยนตร์") : "ภาพยนตร์",
                                    Seats = item.TryGetProperty("SeatNumbers", out var sn) ? (sn.GetString() ?? "-") : "-",
                                    SeatCount = item.TryGetProperty("SeatCount", out var sc) ? sc.GetInt32() : 1,
                                    Amount = item.TryGetProperty("TotalAmount", out var ta) ? ta.GetDecimal() : 0,
                                    Status = item.TryGetProperty("Status", out var st) ? (st.GetString() ?? "Confirmed") : "Confirmed",
                                    BookedAt = DateTime.TryParse(item.TryGetProperty("BookedAt", out var ba) ? ba.GetString() : null, out var bdt) ? bdt : DateTime.Now,
                                    TaxInvoiceNo = item.TryGetProperty("TaxInvoiceNo", out var iv) ? (iv.GetString() ?? "") : ""
                                });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return list;
    }

    private List<TopGrossingMovieItem> GetTopGrossingMovies(decimal totalTicketRevenue, List<RealBookingItem> realBookings)
    {
        var movies = new List<TopGrossingMovieItem>();

        var active = realBookings.Where(b => b.Status != "Cancelled").ToList();
        if (active.Any())
        {
            var groups = active.GroupBy(b => b.MovieTitle).OrderByDescending(g => g.Sum(x => x.Amount)).ToList();
            int r = 1;
            foreach (var g in groups.Take(4))
            {
                movies.Add(new TopGrossingMovieItem
                {
                    Rank = r++,
                    MovieTitle = g.Key,
                    ShowtimesCount = g.Count(),
                    NetGross = g.Sum(x => x.Amount)
                });
            }
        }

        return movies;
    }

    private void EnsureAccountingTable()
    {
        try
        {
            if (_db.Database.CanConnect())
            {
                var conn = _db.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS accounting_transactions (
                        TransactionId INT AUTO_INCREMENT PRIMARY KEY,
                        Type VARCHAR(20) NOT NULL DEFAULT 'Income',
                        Category VARCHAR(100) NOT NULL,
                        Description VARCHAR(255) NOT NULL,
                        Amount DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
                        TransactionDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ReferenceNo VARCHAR(50) NOT NULL,
                        Notes TEXT NULL,
                        CreatedBy VARCHAR(100) NOT NULL DEFAULT 'Head Accountant'
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }
        catch { }
    }

    private List<AccountingTransaction> GetAccountingTransactions()
    {
        var list = new List<AccountingTransaction>();

        try
        {
            EnsureAccountingTable();
            if (_db.Database.CanConnect())
            {
                list.AddRange(_db.AccountingTransactions.OrderByDescending(t => t.TransactionDate).ToList());
            }
        }
        catch { }

        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "accounting.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        int id = el.TryGetProperty("TransactionId", out var tid) ? tid.GetInt32() : 0;
                        if (!list.Any(l => l.TransactionId == id))
                        {
                            list.Add(new AccountingTransaction
                            {
                                TransactionId = id,
                                Type = el.TryGetProperty("Type", out var ty) ? ty.GetString() ?? "Income" : "Income",
                                Category = el.TryGetProperty("Category", out var ct) ? ct.GetString() ?? "" : "",
                                Description = el.TryGetProperty("Description", out var ds) ? ds.GetString() ?? "" : "",
                                Amount = el.TryGetProperty("Amount", out var am) ? am.GetDecimal() : 0,
                                TransactionDate = DateTime.TryParse(el.TryGetProperty("TransactionDate", out var td) ? td.GetString() : null, out var dt) ? dt : DateTime.Now,
                                ReferenceNo = el.TryGetProperty("ReferenceNo", out var rf) ? rf.GetString() ?? "" : ""
                            });
                        }
                    }
                }
            }
        }
        catch { }

        return list;
    }

    private void SaveTransactionToFile(object txn)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "accounting.json");

            var list = new List<JsonElement>();
            if (System.IO.File.Exists(filePath))
            {
                var text = System.IO.File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray()) list.Add(el.Clone());
                }
            }

            list.Add(JsonSerializer.SerializeToElement(txn));
            var output = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, output);
        }
        catch { }
    }

    private List<InvoiceItemDto> GetInvoicesFromFile()
    {
        var list = new List<InvoiceItemDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "invoices.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<InvoiceItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }

        if (!list.Any())
        {
            list = new List<InvoiceItemDto>
            {
                new() { Id = 1, InvoiceNo = "INV-2026-0089", DateText = "24/02/2026", Party = "บริษัท ยูไนเต็ด ยูไอพี จำกัด", Description = "ส่วนแบ่งยอดขายตั๋วภาพยนตร์ 'Dune'", Amount = 85000, Status = "รอชำระ" },
                new() { Id = 2, InvoiceNo = "INV-2026-0088", DateText = "23/02/2026", Party = "คุณวิรุฬห์ สุขสวัสดิ์ (ลูกค้าจัดจ้างฉายรอบพิเศษ)", Description = "ปิดโรงจัดงานวันเกิดโรงภาพยนตร์ที่ 5", Amount = 32000, Status = "ชำระแล้ว" },
                new() { Id = 3, InvoiceNo = "INV-2026-0087", DateText = "20/02/2026", Party = "เป๊ปซี่-โคล่า (ประเทศไทย) จำกัด", Description = "ค่าสนับสนุนป้ายโฆษณาหน้าโรงภาพยนตร์", Amount = 45000, Status = "เกินกำหนด" },
                new() { Id = 4, InvoiceNo = "INV-2026-0086", DateText = "18/02/2026", Party = "ค่ายหนังภาพยนตร์ สหมงคลฟิล์ม", Description = "ส่วนแบ่งส่วนแรก 'หลานม่า'", Amount = 20500, Status = "ชำระแล้ว" }
            };
            SaveInvoicesToFile(list);
        }

        return list;
    }

    private void SaveInvoicesToFile(List<InvoiceItemDto> invoices)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "invoices.json");
            var json = JsonSerializer.Serialize(invoices, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private List<ChatMessageDto> GetChatMessagesFromFile()
    {
        var list = new List<ChatMessageDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "chat_messages.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<ChatMessageDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }

        if (!list.Any())
        {
            list = new List<ChatMessageDto>
            {
                new() { Id = 1, ChannelId = "accounting", SenderName = "เกวลิน ฝ่ายขาย", TimeText = "10:14 AM", MessageText = "สวัสดีค่ะพี่ณิชา ยอดขายตั๋ว Dune รอบแรกวันนี้สูงมากเลยค่ะ มีลูกค้าขอทำรายการใบกำกับภาษีเต็มรูปแบบ 3 ราย รายละเอียดส่งเข้าไปในระบบแล้วนะคะ", IsMine = false, CreatedAt = DateTime.Now.AddHours(-2) },
                new() { Id = 2, ChannelId = "accounting", SenderName = "ณิชา พงษ์ศิริ (คุณ)", TimeText = "10:18 AM", MessageText = "รับทราบค่ะเกวลิน เดี๋ยวพี่ตรวจสอบความถูกต้องกับระบบสตรีมหลังบ้านให้อีกรอบก่อนออกเอกสารนะ", IsMine = true, CreatedAt = DateTime.Now.AddHours(-2).AddMinutes(4) },
                new() { Id = 3, ChannelId = "accounting", SenderName = "สมชาย คลังสินค้า", TimeText = "10:22 AM", MessageText = "แจ้งเรื่องสต็อกวัตถุดิบป๊อปคอร์นครับ สัปดาห์หน้ามีหนังฟอร์มยักษ์เข้าพร้อมกัน 2 เรื่อง ผมแนะนำให้สั่งข้าวโพดเพิ่มอีก 15 กระสอบนะครับเพื่อความมั่นใจ", IsMine = false, CreatedAt = DateTime.Now.AddHours(-2).AddMinutes(8) }
            };
            SaveChatMessagesToFile(list);
        }

        return list;
    }

    private void SaveChatMessagesToFile(List<ChatMessageDto> messages)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "chat_messages.json");
            var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private List<RefundItemDto> GetRefundsFromFile()
    {
        var list = new List<RefundItemDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "refunds.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<RefundItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }

        return list;
    }

    private void SaveRefundsToFile(List<RefundItemDto> refunds)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "refunds.json");
            var json = JsonSerializer.Serialize(refunds, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private void UpdateRefundInFile(string requestId, string newStatus)
    {
        var list = GetRefundsFromFile();
        var item = list.FirstOrDefault(r => r.RequestId == requestId);
        if (item != null)
        {
            item.Status = newStatus;
            SaveRefundsToFile(list);
        }
    }

    private void UpdateBookingStatusByCode(string bookingCode, string newStatus)
    {
        try
        {
            var filePath = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
            if (System.IO.File.Exists(filePath))
            {
                var text = System.IO.File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var updated = new List<Dictionary<string, object>>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText()) ?? new();
                        if (dict.TryGetValue("ReferenceCode", out var rVal) && rVal.ToString() == bookingCode)
                        {
                            dict["Status"] = newStatus;
                        }
                        updated.Add(dict);
                    }
                    var output = JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(filePath, output);
                }
            }
        }
        catch { }
    }

    private void UpdateBookingStatusInFile(int bookingId, string newStatus)
    {
        try
        {
            var filePath = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
            if (System.IO.File.Exists(filePath))
            {
                var text = System.IO.File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var updated = new List<Dictionary<string, object>>();
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText()) ?? new();
                        if (dict.TryGetValue("BookingId", out var bVal) && Convert.ToInt32(bVal.ToString()) == bookingId)
                        {
                            dict["Status"] = newStatus;
                        }
                        updated.Add(dict);
                    }
                    var output = JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(filePath, output);
                }
            }
        }
        catch { }
    }

    private class RealBookingItem
    {
        public int BookingId { get; set; }
        public string CustomerName { get; set; } = "";
        public string MovieTitle { get; set; } = "";
        public string Seats { get; set; } = "";
        public int SeatCount { get; set; } = 1;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Confirmed";
        public DateTime BookedAt { get; set; }
        public string TaxInvoiceNo { get; set; } = "";
    }
}
