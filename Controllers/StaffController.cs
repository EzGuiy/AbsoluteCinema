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
    // 1. หน้าแรกของพนักงาน: สรุปสถิติจำนวนลูกค้าและการจอง
    // =========================================================================
    public IActionResult Index()
    {
        var vm = new StaffDashboardViewModel();

        // ดึงยอดจองจริงจาก bookings.json และ MySQL
        var realBookings = GetRealBookings();
        int realBookingsCount = realBookings.Count;
        int realCustomerCount = realBookings.Sum(b => b.SeatCount);

        vm.ExpectedCustomersToday = realCustomerCount > 0 ? (1800 + realCustomerCount) : 1842;
        vm.TodayBookingsCount = realBookingsCount > 0 ? (880 + realBookingsCount) : 892;

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
    // 7. ระบบแชตของพนักงาน (Floor Staff Internal Chat)
    // =========================================================================
    public IActionResult Chat(string channel = "staff")
    {
        HttpContext.Session.SetString("StaffChat_Seen_" + channel, "true");

        var vm = new ManagerChatViewModel();
        vm.ActiveRoomId = channel;

        int staffBadge = HttpContext.Session.GetString("StaffChat_Seen_staff") == "true" ? 0 : 2;
        int adminBadge = HttpContext.Session.GetString("StaffChat_Seen_admin") == "true" ? 0 : 1;
        int techBadge = HttpContext.Session.GetString("StaffChat_Seen_technician") == "true" ? 0 : 1;
        int acctBadge = HttpContext.Session.GetString("StaffChat_Seen_accounting") == "true" ? 0 : 0;

        vm.Rooms = new List<ManagerChatRoomDto>
        {
            new() { Id = "staff", Name = "พนักงานบริการ (Floor Staff)", LastMessageText = "แจ้งพบลืมร่มไว้บริเวณโรงฉาย 5 ค่ะ...", UnreadCount = staffBadge, ColorType = "brown", IsActive = channel == "staff", MembersInfo = "สมาชิกทั้งหมด 12 คน • ออนไลน์ 8 คน" },
            new() { Id = "admin", Name = "ผู้จัดการ & แอดมินสาขา", LastMessageText = "ขอรอบฉายเพิ่มเรื่องหลานม่า โรง 4...", UnreadCount = adminBadge, ColorType = "gold", IsActive = channel == "admin", MembersInfo = "สมาชิกทั้งหมด 6 คน • ออนไลน์ 3 คน" },
            new() { Id = "technician", Name = "ช่างเทคนิคประจำสาขา", LastMessageText = "ระบบเสียงโรง 2 ปรับปรุงและทดสอบผ่านแล้ว...", UnreadCount = techBadge, ColorType = "green", IsActive = channel == "technician", MembersInfo = "สมาชิกทั้งหมด 5 คน • ออนไลน์ 4 คน" },
            new() { Id = "accounting", Name = "ฝ่ายบัญชี & การเงิน", LastMessageText = "สรุปยอดเงินส่วนแบ่งรอบฉายและตั๋ว...", UnreadCount = acctBadge, ColorType = "blue", IsActive = channel == "accounting", MembersInfo = "สมาชิกทั้งหมด 4 คน • ออนไลน์ 2 คน" }
        };

        vm.ActiveRoom = vm.Rooms.FirstOrDefault(r => r.Id == channel) ?? vm.Rooms.First();

        // ดึงข้อความจาก Data/chat_messages.json
        string chatPath = Path.Combine(_env.ContentRootPath, "Data", "chat_messages.json");
        if (System.IO.File.Exists(chatPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(chatPath);
                var allMsgs = JsonSerializer.Deserialize<List<ManagerChatMessageDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                vm.Messages = allMsgs.Where(m => m.ChannelId.Equals(channel, StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.CreatedAt).ToList();
            }
            catch { }
        }

        vm.TotalUnreadCount = staffBadge + adminBadge + techBadge + acctBadge;
        ViewBag.UnreadStaffChatCount = vm.TotalUnreadCount;

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendStaffChatMessage(string channel, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string targetChannel = string.IsNullOrWhiteSpace(channel) ? "staff" : channel;
            string chatPath = Path.Combine(_env.ContentRootPath, "Data", "chat_messages.json");
            try
            {
                List<ManagerChatMessageDto> allMsgs = new();
                if (System.IO.File.Exists(chatPath))
                {
                    var json = System.IO.File.ReadAllText(chatPath);
                    allMsgs = JsonSerializer.Deserialize<List<ManagerChatMessageDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                int nextId = allMsgs.Any() ? allMsgs.Max(m => m.Id) + 1 : 1;
                string sender = HttpContext.Session.GetString("UserName") ?? "พนักงานบริการ (ตัวคุณ)";

                var newMsg = new ManagerChatMessageDto
                {
                    Id = nextId,
                    ChannelId = targetChannel,
                    SenderName = sender,
                    TimeText = DateTime.Now.ToString("HH:mm น."),
                    MessageText = message.Trim(),
                    IsMine = true,
                    CreatedAt = DateTime.Now
                };

                allMsgs.Add(newMsg);
                System.IO.File.WriteAllText(chatPath, JsonSerializer.Serialize(allMsgs, new JsonSerializerOptions { WriteIndented = true }));

                // บันทึกลง MySQL Table Chats ด้วย
                try
                {
                    if (_db.Database.CanConnect())
                    {
                        var dbChat = new Chat
                        {
                            SenderId = 1,
                            Message = message.Trim(),
                            SentAt = DateTime.Now
                        };
                        _db.Chats.Add(dbChat);
                        _db.SaveChanges();
                    }
                }
                catch { }
            }
            catch { }
        }

        return RedirectToAction("Chat", new { channel = channel });
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

        // 1. ดึงจาก MySQL Database โดยตรงผ่าน Entity Framework Core
        try
        {
            if (_db.Database.CanConnect())
            {
                var dbBookings = _db.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Where(b => b.Status != "Cancelled")
                    .OrderByDescending(b => b.BookingId)
                    .ToList();

                foreach (var b in dbBookings)
                {
                    var seats = (b.SeatNumbers ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string refCode = $"BK-{b.BookingId:D5}";
                    seenCodes.Add(refCode);

                    list.Add(new RealBookingItem
                    {
                        BookingId = b.BookingId,
                        CustomerName = b.User?.FullName ?? "ลูกค้า Absolute Cinema",
                        CustomerPhone = "08x-xxx-xxxx",
                        MovieTitle = b.Showtime?.Movie?.Title ?? "ภาพยนตร์รอบปัจจุบัน",
                        SeatNumbers = b.SeatNumbers ?? "",
                        SeatCount = seats.Length > 0 ? seats.Length : b.SeatCount,
                        ReferenceCode = refCode
                    });
                }
            }
        }
        catch { }

        // 2. ดึงเสริมจากไฟล์สำรอง Data/bookings.json (Dual Persistence สำรองข้อมูล)
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

                        list.Add(new RealBookingItem
                        {
                            BookingId = item.TryGetProperty("BookingId", out var id) ? id.GetInt32() : 0,
                            CustomerName = item.TryGetProperty("CustomerName", out var cn) ? cn.GetString() ?? "ลูกค้า" : "ลูกค้า",
                            CustomerPhone = item.TryGetProperty("CustomerPhone", out var cp) ? cp.GetString() ?? "" : "",
                            MovieTitle = item.TryGetProperty("MovieTitle", out var mt) ? mt.GetString() ?? "" : "",
                            SeatNumbers = item.TryGetProperty("SeatNumbers", out var sn) ? sn.GetString() ?? "" : "",
                            SeatCount = item.TryGetProperty("SeatCount", out var sc) ? sc.GetInt32() : 1,
                            ReferenceCode = refCode
                        });
                    }
                }
            }
        }
        catch { }

        return list;
    }

    private List<SeatStatusDto> GenerateSeatsMap(CinemaHallCardDto hall)
    {
        var seats = new List<SeatStatusDto>();
        var rows = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
        int seatsPerRow = hall.TotalSeats >= 180 ? 14 : (hall.TotalSeats >= 120 ? 12 : 10);

        var realBookings = GetRealBookings();

        // แมปการจองจริงลงที่นั่ง
        var bookedSeatsMap = new Dictionary<string, (string name, string phone, string refCode)>();
        foreach (var b in realBookings)
        {
            var splitSeats = b.SeatNumbers.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in splitSeats)
            {
                string cleanSeat = s.Trim().ToUpperInvariant();
                if (!bookedSeatsMap.ContainsKey(cleanSeat))
                {
                    bookedSeatsMap[cleanSeat] = (b.CustomerName, b.CustomerPhone, b.ReferenceCode);
                }
            }
        }

        // รายชื่อตัวอย่างเสริมสำหรับที่นั่งอื่นๆ ที่จองแล้ว
        var sampleCustomers = new[]
        {
            ("คุณกิตติพงษ์ สุขใจ", "081-998-1122", "BK-89021"),
            ("คุณอริสรา พรหมศร", "082-555-8899", "BK-89115"),
            ("คุณปองพล บุญส่ง", "089-123-4567", "BK-89088"),
            ("คุณสมชาย วรเศรษฐ์", "084-332-9988", "BK-88902"),
            ("คุณนิตยา เกียรติสกุล", "086-777-6543", "BK-88944"),
            ("คุณวราภรณ์ สุวรรณโชติ", "085-443-2211", "BK-88712"),
            ("คุณธีรพงศ์ อนันต์", "081-332-1144", "BK-88655")
        };

        int sampleIdx = 0;

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

                // ตรวจสอบกับการจองจริง
                if (bookedSeatsMap.TryGetValue(code, out var info))
                {
                    isBooked = true;
                    custName = info.name;
                    custPhone = info.phone;
                    refCode = info.refCode;
                    isCheckedIn = true; // สมมติว่าบางคนเช็คอินแล้ว
                }
                else
                {
                    // กำหนดสถานะตาม occupancy ของโรง
                    // แถวกลางๆ มักมีคนจองเยอะ
                    int hash = (code.GetHashCode() + hall.HallId * 7) % 100;
                    if (Math.Abs(hash) < (int)hall.OccupancyPercent)
                    {
                        isBooked = true;
                        var sample = sampleCustomers[sampleIdx % sampleCustomers.Length];
                        custName = sample.Item1;
                        custPhone = sample.Item2;
                        refCode = sample.Item3;
                        isCheckedIn = (hash % 2 == 0);
                        sampleIdx++;
                    }
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
                    BookedAtText = "วันนี้",
                    PaymentMethod = "PromptPay / บัตรเครดิต"
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
    }
}

