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

public class ManagerController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public ManagerController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private void CalculateUnreadBadges()
    {
        int adminUnread = HttpContext.Session.GetString("ManagerChat_Seen_admin") == "true" ? 0 : 3;
        int acctUnread = HttpContext.Session.GetString("ManagerChat_Seen_accounting") == "true" ? 0 : 2;
        int techUnread = HttpContext.Session.GetString("ManagerChat_Seen_technician") == "true" ? 0 : 1;
        int staffUnread = HttpContext.Session.GetString("ManagerChat_Seen_staff") == "true" ? 0 : 2;

        int totalUnread = adminUnread + acctUnread + techUnread + staffUnread;
        ViewBag.UnreadManagerChatCount = totalUnread;
    }

    // ==========================================
    // 0. Dashboard / ภาพรวม
    // ==========================================
    public IActionResult Index()
    {
        CalculateUnreadBadges();
        var vm = new ManagerDashboardViewModel();

        // 1. KPI Stats
        vm.TodayRevenue = 428650;
        vm.RevenueChangeText = "+12.4%";
        vm.TicketsSold = 1842;
        vm.TicketsGoalText = "78% เป้าหมาย";
        vm.AvgAttendancePercent = 78.6;
        vm.AttendanceChangeText = "+5.2%";
        vm.TodayShowtimesCount = 48;
        vm.RemainingShowtimesText = "อีก 19 รอบ";

        // 2. รอบฉายที่กำลังมาถึง (Upcoming Showtimes)
        vm.UpcomingShowtimes = new List<UpcomingShowtimeDto>
        {
            new() { TimeText = "12:30", MovieTitle = "Dune: Part Two", HallAndType = "โรง 1 • IMAX", SeatsSoldText = "188 / 220", Status = "กำลังขาย", StatusBadgeColor = "green" },
            new() { TimeText = "14:10", MovieTitle = "หลานม่า", HallAndType = "โรง 3 • Digital", SeatsSoldText = "95 / 140", Status = "กำลังขาย", StatusBadgeColor = "green" },
            new() { TimeText = "16:45", MovieTitle = "Furiosa", HallAndType = "โรง 2 • 4DX", SeatsSoldText = "121 / 156", Status = "ใกล้เต็ม", StatusBadgeColor = "red" }
        };

        // 3. จัดการสถานะภาพยนตร์ (Movie Statuses)
        vm.MovieStatuses = GetMovieStatuses();

        // 4. การแจ้งเตือนแชททีมงาน (Team Chat) - เชื่อมโยงข้าม Role จริง
        string techMsg = "แก้ไขระบบเสียงโรง 2 เรียบร้อย";
        string acctMsg = "อนุมัติยอดเงินส่วนแบ่งรอบฉายแล้ว";
        try
        {
            var chatPath = Path.Combine(_env.ContentRootPath, "Data", "chat_messages.json");
            if (System.IO.File.Exists(chatPath))
            {
                var json = System.IO.File.ReadAllText(chatPath);
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("ChannelId", out var ch))
                    {
                        string c = ch.GetString() ?? "";
                        if (c == "manager" && el.TryGetProperty("MessageText", out var mt))
                            techMsg = mt.GetString() ?? techMsg;
                        else if (c == "accounting" && el.TryGetProperty("MessageText", out var ma))
                            acctMsg = ma.GetString() ?? acctMsg;
                    }
                }
            }
        }
        catch { }

        int adminBadge = HttpContext.Session.GetString("ManagerChat_Seen_admin") == "true" ? 0 : 3;
        int acctBadge = HttpContext.Session.GetString("ManagerChat_Seen_accounting") == "true" ? 0 : 2;
        int techBadge = HttpContext.Session.GetString("ManagerChat_Seen_technician") == "true" ? 0 : 1;
        int staffBadge = HttpContext.Session.GetString("ManagerChat_Seen_staff") == "true" ? 0 : 2;

        vm.ChatNotifications = new List<ManagerTeamChatNotificationDto>
        {
            new() { DepartmentName = "ฝ่ายบัญชี", LastMessage = acctMsg, UnreadCount = acctBadge, AvatarColor = "blue" },
            new() { DepartmentName = "แอดมินสาขา", LastMessage = "ขอรอบฉายเพิ่มเรื่องหลานม่า โรง 4", UnreadCount = adminBadge, AvatarColor = "orange" },
            new() { DepartmentName = "ช่างเทคนิคประจำสาขา", LastMessage = techMsg, UnreadCount = techBadge, AvatarColor = "green" },
            new() { DepartmentName = "พนักงานบริการ", LastMessage = "แจ้งขอพนักงานตรวจตั๋วเพิ่ม", UnreadCount = staffBadge, AvatarColor = "brown" }
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult SetMovieReady(string title)
    {
        if (!string.IsNullOrEmpty(title))
        {
            try
            {
                if (_db.Database.CanConnect())
                {
                    var m = _db.Movies.FirstOrDefault(x => x.Title.Contains(title));
                    if (m != null)
                    {
                        m.Status = "Showing";
                        _db.SaveChanges();
                    }
                }
            }
            catch { }

            TempData["Success"] = $"อัปเดตสถานะภาพยนตร์ '{title}' เป็น 'พร้อมฉาย' เรียบร้อยแล้ว";
        }

        return RedirectToAction("Index");
    }

    // ==========================================
    // 1. จัดการรอบฉายภาพยนตร์ (Showtimes)
    // ==========================================
    public IActionResult Showtimes(string? filter)
    {
        CalculateUnreadBadges();
        var vm = new ManagerShowtimesViewModel();
        vm.DateDisplay = "15 กันยายน 2569 (วันนี้)";
        vm.HallFilter = string.IsNullOrEmpty(filter) ? "ทั้งหมด (IMAX, 4DX, Standard)" : filter;

        string showtimesPath = Path.Combine(_env.ContentRootPath, "Data", "manager_showtimes.json");
        if (System.IO.File.Exists(showtimesPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(showtimesPath);
                var halls = JsonSerializer.Deserialize<List<ManagerHallScheduleDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (halls != null)
                {
                    vm.Halls = halls;
                }
            }
            catch { }
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateShowtime([FromForm] CreateShowtimeDto dto)
    {
        if (dto != null)
        {
            string showtimesPath = Path.Combine(_env.ContentRootPath, "Data", "manager_showtimes.json");
            try
            {
                List<ManagerHallScheduleDto> halls = new();
                if (System.IO.File.Exists(showtimesPath))
                {
                    var json = System.IO.File.ReadAllText(showtimesPath);
                    halls = JsonSerializer.Deserialize<List<ManagerHallScheduleDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                var hall = halls.FirstOrDefault(h => h.HallNumber == dto.HallNumber);
                if (hall != null)
                {
                    hall.Slots.Add(new ManagerShowtimeSlotDto
                    {
                        ShowtimeId = new Random().Next(1000, 9999),
                        TimeText = string.IsNullOrWhiteSpace(dto.TimeText) ? "20:00" : dto.TimeText,
                        MovieTitle = string.IsNullOrWhiteSpace(dto.MovieTitle) ? "Dune: Part Two" : dto.MovieTitle,
                        BookedSeats = 0,
                        TotalSeats = dto.TotalSeats > 0 ? dto.TotalSeats : 150,
                        DotColor = "green"
                    });

                    System.IO.File.WriteAllText(showtimesPath, JsonSerializer.Serialize(halls, new JsonSerializerOptions { WriteIndented = true }));
                    TempData["Success"] = $"สร้างรอบฉายใหม่ {dto.TimeText} ({dto.MovieTitle}) สำหรับโรง {dto.HallNumber} สำเร็จแล้ว";
                }
            }
            catch { }
        }
        return RedirectToAction("Showtimes");
    }

    // ==========================================
    // 2. คลังและจัดการภาพยนตร์ (Movies)
    // ==========================================
    public IActionResult Movies(string filter = "all", int? selectedId = null)
    {
        CalculateUnreadBadges();
        var vm = new ManagerMoviesViewModel();
        vm.ActiveFilter = filter;

        var allMovies = GetManagerMoviesList();

        if (filter == "showing")
        {
            vm.Movies = allMovies.Where(m => m.StatusBadge == "กำลังฉาย").ToList();
        }
        else if (filter == "coming")
        {
            vm.Movies = allMovies.Where(m => m.StatusBadge == "โปรแกรมหน้า" || m.StatusBadge == "พร้อมฉาย").ToList();
        }
        else
        {
            vm.Movies = allMovies;
        }

        int targetId = selectedId ?? 1;
        vm.SelectedMovie = allMovies.FirstOrDefault(m => m.MovieId == targetId) ?? allMovies.FirstOrDefault();

        return View(vm);
    }

    [HttpPost]
    public IActionResult SetMovieStatus(int id, string status)
    {
        TempData["Success"] = $"ปรับปรุงสถานะภาพยนตร์เป็น '{status}' เรียบร้อยแล้ว";
        return RedirectToAction("Movies", new { selectedId = id });
    }

    [HttpPost]
    public IActionResult AddManagerMovie([FromForm] AddMovieDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Title))
        {
            try
            {
                if (_db.Database.CanConnect())
                {
                    var m = new Movie
                    {
                        Title = dto.Title.Trim(),
                        Genre = dto.Genre,
                        DurationMins = dto.DurationMins > 0 ? dto.DurationMins : 120,
                        PriceMin = dto.PriceMin,
                        PriceMax = dto.PriceMax,
                        Status = dto.Status,
                        Description = dto.Description,
                        Rating = 8.5,
                        ReleaseDate = DateTime.Today
                    };
                    _db.Movies.Add(m);
                    _db.SaveChanges();
                }
            }
            catch { }

            TempData["Success"] = $"เพิ่มภาพยนตร์ใหม่ '{dto.Title}' เรียบร้อยแล้ว";
        }
        return RedirectToAction("Movies");
    }

    // ==========================================
    // 3. โปรโมชั่นและส่วนลด (Promotions)
    // ==========================================
    public IActionResult Promotions()
    {
        CalculateUnreadBadges();
        var vm = new ManagerPromotionsViewModel();

        string promoPath = Path.Combine(_env.ContentRootPath, "Data", "manager_promotions.json");
        if (System.IO.File.Exists(promoPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(promoPath);
                var list = JsonSerializer.Deserialize<List<ManagerPromotionItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (list != null)
                {
                    vm.Promotions = list;
                }
            }
            catch { }
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreatePromotion([FromForm] CreatePromotionDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Title))
        {
            string promoPath = Path.Combine(_env.ContentRootPath, "Data", "manager_promotions.json");
            try
            {
                List<ManagerPromotionItemDto> list = new();
                if (System.IO.File.Exists(promoPath))
                {
                    var json = System.IO.File.ReadAllText(promoPath);
                    list = JsonSerializer.Deserialize<List<ManagerPromotionItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                int nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                list.Add(new ManagerPromotionItemDto
                {
                    Id = nextId,
                    Title = dto.Title,
                    Description = dto.Description,
                    DiscountType = dto.DiscountType,
                    DurationText = dto.DurationText,
                    UsageCountText = "0 ครั้ง",
                    Status = dto.Status,
                    StatusColor = dto.Status == "กำลังใช้งาน" ? "green" : (dto.Status == "หมดอายุแล้ว" ? "grey" : "amber")
                });

                System.IO.File.WriteAllText(promoPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                TempData["Success"] = $"สร้างแคมเปญโปรโมชั่น '{dto.Title}' เรียบร้อยแล้ว";
            }
            catch { }
        }
        return RedirectToAction("Promotions");
    }

    [HttpPost]
    public IActionResult TogglePromotionStatus(int id)
    {
        string promoPath = Path.Combine(_env.ContentRootPath, "Data", "manager_promotions.json");
        try
        {
            if (System.IO.File.Exists(promoPath))
            {
                var json = System.IO.File.ReadAllText(promoPath);
                var list = JsonSerializer.Deserialize<List<ManagerPromotionItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                var promo = list.FirstOrDefault(p => p.Id == id);
                if (promo != null)
                {
                    if (promo.Status == "กำลังใช้งาน")
                    {
                        promo.Status = "หมดอายุแล้ว";
                        promo.StatusColor = "grey";
                    }
                    else
                    {
                        promo.Status = "กำลังใช้งาน";
                        promo.StatusColor = "green";
                    }
                    System.IO.File.WriteAllText(promoPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                    TempData["Success"] = $"อัปเดตสถานะของ '{promo.Title}' เป็น '{promo.Status}' แล้ว";
                }
            }
        }
        catch { }
        return RedirectToAction("Promotions");
    }

    // ==========================================
    // 4. รายงานยอดขายและสถิติ (Reports)
    // ==========================================
    public IActionResult Reports()
    {
        CalculateUnreadBadges();
        var vm = new ManagerReportsViewModel();

        vm.TotalRevenueToday = "฿428,650";
        vm.TotalRevenueSub = "+12.4% เทียบสัปดาห์ก่อน";
        vm.TicketRevenue = "฿295,400";
        vm.TicketRevenueSub = "รวม 1,842 ใบจำหน่าย";
        vm.ConcessionRevenue = "฿133,250";
        vm.ConcessionSub = "คิดเป็น 31% ของยอดรวม";
        vm.AvgRevenuePerShow = "฿8,930";
        vm.AvgRevenueSub = "+5.2% การเติบโตต่อที่นั่ง";

        vm.WeeklySales = new List<DaySalesBarDto>
        {
            new() { DayName = "จันทร์", SalesText = "฿185K", BarHeightPercent = 45 },
            new() { DayName = "อังคาร", SalesText = "฿428K", BarHeightPercent = 100 },
            new() { DayName = "พุธ", SalesText = "฿290K", BarHeightPercent = 65 },
            new() { DayName = "พฤหัสบดี", SalesText = "฿210K", BarHeightPercent = 50 },
            new() { DayName = "ศุกร์", SalesText = "฿380K", BarHeightPercent = 85 },
            new() { DayName = "เสาร์", SalesText = "฿410K", BarHeightPercent = 95 },
            new() { DayName = "อาทิตย์", SalesText = "฿395K", BarHeightPercent = 90 }
        };

        vm.MovieSales = new List<MovieSalesRowDto>
        {
            new() { Rank = 1, Title = "Dune: Part Two", RevenueText = "฿165,200", SharePercentText = "38.5%", ChangeText = "+18.2%", ChangeColor = "green" },
            new() { Rank = 2, Title = "Inside Out 2", RevenueText = "฿112,400", SharePercentText = "26.2%", ChangeText = "+8.4%", ChangeColor = "green" },
            new() { Rank = 3, Title = "หลานม่า", RevenueText = "฿78,600", SharePercentText = "18.3%", ChangeText = "-2.1%", ChangeColor = "red" },
            new() { Rank = 4, Title = "Furiosa", RevenueText = "฿45,300", SharePercentText = "10.6%", ChangeText = "+4.5%", ChangeColor = "green" },
            new() { Rank = 5, Title = "Bad Boys", RevenueText = "฿27,150", SharePercentText = "6.4%", ChangeText = "-5.0%", ChangeColor = "red" }
        };

        vm.HallSales = new List<HallSalesRowDto>
        {
            new() { HallTitle = "โรง 1 (IMAX Laser) • 3 รอบ", RevenueText = "฿142,500", OccupancyPercent = "84.5% อัตราที่นั่ง" },
            new() { HallTitle = "โรง 2 (4DX Extreme) • 3 รอบ", RevenueText = "฿86,400", OccupancyPercent = "78.2% อัตราที่นั่ง" },
            new() { HallTitle = "โรง 3 (Standard Digital) • 3 รอบ", RevenueText = "฿54,000", OccupancyPercent = "92.0% อัตราที่นั่ง" },
            new() { HallTitle = "โรง 4 (Standard Digital) • 2 รอบ", RevenueText = "฿38,500", OccupancyPercent = "68.0% อัตราที่นั่ง" },
            new() { HallTitle = "โรง 5 - 8 (Standard Digital)", RevenueText = "฿107,250", OccupancyPercent = "72.4% อัตราที่นั่ง" }
        };

        return View(vm);
    }

    // ==========================================
    // 5. พนักงานและสิทธิ์การเข้าถึง (Staff)
    // ==========================================
    public IActionResult Staff()
    {
        CalculateUnreadBadges();
        var vm = new ManagerStaffViewModel();

        string staffPath = Path.Combine(_env.ContentRootPath, "Data", "manager_staff.json");
        if (System.IO.File.Exists(staffPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(staffPath);
                var list = JsonSerializer.Deserialize<List<ManagerStaffRowDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (list != null)
                {
                    vm.StaffList = list;
                    vm.OnDutyCount = list.Count(s => s.Status.Contains("ปฏิบัติงาน"));
                    vm.OnLeaveCount = list.Count(s => !s.Status.Contains("ปฏิบัติงาน"));
                }
            }
            catch { }
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateStaffMember([FromForm] CreateStaffMemberDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Name))
        {
            string staffPath = Path.Combine(_env.ContentRootPath, "Data", "manager_staff.json");
            try
            {
                List<ManagerStaffRowDto> list = new();
                if (System.IO.File.Exists(staffPath))
                {
                    var json = System.IO.File.ReadAllText(staffPath);
                    list = JsonSerializer.Deserialize<List<ManagerStaffRowDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                int nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                list.Add(new ManagerStaffRowDto
                {
                    Id = nextId,
                    Name = dto.Name,
                    RoleTitle = dto.RoleTitle,
                    ShiftText = dto.ShiftText,
                    AccessLevel = dto.AccessLevel,
                    Status = "ปฏิบัติงาน (On-duty)",
                    StatusColor = "green"
                });

                System.IO.File.WriteAllText(staffPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                TempData["Success"] = $"เพิ่มเจ้าหน้าที่ '{dto.Name}' เข้าสู่ระบบแล้ว";
            }
            catch { }
        }
        return RedirectToAction("Staff");
    }

    [HttpPost]
    public IActionResult ToggleStaffStatus(int id)
    {
        string staffPath = Path.Combine(_env.ContentRootPath, "Data", "manager_staff.json");
        try
        {
            if (System.IO.File.Exists(staffPath))
            {
                var json = System.IO.File.ReadAllText(staffPath);
                var list = JsonSerializer.Deserialize<List<ManagerStaffRowDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                var s = list.FirstOrDefault(x => x.Id == id);
                if (s != null)
                {
                    if (s.Status.Contains("ปฏิบัติงาน"))
                    {
                        s.Status = "ลากิจ (On Leave)";
                        s.StatusColor = "amber";
                    }
                    else
                    {
                        s.Status = "ปฏิบัติงาน (On-duty)";
                        s.StatusColor = "green";
                    }
                    System.IO.File.WriteAllText(staffPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                    TempData["Success"] = $"เปลี่ยนสถานะของ '{s.Name}' เป็น '{s.Status}' เรียบร้อยแล้ว";
                }
            }
        }
        catch { }
        return RedirectToAction("Staff");
    }

    // ==========================================
    // 6. แชทและการสื่อสารภายใน (Chat)
    // ==========================================
    public IActionResult Chat(string channel = "admin")
    {
        // When visiting the channel, mark it as seen
        HttpContext.Session.SetString("ManagerChat_Seen_" + channel, "true");
        CalculateUnreadBadges();

        var vm = new ManagerChatViewModel();
        vm.ActiveRoomId = channel;

        int adminBadge = HttpContext.Session.GetString("ManagerChat_Seen_admin") == "true" ? 0 : 3;
        int acctBadge = HttpContext.Session.GetString("ManagerChat_Seen_accounting") == "true" ? 0 : 2;
        int techBadge = HttpContext.Session.GetString("ManagerChat_Seen_technician") == "true" ? 0 : 1;
        int staffBadge = HttpContext.Session.GetString("ManagerChat_Seen_staff") == "true" ? 0 : 2;

        vm.Rooms = new List<ManagerChatRoomDto>
        {
            new() { Id = "admin", Name = "แอดมินสาขา", LastMessageText = "ผู้จัดการครับ ทางลูกค้าแจ้งว่ารอบ Dune 14:30...", UnreadCount = adminBadge, ColorType = "gold", IsActive = channel == "admin", MembersInfo = "สมาชิกทั้งหมด 6 คน • ออนไลน์ 3 คน" },
            new() { Id = "accounting", Name = "ฝ่ายบัญชี", LastMessageText = "แจ้งเรื่องสต็อกวัตถุดิบป๊อปคอร์นครับ สัปดาห์หน้า...", UnreadCount = acctBadge, ColorType = "blue", IsActive = channel == "accounting", MembersInfo = "สมาชิกทั้งหมด 4 คน • ออนไลน์ 2 คน" },
            new() { Id = "technician", Name = "ช่างเทคนิคประจำสาขา", LastMessageText = "ระบบเสียงโรง 2 ปรับปรุงและทดสอบผ่านแล้ว...", UnreadCount = techBadge, ColorType = "green", IsActive = channel == "technician", MembersInfo = "สมาชิกทั้งหมด 5 คน • ออนไลน์ 4 คน" },
            new() { Id = "staff", Name = "พนักงานบริการ", LastMessageText = "แจ้งพบลืมร่มไว้บริเวณโรงฉาย 5 ค่ะ...", UnreadCount = staffBadge, ColorType = "brown", IsActive = channel == "staff", MembersInfo = "สมาชิกทั้งหมด 12 คน • ออนไลน์ 8 คน" }
        };

        vm.ActiveRoom = vm.Rooms.FirstOrDefault(r => r.Id == channel) ?? vm.Rooms.First();

        // Read messages from chat_messages.json
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

        vm.TotalUnreadCount = (int)(ViewBag.UnreadManagerChatCount ?? 0);

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendManagerChatMessage(string channel, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string targetChannel = string.IsNullOrWhiteSpace(channel) ? "admin" : channel;
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
                var newMsg = new ManagerChatMessageDto
                {
                    Id = nextId,
                    ChannelId = targetChannel,
                    SenderName = "ณัฐชา (ผู้จัดการ - ตัวคุณ)",
                    TimeText = DateTime.Now.ToString("HH:mm"),
                    MessageText = message.Trim(),
                    IsMine = true,
                    CreatedAt = DateTime.Now
                };

                allMsgs.Add(newMsg);
                System.IO.File.WriteAllText(chatPath, JsonSerializer.Serialize(allMsgs, new JsonSerializerOptions { WriteIndented = true }));

                // Cross-sync to DB if connected
                try
                {
                    if (_db.Database.CanConnect())
                    {
                        var user = _db.Users.FirstOrDefault(u => u.Role != null && u.Role.RoleName == "Manager") ?? _db.Users.FirstOrDefault();
                        if (user != null)
                        {
                            var dbChat = new Chat
                            {
                                SenderId = user.UserId,
                                Message = message.Trim(),
                                SentAt = DateTime.Now
                            };
                            _db.Chats.Add(dbChat);
                            _db.SaveChanges();
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        return RedirectToAction("Chat", new { channel = channel });
    }

    // ==========================================
    // Helpers
    // ==========================================
    private List<MovieStatusCardDto> GetMovieStatuses()
    {
        return new List<MovieStatusCardDto>
        {
            new() { MovieId = 1, Title = "A Quiet Place: Day One", Subtitle = "กำหนดฉาย 27 มิ.ย.", Status = "โปรแกรมหน้า", BadgeColor = "gray", CanSetReady = true },
            new() { MovieId = 2, Title = "Inside Out 2", Subtitle = "เริ่มฉายวันนี้", Status = "พร้อมฉาย", BadgeColor = "yellow", CanSetReady = false },
            new() { MovieId = 3, Title = "Bad Boys: Ride or Die", Subtitle = "เหลือ 18 รอบ", Status = "กำลังฉาย", BadgeColor = "green", CanSetReady = false }
        };
    }

    private List<ManagerMovieCardDto> GetManagerMoviesList()
    {
        return new List<ManagerMovieCardDto>
        {
            new()
            {
                MovieId = 1,
                Title = "Dune: Part Two",
                ThaiTitle = "ดูน ภาคสอง",
                Genre = "Sci-Fi • 166 นาที",
                ReleaseDateText = "เริ่มฉาย 29 ก.พ. 2569",
                AgeRating = "PG-13",
                StatusBadge = "กำลังฉาย",
                StatusColor = "green",
                DirectorAndCast = "Denis Villeneuve • Timothée Chalamet",
                ShowingHalls = "โรง 1 (IMAX Laser), โรง 5 (Digital)",
                TicketSalesAvg = "ยอดจำหน่ายตั๋วเฉลี่ย 88.5% ของทุกรอบ",
                PosterUrl = ""
            },
            new()
            {
                MovieId = 2,
                Title = "Inside Out 2",
                ThaiTitle = "มหัศจรรย์อารมณ์อลเวง 2",
                Genre = "Animation • 96 นาที",
                ReleaseDateText = "เริ่มฉายวันนี้",
                AgeRating = "G",
                StatusBadge = "กำลังฉาย",
                StatusColor = "green",
                DirectorAndCast = "Kelsey Mann • Amy Poehler",
                ShowingHalls = "โรง 1, โรง 4, โรง 7",
                TicketSalesAvg = "ยอดจำหน่ายตั๋วเฉลี่ย 82.0% ของทุกรอบ",
                PosterUrl = ""
            },
            new()
            {
                MovieId = 3,
                Title = "หลานม่า",
                ThaiTitle = "หลานม่า",
                Genre = "Drama • 127 นาที",
                ReleaseDateText = "เริ่มฉาย 4 เม.ย. 2569",
                AgeRating = "G",
                StatusBadge = "พร้อมฉาย",
                StatusColor = "yellow",
                DirectorAndCast = "พัฒน์ บุญนิธิพัฒน์ • บิวกิ้น พุฒิพงศ์",
                ShowingHalls = "โรง 3 (Standard Digital)",
                TicketSalesAvg = "ยอดจำหน่ายตั๋วเฉลี่ย 94.2% ของทุกรอบ",
                PosterUrl = ""
            },
            new()
            {
                MovieId = 4,
                Title = "A Quiet Place: Day One",
                ThaiTitle = "ดินแดนไร้เสียง: วันที่หนึ่ง",
                Genre = "Thriller • 100 นาที",
                ReleaseDateText = "เข้าฉาย 27 มิ.ย. 2569",
                AgeRating = "PG-13",
                StatusBadge = "โปรแกรมหน้า",
                StatusColor = "cyan",
                DirectorAndCast = "Michael Sarnoski • Lupita Nyong'o",
                ShowingHalls = "โรง 4, โรง 6 (เปิดจองล่วงหน้า)",
                TicketSalesAvg = "ยอดจองตั๋วล่วงหน้า 54 ที่นั่ง",
                PosterUrl = ""
            },
            new()
            {
                MovieId = 5,
                Title = "Furiosa",
                ThaiTitle = "ฟูริโอซ่า มหากาพย์แมดแม็กซ์",
                Genre = "Action • 148 นาที",
                ReleaseDateText = "สิ้นสุดโปรแกรมฉาย",
                AgeRating = "R",
                StatusBadge = "ปิดรอบแล้ว",
                StatusColor = "red",
                DirectorAndCast = "George Miller • Anya Taylor-Joy",
                ShowingHalls = "โรง 2, โรง 5",
                TicketSalesAvg = "สิ้นสุดโปรแกรมฉายรอบปกติ",
                PosterUrl = ""
            },
            new()
            {
                MovieId = 6,
                Title = "Bad Boys: Ride or Die",
                ThaiTitle = "คู่หูขวางนรก: ลุยต่อให้สุด",
                Genre = "Action • 115 นาที",
                ReleaseDateText = "ฉายรอบสุดท้ายสัปดาห์นี้",
                AgeRating = "R",
                StatusBadge = "กำลังฉาย",
                StatusColor = "green",
                DirectorAndCast = "Adil & Bilall • Will Smith, Martin Lawrence",
                ShowingHalls = "โรง 2, โรง 6, โรง 8",
                TicketSalesAvg = "ยอดจำหน่ายตั๋วเฉลี่ย 75.3% ของทุกรอบ",
                PosterUrl = ""
            }
        };
    }
}
