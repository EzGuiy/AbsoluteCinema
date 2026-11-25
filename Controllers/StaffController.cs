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

public class StaffController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public StaffController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // =========================================================================
    // Auth Guard: ตรวจสิทธิ์ทุก action (Staff หรือ Admin เท่านั้น)
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
        if (role != "Staff" && role != "Admin")
        {
            context.Result = RedirectToAction("Index", "Home");
        }
    }

    // =========================================================================
    // 1. หน้าแรกของพนักงาน: สรุปสถิติจำนวนลูกค้าและการจอง
    // =========================================================================
    public IActionResult Index()
    {
        var vm = new StaffDashboardViewModel();

        // ดึงยอดจองจริง 100% จาก MySQL
        var realBookings = GetRealBookings();
        int realBookingsCount = realBookings.Count;
        int realCustomerCount = realBookings.Sum(b => b.SeatCount);

        vm.ExpectedCustomersToday = realCustomerCount;
        vm.TodayBookingsCount = realBookingsCount;

        vm.Halls = GetHallsFromFile();
        vm.ActiveHallsCount = vm.Halls.Count;
        int cleanHalls = vm.Halls.Count(h => h.IsCleaned);
        vm.HallsReadinessPercent = Math.Round((double)cleanHalls / vm.Halls.Count * 100, 1);
        vm.HallsReadinessSubtext = $"ตรวจความพร้อมแล้ว {cleanHalls} จาก {vm.Halls.Count} โรง";

        // รายการภารกิจกะประจำวัน (Shift Tasks)
        vm.ShiftTasks = new List<StaffShiftTaskDto>
        {
            new() { TimeText = "14:50 น.", HallName = "โรง 2 (Dolby Atmos)", TaskTitle = "เตรียมตรวจความสะอาดหลังหนังจบ (Godzilla x Kong)", Status = "รอดำเนินการ", BadgeColor = "orange" },
            new() { TimeText = "15:00 น.", HallName = "โรง 1 (IMAX)", TaskTitle = "เตรียมตรวจความสะอาดและเคลียร์ทางเดิน (Dune: Part Two)", Status = "รอดำเนินการ", BadgeColor = "orange" },
            new() { TimeText = "15:10 น.", HallName = "โรง 7 (Standard)", TaskTitle = "เช็คเก้าอี้และถูพื้นเตรียมรอบ 15:30 น.", Status = "รอดำเนินการ", BadgeColor = "orange" },
            new() { TimeText = "13:45 น.", HallName = "โรง 3 (Digital 4K)", TaskTitle = "ตรวจความสะอาดและส่งมอบโรงสำเร็จ", Status = "เสร็จสิ้น", BadgeColor = "green" }
        };

        // รายการรหัสตั๋วและข้อมูลการจองล่าสุดสำหรับพนักงานตรวจตั๋ว
        vm.RecentBookings = realBookings.Take(10).Select(b => new StaffRecentBookingDto
        {
            BookingId = b.BookingId,
            ReferenceCode = b.ReferenceCode,
            MovieTitle = b.MovieTitle,
            CinemaName = b.CinemaName,
            ShowtimeText = b.ShowtimeText,
            SeatNumbers = b.SeatNumbers,
            SeatCount = b.SeatCount,
            CustomerName = b.CustomerName,
            CustomerPhone = b.CustomerPhone,
            TotalAmount = b.TotalAmount,
            Status = b.Status,
            BookedAtText = b.BookedAtText
        }).ToList();

        return View(vm);
    }

    // =========================================================================
    // 2. ระบบเช็คข้อมูลภายในโรงภาพยนต์ (แสดงห้องทุกห้องในโรงภาพยนตร์)
    // =========================================================================
    public IActionResult Halls(string status = "all")
    {
        var halls = GetHallsFromFile();

        if (status == "cleaning")
        {
            halls = halls.Where(h => !h.IsCleaned || h.ShowingStatus.Contains("ทำความสะอาด")).ToList();
        }
        else if (status == "ready")
        {
            halls = halls.Where(h => h.IsCleaned && h.ShowingStatus == "พร้อมเข้าชม").ToList();
        }
        else if (status == "showing")
        {
            halls = halls.Where(h => h.ShowingStatus == "กำลังฉาย").ToList();
        }

        ViewBag.StatusFilter = status;
        ViewBag.TotalHalls = 8;
        ViewBag.NeedCleaningCount = GetHallsFromFile().Count(h => !h.IsCleaned || h.ShowingStatus.Contains("ทำความสะอาด"));

        return View(halls);
    }

    // =========================================================================
    // 3. ผังที่นั่งตามที่ลูกค้าไปตามที่นั่งคนนั้นๆ เมื่อกดเข้าดูในโรงภาพยนตร์
    // =========================================================================
    public IActionResult HallDetail(int id)
    {
        var halls = GetHallsFromFile();
        var hall = halls.FirstOrDefault(h => h.HallId == id) ?? halls.First();

        // สร้างผังที่นั่งจริงตามการจอง
        hall.Seats = GenerateSeatsMap(hall);

        return View(hall);
    }

    // =========================================================================
    // 4. บันทึกตรวจความสะอาดและความพร้อมของโรงภาพยนตร์
    // =========================================================================
    [HttpPost]
    public IActionResult MarkCleaned([FromForm] InspectHallDto dto)
    {
        var halls = GetHallsFromFile();
        var hall = halls.FirstOrDefault(h => h.HallId == dto.HallId);

        if (hall != null)
        {
            hall.IsCleaned = true;
            hall.CleanlinessStatus = "สะอาดเรียบร้อย";
            hall.ShowingStatus = "พร้อมเข้าชม";
            hall.ShowingStatusColor = "green";
            hall.LastCleanedTime = DateTime.Now.ToString("HH:mm น.");
            hall.CleanedBy = string.IsNullOrWhiteSpace(dto.StaffName) ? "สมศรี รักบริการ" : dto.StaffName;

            SaveHallsToFile(halls);
            TempData["Success"] = $"บันทึกการตรวจความสะอาดและเตรียมความพร้อม {hall.HallName} สำเร็จ! โรงพร้อมเข้าชมแล้ว";
        }

        return RedirectToAction("Halls");
    }

    // =========================================================================
    // 5. เรียกช่างเทคนิคเพื่อทดสอบระบบหรือแจ้งของพัง
    // =========================================================================
    [HttpPost]
    public IActionResult CallTechnician([FromForm] CallTechnicianDto dto)
    {
        if (dto != null)
        {
            string hallName = string.IsNullOrWhiteSpace(dto.HallName) ? $"โรง {dto.HallId}" : dto.HallName;
            string incCode = $"INC-0{new Random().Next(845, 899)}";

            // 1. เพิ่มเข้า Data/operations_incidents.json ของทีมเทคนิค
            try
            {
                var opsPath = Path.Combine(_env.ContentRootPath, "Data", "operations_incidents.json");
                var incidents = new List<IncidentItemDto>();
                if (System.IO.File.Exists(opsPath))
                {
                    var json = System.IO.File.ReadAllText(opsPath);
                    incidents = JsonSerializer.Deserialize<List<IncidentItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                incidents.Insert(0, new IncidentItemDto
                {
                    Code = incCode,
                    Title = $"[{dto.IssueType}] {hallName}: {dto.Description}",
                    SourceAndTime = $"{hallName} • พนักงาน {dto.ReportedBy} • {DateTime.Now:HH:mm}",
                    Assignee = "ช่างประจำเวร",
                    Status = "เปิดอยู่",
                    StatusColor = "red",
                    ReportedAt = DateTime.Now
                });

                System.IO.File.WriteAllText(opsPath, JsonSerializer.Serialize(incidents, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            // 2. บันทึกลง MySQL Operations Table ถ้ามี
            try
            {
                if (_db.Database.CanConnect())
                {
                    _db.Operations.Add(new Operation
                    {
                        CinemaId = dto.HallId > 0 ? dto.HallId : 1,
                        ReportedBy = 1,
                        TaskType = dto.IssueType,
                        Description = $"{incCode}: {dto.Description} (ความเร่งด่วน: {dto.Urgency})",
                        Status = "เปิดอยู่"
                    });
                    _db.SaveChanges();
                }
            }
            catch { }

            TempData["Success"] = $"เรียกช่างเทคนิคเรียบร้อยแล้ว (รหัสแจ้งเหตุ {incCode}) ระบบแจ้งเตือนไปยังฝ่ายช่างทันที";
        }

        return RedirectToAction("Halls");
    }

    // =========================================================================
    // 6. เช็คอินที่นั่งของลูกค้า (Ticket Check-in)
    // =========================================================================
    [HttpPost]
    public IActionResult CheckInTicket(int hallId, string seatCode)
    {
        TempData["Success"] = $"เช็คอินที่นั่ง {seatCode} เรียบร้อยแล้ว (ลูกค้ารับการตรวจตั๋วแล้ว)";
        return RedirectToAction("HallDetail", new { id = hallId });
    }

    // =========================================================================
    // 7. ระบบแชตของพนักงาน (แชทกลุ่มรวมทุกแผนก)
    // =========================================================================
    public IActionResult Chat(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Staff");
        string activeRoom = "group";

        HttpContext.Session.SetString("StaffChat_Seen_group", "true");

        var vm = new ManagerChatViewModel();
        string myName = HttpContext.Session.GetString("UserName") ?? "พนักงานบริการ";

        vm.Rooms = new List<ManagerChatRoomDto>
        {
            new()
            {
                Id = "group",
                Name = "แชทกลุ่มรวม (All Staff)",
                LastMessageText = "สื่อสารระหว่างพนักงานทุกแผนกในสาขา",
                UnreadCount = 0,
                ColorType = "blue",
                IsActive = true,
                MembersInfo = "ผู้จัดการ, พนักงานบริการ, ช่างเทคนิค, บัญชี, แอดมิน"
            }
        };

        vm.ActiveRoomId = activeRoom;
        vm.ActiveRoom = vm.Rooms.First();

        vm.Messages = ChatHelper.GetMessagesForChannel(_db, _env, activeRoom, "Staff", myName, myUserId);

        vm.TotalUnreadCount = 0;
        ViewBag.UnreadStaffChatCount = 0;

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendStaffChatMessage(string channel, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string sender = HttpContext.Session.GetString("UserName") ?? "พนักงานบริการ";
            ChatHelper.SendMessage(_db, _env, "group", message, sender, "Staff", HttpContext);
        }
        return RedirectToAction("Chat", new { channel = "group" });
    }

    [HttpGet]
    public IActionResult GetMessagesApi(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Staff");
        string role = HttpContext.Session.GetString("UserRole") ?? "Staff";
        string myName = HttpContext.Session.GetString("UserName") ?? "พนักงานบริการ";
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, "group", role, myName, myUserId);
        return Json(new { success = true, channel = "group", messages = messages });
    }

    [HttpPost]
    public IActionResult SendMessageApi([FromForm] string channel, [FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { success = false, message = "ข้อความว่างเปล่า" });

        string sender = HttpContext.Session.GetString("UserName") ?? "พนักงานบริการ";
        string role = HttpContext.Session.GetString("UserRole") ?? "Staff";
        var newMsg = ChatHelper.SendMessage(_db, _env, "group", message, sender, role, HttpContext);
        return Json(new { success = true, newMsg = newMsg });
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================
    private List<CinemaHallCardDto> GetHallsFromFile()
    {
        var list = new List<CinemaHallCardDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "hall_inspections.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<CinemaHallCardDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }

        return list;
    }

    private void SaveHallsToFile(List<CinemaHallCardDto> halls)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var path = Path.Combine(dataDir, "hall_inspections.json");
            var json = JsonSerializer.Serialize(halls, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(path, json);
        }
        catch { }
    }

    private List<RealBookingItem> GetRealBookings()
    {
        var list = new List<RealBookingItem>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool dbLoaded = false;
        // 1. ดึงจาก MySQL Database โดยตรงผ่าน Entity Framework Core
        try
        {
            if (_db.Database.CanConnect())
            {
                dbLoaded = true;
                var dbBookings = _db.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Include(b => b.Showtime).ThenInclude(s => s.Cinema)
                    .Where(b => b.Status != "Cancelled")
                    .OrderByDescending(b => b.BookingId)
                    .ToList();

                foreach (var b in dbBookings)
                {
                    var seats = (b.SeatNumbers ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string refCode = $"AC-{b.BookingId:D6}";
                    seenCodes.Add(refCode);

                    string realPhone = UserProfileHelper.GetPhone(b.User?.Username, b.UserId);
                    if (string.IsNullOrEmpty(realPhone)) realPhone = "081-xxx-xxxx";

                    list.Add(new RealBookingItem
                    {
                        BookingId = b.BookingId,
                        CustomerName = b.User?.FullName ?? "ลูกค้า Absolute Cinema",
                        CustomerPhone = realPhone,
                        MovieTitle = b.Showtime?.Movie?.Title ?? "ภาพยนตร์รอบปัจจุบัน",
                        SeatNumbers = b.SeatNumbers ?? "",
                        SeatCount = seats.Length > 0 ? seats.Length : b.SeatCount,
                        ReferenceCode = refCode,
                        ShowtimeText = b.Showtime != null ? b.Showtime.StartTime.ToString("dd/MM/yyyy HH:mm น.") : "15:00 น.",
                        CinemaName = b.Showtime?.Cinema?.CinemaName ?? "Gold Class",
                        Status = b.Status ?? "Confirmed",
                        BookedAtText = b.BookedAt.HasValue ? b.BookedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        TotalAmount = b.TotalAmount ?? 0m
                    });
                }
            }
        }
        catch { }

        // 2. ดึงเสริมจากไฟล์สำรองเฉพาะกรณีที่ฐานข้อมูล MySQL ไม่ได้เชื่อมต่อ
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
                        string refCode = item.TryGetProperty("ReferenceCode", out var rc) ? rc.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(refCode) && seenCodes.Contains(refCode))
                            continue; // ข้ามรายการที่โหลดจาก MySQL มาแล้ว

                        string custPhone = item.TryGetProperty("CustomerPhone", out var cp) ? cp.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(custPhone) || custPhone == "08x-xxx-xxxx")
                        {
                            string cEmail = item.TryGetProperty("CustomerEmail", out var ce) ? ce.GetString() ?? "" : "";
                            int uId = item.TryGetProperty("UserId", out var uidP) ? uidP.GetInt32() : 0;
                            custPhone = UserProfileHelper.GetPhone(cEmail, uId);
                        }
                        if (string.IsNullOrEmpty(custPhone)) custPhone = "081-xxx-xxxx";

                        decimal totalAmt = item.TryGetProperty("TotalAmount", out var ta) ? ta.GetDecimal() : 0m;
                        string mTitle = item.TryGetProperty("MovieTitle", out var mt) ? mt.GetString() ?? "" : "ภาพยนตร์";
                        string cinema = item.TryGetProperty("CinemaName", out var cnm) ? cnm.GetString() ?? "Gold Class" : "Gold Class";
                        string showTime = item.TryGetProperty("ShowtimeTimeText", out var stt) ? stt.GetString() ?? "15:00 น." : "15:00 น.";
                        string status = item.TryGetProperty("Status", out var stp) ? stp.GetString() ?? "Confirmed" : "Confirmed";
                        string bookedAt = item.TryGetProperty("BookedAt", out var bat) ? bat.GetString() ?? "" : "";

                        list.Add(new RealBookingItem
                        {
                            BookingId = item.TryGetProperty("BookingId", out var id) ? id.GetInt32() : 0,
                            CustomerName = item.TryGetProperty("CustomerName", out var cn) ? cn.GetString() ?? "ลูกค้า" : "ลูกค้า",
                            CustomerPhone = custPhone,
                            MovieTitle = mTitle,
                            SeatNumbers = item.TryGetProperty("SeatNumbers", out var sn) ? sn.GetString() ?? "" : "",
                            SeatCount = item.TryGetProperty("SeatCount", out var sc) ? sc.GetInt32() : 1,
                            ReferenceCode = refCode,
                            ShowtimeText = showTime,
                            CinemaName = cinema,
                            Status = status,
                            BookedAtText = bookedAt,
                            TotalAmount = totalAmt
                        });
                    }
                }
            }
            }
            catch { }
        }

        return list;
    }

    private List<SeatStatusDto> GenerateSeatsMap(CinemaHallCardDto hall)
    {
        var seats = new List<SeatStatusDto>();
        var rows = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
        int seatsPerRow = hall.TotalSeats >= 180 ? 14 : (hall.TotalSeats >= 120 ? 12 : 10);

        var realBookings = GetRealBookings();

        // แมปการจองจริงจาก Database ลงที่นั่งเท่านั้น (ไม่สุ่มลูกค้าจำลอง)
        var bookedSeatsMap = new Dictionary<string, (string name, string phone, string refCode, string bookedAt)>();
        foreach (var b in realBookings)
        {
            var splitSeats = b.SeatNumbers.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in splitSeats)
            {
                string cleanSeat = s.Trim().ToUpperInvariant();
                if (!bookedSeatsMap.ContainsKey(cleanSeat))
                {
                    bookedSeatsMap[cleanSeat] = (b.CustomerName, b.CustomerPhone, b.ReferenceCode, b.BookedAtText);
                }
            }
        }

        foreach (var r in rows)
        {
            string tier = (r == "G" || r == "H") ? "VIP" : ((r == "D" || r == "E" || r == "F") ? "Premium" : "Normal");
            decimal price = tier == "VIP" ? 380 : (tier == "Premium" ? 320 : 260);

            for (int n = 1; n <= seatsPerRow; n++)
            {
                string code = $"{r}{n}";
                bool isBooked = false;
                bool isCheckedIn = false;
                string custName = "";
                string custPhone = "";
                string refCode = "";
                string bookedAt = "";

                if (bookedSeatsMap.TryGetValue(code, out var info))
                {
                    isBooked = true;
                    custName = info.name;
                    custPhone = info.phone;
                    refCode = info.refCode;
                    bookedAt = info.bookedAt;
                    isCheckedIn = false;
                }

                seats.Add(new SeatStatusDto
                {
                    SeatCode = code,
                    Row = r,
                    Number = n,
                    Tier = tier,
                    Price = price,
                    IsBooked = isBooked,
                    IsCheckedIn = isCheckedIn,
                    CustomerName = custName,
                    CustomerPhone = custPhone,
                    BookingCode = refCode,
                    BookedAtText = string.IsNullOrEmpty(bookedAt) ? "-" : bookedAt,
                    PaymentMethod = "PromptPay"
                });
            }
        }

        return seats;
    }

    private class RealBookingItem
    {
        public int BookingId { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string MovieTitle { get; set; } = "";
        public string SeatNumbers { get; set; } = "";
        public int SeatCount { get; set; } = 1;
        public string ReferenceCode { get; set; } = "";
        public string ShowtimeText { get; set; } = "";
        public string CinemaName { get; set; } = "";
        public string Status { get; set; } = "Confirmed";
        public string BookedAtText { get; set; } = "";
        public decimal TotalAmount { get; set; }
    }
}

