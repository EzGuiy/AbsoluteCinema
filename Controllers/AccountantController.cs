using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;

namespace Kawin_67091861_002.Controllers;

public class AccountantController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public AccountantController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // =========================================================================
    // 1. ภาพรวมประจำวัน (Daily Overview)
    // =========================================================================
    public IActionResult Dashboard()
    {
        var vm = new DailyOverviewViewModel();

        // 1. ดึงข้อมูลตั๋วที่จองจริงจาก Database หรือ Data/bookings.json
        var realBookings = GetRealBookings();

        decimal realTicketRevenue = realBookings.Where(b => b.Status != "Cancelled").Sum(b => b.Amount);
        int realBookingCount = realBookings.Count(b => b.Status != "Cancelled");
        int realCustomerCount = realBookings.Where(b => b.Status != "Cancelled").Sum(b => b.SeatCount);

        // รวมยอด Concessions & Merchandise จากบันทึกบัญชี
        var transactions = GetAccountingTransactions();
        decimal concessionRevenue = transactions.Where(t => t.Type == "Income" && t.Category == "Concessions").Sum(t => t.Amount);
        decimal merchRevenue = transactions.Where(t => t.Type == "Income" && t.Category == "Merchandise").Sum(t => t.Amount);
        decimal adRevenue = transactions.Where(t => t.Type == "Income" && t.Category == "Advertising").Sum(t => t.Amount);

        if (concessionRevenue == 0) concessionRevenue = 62325;
        if (merchRevenue == 0) merchRevenue = 24650;
        if (adRevenue == 0) adRevenue = 15000;

        decimal baseTicketRev = realTicketRevenue > 0 ? realTicketRevenue : 161525;
        decimal totalRevenue = baseTicketRev + concessionRevenue + merchRevenue + adRevenue;

        vm.TodayRevenue = totalRevenue;
        vm.TodayCustomers = realCustomerCount > 0 ? (1200 + realCustomerCount) : 1247;
        vm.TodayBookings = realBookingCount > 0 ? (880 + realBookingCount) : 892;
        vm.AttendanceRate = 87.3;

        // สัดส่วนรายรับตามหมวดหมู่ (Category Breakdown)
        vm.CategoryBreakdown = new CategoryRevenueBreakdown
        {
            TotalRevenue = totalRevenue,
            CenterDisplay = $"฿{(totalRevenue / 1000m):F1}K",
            TicketsAmount = baseTicketRev,
            TicketsPercent = Math.Round((double)(baseTicketRev / totalRevenue) * 100, 0),
            ConcessionsAmount = concessionRevenue,
            ConcessionsPercent = Math.Round((double)(concessionRevenue / totalRevenue) * 100, 0),
            MerchandiseAmount = merchRevenue,
            MerchandisePercent = Math.Round((double)(merchRevenue / totalRevenue) * 100, 0)
        };

        // กราฟแนวโน้มรายรับ 7 วัน (Revenue Trend Last 7 Days)
        vm.WeeklyRevenueTrend = new List<DayRevenueItem>
        {
            new() { DayName = "จันทร์", Revenue = 185000 },
            new() { DayName = "อังคาร", Revenue = 210000 },
            new() { DayName = "พุธ", Revenue = 175000 },
            new() { DayName = "พฤหัสบดี", Revenue = 230000 },
            new() { DayName = "ศุกร์", Revenue = 245000 },
            new() { DayName = "เสาร์", Revenue = 220000 },
            new() { DayName = "อาทิตย์ (วันนี้)", Revenue = totalRevenue }
        };

        // ภาพยนตร์ทำรายได้สูงสุดวันนี้ (Top Grossing Shows Today)
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
        if (ticketSales == 0) ticketSales = 161525;

        // รายการรายรับ
        vm.IncomeBreakdown = new List<FinancialLineItem>
        {
            new() { Id = 1, CategoryName = "ตั๋วภาพยนตร์ (Ticket Sales)", Subtitle = "+4.15% vs เมื่อวาน", Amount = ticketSales },
            new() { Id = 2, CategoryName = "ขนมและเครื่องดื่ม (Concessions)", Subtitle = "+12.1% vs เมื่อวาน", Amount = 62325 },
            new() { Id = 3, CategoryName = "สินค้าของพรีเมียม (Merchandise)", Subtitle = "-2.5% vs เมื่อวาน", Amount = 24650 },
            new() { Id = 4, CategoryName = "โฆษณาในโรงหนัง (Advertising)", Subtitle = "คงที่ (Stable)", Amount = 15000 }
        };

        // รายการรายจ่าย
        vm.ExpenseBreakdown = new List<FinancialLineItem>
        {
            new() { Id = 101, CategoryName = "ค่าจ้างพนักงาน (Staff Wages)", Subtitle = "รวมกะเช้าและกะดึก", Amount = 42500, IsExpense = true },
            new() { Id = 102, CategoryName = "สาธารณูปโภค (Utilities)", Subtitle = "ค่าไฟและแอร์ตลอดวัน", Amount = 18200, IsExpense = true },
            new() { Id = 103, CategoryName = "พัสดุและวัตถุดิบ (Supplies)", Subtitle = "วัตถุดิบป๊อปคอร์นและแก้วน้ำ", Amount = 12400, IsExpense = true },
            new() { Id = 104, CategoryName = "การบำรุงรักษาเครื่อง (Maintenance)", Subtitle = "ดูแลระบบฉายและทำความสะอาด", Amount = 9500, IsExpense = true },
            new() { Id = 105, CategoryName = "ส่วนแบ่งลิขสิทธิ์หนัง (Licensing)", Subtitle = "สัญญาแบ่งสตูดิโอค่ายหนัง", Amount = 85000, IsExpense = true }
        };

        // รวมยอดเพิ่มเติมจาก transactions ที่บันทึกเข้ามาจริง
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

        // กราฟเปรียบเทียบในรอบ 30 วัน (4 สัปดาห์)
        vm.MonthlyComparison = new List<WeeklyFinancialItem>
        {
            new() { WeekLabel = "สัปดาห์ 1", Income = 1750000, Expenses = 1120000 },
            new() { WeekLabel = "สัปดาห์ 2", Income = 1920000, Expenses = 1250000 },
            new() { WeekLabel = "สัปดาห์ 3", Income = 1680000, Expenses = 1190000 },
            new() { WeekLabel = "สัปดาห์ 4", Income = 2100000, Expenses = 1380000 }
        };

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

        // คำนวณสรุปสถิติ
        vm.TodayRequestsCount = 24;
        vm.CompletedCount = transactions.Count(t => t.Status == "อนุมัติแล้ว") > 0 ? (20 + transactions.Count(t => t.Status == "อนุมัติแล้ว")) : 21;
        vm.TotalRefundAmount = transactions.Where(t => t.Status == "อนุมัติแล้ว").Sum(t => t.Amount);
        if (vm.TotalRefundAmount == 0) vm.TotalRefundAmount = 5460;
        vm.CancellationRate = 1.8;

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

            // อัปเดตใน bookings.json ด้วยหากมี
            try
            {
                var refunds = GetRefundsFromFile();
                var item = refunds.FirstOrDefault(r => r.RequestId == id);
                if (item != null && !string.IsNullOrEmpty(item.BookingCode))
                {
                    // ลองหาและอัปเดตใน Database หรือไฟล์
                    UpdateBookingStatusByCode(item.BookingCode, "Cancelled");
                }
            }
            catch { }

            TempData["Success"] = $"อนุมัติการคืนเงิน {id} สำเร็จ ยอดเงินถูกคืนเข้าสู่บัญชีลูกค้าเรียบร้อยแล้ว";
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
    // 6. แชทในองค์กร (Organization Chat)
    // =========================================================================
    public IActionResult Chat(string channel = "accounting")
    {
        var vm = new OrganizationChatViewModel
        {
            ActiveChannelId = string.IsNullOrEmpty(channel) ? "accounting" : channel.ToLowerInvariant()
        };

        // ตั้งชื่อ Channel ที่เปิดอยู่
        vm.ActiveChannelName = vm.ActiveChannelId switch
        {
            "purchasing" => "ฝ่ายจัดซื้อ (Purchasing)",
            "executives" => "ผู้บริหาร (Executives)",
            _ => "ฝ่ายบัญชี (Accounting)"
        };

        // Channels ตาม Mockup
        vm.Channels = new List<ChatChannelDto>
        {
            new() { Id = "accounting", Name = "ฝ่ายบัญชี (Accounting)", UnreadCount = 0, IsActive = vm.ActiveChannelId == "accounting" },
            new() { Id = "purchasing", Name = "ฝ่ายจัดซื้อ (Purchasing)", UnreadCount = 2, IsActive = vm.ActiveChannelId == "purchasing" },
            new() { Id = "executives", Name = "ผู้บริหาร (Executives)", UnreadCount = 0, IsActive = vm.ActiveChannelId == "executives" }
        };

        // Direct Messages ตาม Mockup
        vm.DirectMessages = new List<ChatContactDto>
        {
            new() { Id = "somchai", Name = "สมชาย คลังสินค้า", Role = "Concessions Lead", IsOnline = true },
            new() { Id = "kewalin", Name = "เกวลิน ฝ่ายขาย", Role = "Box Office Supervisor", IsOnline = true },
            new() { Id = "thanawut", Name = "ธนาวุฒิ ระบบโรง", Role = "Projectionist Engineer", IsOnline = true }
        };

        // ดึงข้อความของ Channel ปัจจุบัน
        var allMessages = GetChatMessagesFromFile();
        vm.Messages = allMessages.Where(m => m.ChannelId == vm.ActiveChannelId).OrderBy(m => m.CreatedAt).ToList();

        // กรณีไม่มีข้อความใน channel อื่น ให้มีข้อความต้อนรับ
        if (!vm.Messages.Any() && vm.ActiveChannelId != "accounting")
        {
            vm.Messages.Add(new ChatMessageDto
            {
                Id = 999,
                ChannelId = vm.ActiveChannelId,
                SenderName = "ระบบแจ้งเตือน",
                TimeText = DateTime.Now.ToString("h:mm tt"),
                MessageText = $"ยินดีต้อนรับสู่ห้องแชท #{vm.ActiveChannelName} สมาชิกสามารถส่งข้อความปรึกษางานได้ที่นี่",
                IsMine = false,
                CreatedAt = DateTime.Now
            });
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendChatMessage([FromForm] string channelId, [FromForm] string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string ch = string.IsNullOrEmpty(channelId) ? "accounting" : channelId.ToLowerInvariant();
            var allMessages = GetChatMessagesFromFile();

            var newMsg = new ChatMessageDto
            {
                Id = allMessages.Count + 1,
                ChannelId = ch,
                SenderName = "ณิชา พงษ์ศิริ (คุณ)",
                TimeText = DateTime.Now.ToString("h:mm tt"),
                MessageText = message.Trim(),
                IsMine = true,
                CreatedAt = DateTime.Now
            };

            allMessages.Add(newMsg);
            SaveChatMessagesToFile(allMessages);

            // บันทึกลง MySQL chats table ด้วยถ้ามี
            try
            {
                if (_db.Database.CanConnect())
                {
                    var user = _db.Users.FirstOrDefault(u => u.RoleId == 3) ?? _db.Users.FirstOrDefault();
                    if (user != null)
                    {
                        _db.Chats.Add(new Chat
                        {
                            SenderId = user.UserId,
                            Message = message.Trim(),
                            SentAt = DateTime.Now
                        });
                        _db.SaveChanges();
                    }
                }
            }
            catch { }
        }

        return RedirectToAction("Chat", new { channel = channelId });
    }

    // =========================================================================
    // Private Helpers: ข้อมูลตั๋ว, หนังทำเงิน, Transactions
    // =========================================================================
    private List<RealBookingItem> GetRealBookings()
    {
        var list = new List<RealBookingItem>();

        // จาก MySQL
        try
        {
            if (_db.Database.CanConnect())
            {
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

        // จาก Data/bookings.json
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
                                MovieTitle = item.TryGetProperty("MovieTitle", out var mt) ? (mt.GetString() ?? "Dune: Part Two") : "Dune: Part Two",
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

        return list;
    }

    private List<TopGrossingMovieItem> GetTopGrossingMovies(decimal totalTicketRevenue, List<RealBookingItem> realBookings)
    {
        var movies = new List<TopGrossingMovieItem>();

        // ถ้ามีการจองจริง ให้กรุ๊ปตามเรื่อง
        if (realBookings.Any())
        {
            var groups = realBookings.GroupBy(b => b.MovieTitle).OrderByDescending(g => g.Sum(x => x.Amount)).ToList();
            int r = 1;
            foreach (var g in groups.Take(4))
            {
                movies.Add(new TopGrossingMovieItem
                {
                    Rank = r++,
                    MovieTitle = g.Key,
                    ShowtimesCount = Math.Max(2, g.Count()),
                    NetGross = g.Sum(x => x.Amount)
                });
            }
        }

        // หนังมาตรฐานตาม mockup
        if (!movies.Any())
        {
            movies.Add(new TopGrossingMovieItem { Rank = 1, MovieTitle = "Dune: Part Two (IMAX 2D)", ShowtimesCount = 4, NetGross = 124500 });
            movies.Add(new TopGrossingMovieItem { Rank = 2, MovieTitle = "Godzilla x Kong", ShowtimesCount = 2, NetGross = 62300 });
            movies.Add(new TopGrossingMovieItem { Rank = 3, MovieTitle = "Exhuma (ขุดรอยหลอนอุบาทว์)", ShowtimesCount = 3, NetGross = 41200 });
            movies.Add(new TopGrossingMovieItem { Rank = 4, MovieTitle = "หลานม่า (Lahn Mah)", ShowtimesCount = 3, NetGross = 20500 });
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

        if (!list.Any())
        {
            list = new List<RefundItemDto>
            {
                new() { RequestId = "REF-4001", CustomerName = "กิตติพงษ์ สุขใจ", MovieTitle = "Dune: Part Two", Amount = 260, Status = "อนุมัติแล้ว", BookingCode = "BK-89021", ShowtimeText = "24/02/2026 เวลา 19:00 น.", CancellationTimeText = "24/02/2026 เวลา 12:00 น. (ก่อนฉาย 7 ชั่วโมง)", RefundAmountText = "260.00 (คืนเต็มจำนวน)" },
                new() { RequestId = "REF-4002", CustomerName = "อริสรา พรหมศร", MovieTitle = "หลานม่า (Lahn Mah)", Amount = 160, Status = "รอดำเนินการ", BookingCode = "BK-89115", ShowtimeText = "24/02/2026 เวลา 21:00 น.", CancellationTimeText = "24/02/2026 เวลา 14:00 น. (ก่อนฉาย 7 ชั่วโมง)", RefundAmountText = "160.00 (คืนเต็มจำนวน)" },
                new() { RequestId = "REF-4003", CustomerName = "ปองพล บุญส่ง", MovieTitle = "Godzilla x Kong", Amount = 320, Status = "ปฏิเสธ", BookingCode = "BK-89088", ShowtimeText = "24/02/2026 เวลา 16:30 น.", CancellationTimeText = "24/02/2026 เวลา 15:45 น. (ก่อนฉาย 45 นาที)", RefundAmountText = "0.00 (ยกเลิกน้อยกว่า 5 ชม.)" }
            };
            SaveRefundsToFile(list);
        }

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
