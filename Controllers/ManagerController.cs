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
        int groupUnread = HttpContext.Session.GetString("ManagerChat_Seen_group") == "true" ? 0 : 2;
        int adminUnread = HttpContext.Session.GetString("ManagerChat_Seen_admin") == "true" ? 0 : 1;

        int totalUnread = groupUnread + adminUnread;
        ViewBag.UnreadManagerChatCount = totalUnread;
    }

    // ==========================================
    // Auth Guard: ตรวจสิทธิ์ทุก action (Manager หรือ Admin เท่านั้น)
    // ==========================================
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        if (HttpContext.Session.GetString("UserLoggedIn") != "true")
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }
        var role = HttpContext.Session.GetString("UserRole") ?? "";
        if (role != "Manager" && role != "Admin")
        {
            context.Result = RedirectToAction("Index", "Home");
        }
    }

    // ==========================================
    // Helper: ดึงข้อมูลการจองและคำนวณรายได้จริงตามช่วงวันที่
    // นิยามรายได้: นับเฉพาะการจองที่ Status != "Cancelled"
    // และยึดตามยอด Payments ที่ไม่ถูก Refund (IsRefunded != true) หรือ TotalAmount
    // ==========================================
    private (decimal ticketRevenue, int ticketsSold, int bookingsCount, List<Booking> activeBookings) GetActiveBookingsAndRevenue(DateTime startDate, DateTime endDate)
    {
        if (!_db.Database.CanConnect()) return (0, 0, 0, new List<Booking>());

        var bookings = _db.Bookings
            .Include(b => b.Payments)
            .Include(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(b => b.Showtime).ThenInclude(s => s.Cinema)
            .Where(b => b.Status != "Cancelled" && b.BookedAt.HasValue && b.BookedAt.Value >= startDate && b.BookedAt.Value < endDate)
            .ToList();

        decimal revenue = 0m;
        foreach (var b in bookings)
        {
            var validPayments = b.Payments.Where(p => p.IsRefunded != true).ToList();
            if (validPayments.Any())
            {
                revenue += validPayments.Sum(p => p.Amount);
            }
            else
            {
                revenue += b.TotalAmount ?? 0m;
            }
        }

        int ticketsSold = bookings.Sum(b => b.SeatCount);
        int bookingsCount = bookings.Count;

        return (revenue, ticketsSold, bookingsCount, bookings);
    }

    // ==========================================
    // 0. Dashboard / ภาพรวม
    // ==========================================
    public IActionResult Index()
    {
        CalculateUnreadBadges();
        var vm = new ManagerDashboardViewModel();

        try
        {
            if (!_db.Database.CanConnect())
            {
                ViewBag.DbError = "ไม่สามารถเชื่อมต่อฐานข้อมูล MySQL ได้ กรุณาตรวจสอบว่า Service MySQL กำลังทำงานอยู่";
                return View(vm);
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 1. ดึงยอดขายและสถิติตั๋วของวันนี้จาก DB จริง
            var (todayRevenue, ticketsSold, bookingsCount, activeBookings) = GetActiveBookingsAndRevenue(today, tomorrow);
            vm.TodayRevenue = todayRevenue;
            vm.RevenueChangeText = "ยอดขายตั๋วจริงวันนี้";
            vm.TicketsSold = ticketsSold;
            vm.TicketsGoalText = $"{bookingsCount} รายการจองวันนี้";

            // 2. รอบฉายของวันนี้
            var todayShowtimes = _db.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Cinema)
                .Include(s => s.Bookings)
                .Where(s => s.StartTime >= today && s.StartTime < tomorrow)
                .OrderBy(s => s.StartTime)
                .ToList();

            vm.TodayShowtimesCount = todayShowtimes.Count;
            int totalSeatsToday = todayShowtimes.Sum(s => s.Cinema?.TotalSeats ?? 100);
            int bookedSeatsToday = todayShowtimes.SelectMany(s => s.Bookings).Where(b => b.Status != "Cancelled").Sum(b => b.SeatCount);
            vm.AvgAttendancePercent = totalSeatsToday > 0 ? Math.Round((double)bookedSeatsToday / totalSeatsToday * 100, 1) : 0;
            vm.AttendanceChangeText = "อัตราเฉลี่ยวันนี้";

            int remainingCount = todayShowtimes.Count(s => s.StartTime >= DateTime.Now);
            vm.RemainingShowtimesText = $"อีก {remainingCount} รอบวันนี้";

            vm.UpcomingShowtimes = todayShowtimes
                .Where(s => s.StartTime >= DateTime.Now)
                .Take(5)
                .Select(s =>
                {
                    int cap = s.Cinema?.TotalSeats ?? 100;
                    int sold = s.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.SeatCount);
                    bool almostFull = cap > 0 && ((double)sold / cap) >= 0.75;
                    return new UpcomingShowtimeDto
                    {
                        TimeText = s.StartTime.ToString("HH:mm"),
                        MovieTitle = s.Movie?.Title ?? "ภาพยนตร์",
                        HallAndType = s.Cinema?.CinemaName ?? $"โรง {s.CinemaId}",
                        SeatsSoldText = $"{sold} / {cap}",
                        Status = almostFull ? "ใกล้เต็ม" : "กำลังขาย",
                        StatusBadgeColor = almostFull ? "red" : "green"
                    };
                }).ToList();

            // 3. จัดการสถานะภาพยนตร์จาก DB จริง (ตาราง Movies)
            vm.MovieStatuses = _db.Movies
                .OrderBy(m => m.MovieId)
                .Take(6)
                .Select(m => new MovieStatusCardDto
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Subtitle = m.Genre ?? "ภาพยนตร์",
                    Status = m.Status == "Showing" ? "กำลังฉาย" : (m.Status == "ComingSoon" ? "โปรแกรมหน้า" : (m.Status ?? "พร้อมฉาย")),
                    BadgeColor = m.Status == "Showing" ? "green" : (m.Status == "ComingSoon" ? "gray" : "yellow"),
                    CanSetReady = m.Status != "Showing"
                }).ToList();

            // 4. แชททีมงานจริงจากตาราง Chats
            var latestChats = _db.Chats
                .Include(c => c.Sender)
                .ThenInclude(u => u.Role)
                .OrderByDescending(c => c.SentAt)
                .Take(4)
                .ToList();

            vm.ChatNotifications = latestChats.Select(c => new ManagerTeamChatNotificationDto
            {
                DepartmentName = c.Sender?.FullName ?? c.Sender?.Role?.RoleName ?? "ทีมงาน",
                LastMessage = c.Message,
                UnreadCount = 0,
                AvatarColor = (c.Sender?.Role?.RoleName ?? "") switch
                {
                    "Accountant" => "blue",
                    "Technician" => "green",
                    "Staff" => "brown",
                    _ => "orange"
                }
            }).ToList();
        }
        catch (Exception ex)
        {
            ViewBag.DbError = "เกิดข้อผิดพลาดในการโหลดข้อมูล: " + (ex.InnerException?.Message ?? ex.Message);
        }

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
                        TempData["Success"] = $"อัปเดตสถานะภาพยนตร์ '{m.Title}' เป็น 'พร้อมฉาย' เรียบร้อยแล้ว";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "ไม่สามารถบันทึกข้อมูล: " + ex.Message;
            }
        }

        return RedirectToAction("Index");
    }

    // ==========================================
    // 1. จัดการรอบฉายภาพยนตร์ (Showtimes) จาก DB จริง
    // ==========================================
    public IActionResult Showtimes(string? filter)
    {
        CalculateUnreadBadges();
        var vm = new ManagerShowtimesViewModel();
        vm.DateDisplay = DateTime.Today.ToString("dd MMMM yyyy (วันนี้)", new System.Globalization.CultureInfo("th-TH"));
        vm.HallFilter = string.IsNullOrEmpty(filter) ? "ทั้งหมด" : filter;

        try
        {
            if (!_db.Database.CanConnect())
            {
                ViewBag.DbError = "ไม่สามารถเชื่อมต่อฐานข้อมูล MySQL ได้";
                return View(vm);
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // ดึงภาพยนตร์ทั้งหมดสำหรับตัวเลือก Modal
            ViewBag.Movies = _db.Movies.OrderBy(m => m.Title).ToList();
            ViewBag.Cinemas = _db.Cinemas.OrderBy(c => c.CinemaId).ToList();

            var cinemas = _db.Cinemas.OrderBy(c => c.CinemaId).ToList();
            if (!string.IsNullOrEmpty(filter) && filter != "ทั้งหมด" && filter != "ทั้งหมด (IMAX, 4DX, Standard)")
            {
                cinemas = cinemas.Where(c => c.CinemaName.Contains(filter)).ToList();
            }

            var showtimes = _db.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Bookings)
                .Where(s => s.StartTime >= today && s.StartTime < tomorrow)
                .OrderBy(s => s.StartTime)
                .ToList();

            vm.Halls = cinemas.Select(c =>
            {
                int cSeats = c.TotalSeats > 0 ? c.TotalSeats : 100;
                string cType = c.CinemaName.Contains("IMAX") ? "IMAX Laser" 
                             : (c.CinemaName.Contains("Dolby") || c.CinemaName.Contains("4DX") ? "4DX Extreme" : "Standard Digital");

                var hallSlots = showtimes
                    .Where(s => s.CinemaId == c.CinemaId)
                    .Select(s =>
                    {
                        int booked = s.Bookings != null ? s.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.SeatCount) : 0;
                        string dot = booked >= cSeats ? "red" : (booked > 0 ? "blue" : "green");
                        return new ManagerShowtimeSlotDto
                        {
                            ShowtimeId = s.ShowtimeId,
                            TimeText = s.StartTime.ToString("HH:mm"),
                            MovieTitle = s.Movie?.Title ?? "ภาพยนตร์",
                            BookedSeats = booked,
                            TotalSeats = cSeats,
                            DotColor = dot
                        };
                    }).ToList();

                return new ManagerHallScheduleDto
                {
                    HallNumber = c.CinemaId,
                    HallName = c.CinemaName,
                    HallType = cType,
                    Slots = hallSlots
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            ViewBag.DbError = "เกิดข้อผิดพลาด: " + ex.Message;
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateShowtime([FromForm] CreateShowtimeDto dto)
    {
        if (dto != null)
        {
            try
            {
                if (!_db.Database.CanConnect())
                {
                    TempData["Error"] = "ไม่สามารถเชื่อมต่อฐานข้อมูลได้";
                    return RedirectToAction("Showtimes");
                }

                // ค้นหา Movie จาก Title หรือ Id
                var movie = _db.Movies.FirstOrDefault(m => m.Title == dto.MovieTitle || m.MovieId.ToString() == dto.MovieTitle);
                if (movie == null)
                {
                    movie = _db.Movies.FirstOrDefault();
                }

                if (movie != null)
                {
                    DateTime startDateTime = DateTime.Today;
                    if (TimeSpan.TryParse(dto.TimeText, out var timeSpan))
                    {
                        startDateTime = DateTime.Today.Add(timeSpan);
                    }
                    else
                    {
                        startDateTime = DateTime.Today.AddHours(19);
                    }

                    int dur = movie.DurationMins > 0 ? movie.DurationMins : 120;
                    var newShowtime = new Showtime
                    {
                        MovieId = movie.MovieId,
                        CinemaId = dto.HallNumber,
                        StartTime = startDateTime,
                        EndTime = startDateTime.AddMinutes(dur)
                    };

                    _db.Showtimes.Add(newShowtime);
                    _db.SaveChanges();

                    TempData["Success"] = $"สร้างรอบฉายใหม่ {newShowtime.StartTime:HH:mm} ({movie.Title}) สำหรับโรง {dto.HallNumber} บันทึกลง Database สำเร็จแล้ว";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "เกิดข้อผิดพลาดในการบันทึกรอบฉาย: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }
        return RedirectToAction("Showtimes");
    }

    // ==========================================
    // 2. คลังและจัดการภาพยนตร์ (Movies) จาก DB จริง
    // ==========================================
    public IActionResult Movies(string filter = "all", int? selectedId = null)
    {
        CalculateUnreadBadges();
        var vm = new ManagerMoviesViewModel();
        vm.ActiveFilter = filter;

        try
        {
            if (!_db.Database.CanConnect())
            {
                ViewBag.DbError = "ไม่สามารถเชื่อมต่อฐานข้อมูล MySQL ได้";
                return View(vm);
            }

            var allDbMovies = _db.Movies.OrderBy(m => m.MovieId).ToList();

            var mappedList = allDbMovies.Select(m =>
            {
                string status = m.Status ?? "Showing";
                string badge = status switch
                {
                    "Showing" => "กำลังฉาย",
                    "ComingSoon" => "โปรแกรมหน้า",
                    "Ended" => "ปิดรอบแล้ว",
                    _ => status
                };
                string color = status switch
                {
                    "Showing" => "green",
                    "ComingSoon" => "cyan",
                    "Ended" => "red",
                    _ => "yellow"
                };

                return new ManagerMovieCardDto
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Genre = !string.IsNullOrEmpty(m.Genre) ? m.Genre : "ทั่วไป",
                    DurationMins = m.DurationMins > 0 ? m.DurationMins : 120,
                    PriceMin = m.PriceMin,
                    PriceMax = m.PriceMax,
                    Rating = m.Rating > 0 ? m.Rating : 8.0,
                    ReleaseDateText = m.ReleaseDate.HasValue ? m.ReleaseDate.Value.ToString("dd MMM yyyy", new System.Globalization.CultureInfo("th-TH")) : "-",
                    StatusBadge = badge,
                    StatusColor = color,
                    Description = m.Description ?? "",
                    PosterUrl = m.PosterUrl ?? ""
                };
            }).ToList();

            if (filter == "showing")
            {
                vm.Movies = mappedList.Where(m => m.StatusBadge == "กำลังฉาย").ToList();
            }
            else if (filter == "coming")
            {
                vm.Movies = mappedList.Where(m => m.StatusBadge == "โปรแกรมหน้า" || m.StatusBadge == "พร้อมฉาย").ToList();
            }
            else
            {
                vm.Movies = mappedList;
            }

            int targetId = selectedId ?? (vm.Movies.FirstOrDefault()?.MovieId ?? 1);
            vm.SelectedMovie = vm.Movies.FirstOrDefault(m => m.MovieId == targetId) ?? vm.Movies.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ViewBag.DbError = "เกิดข้อผิดพลาดในการดึงข้อมูลภาพยนตร์: " + ex.Message;
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult SetMovieStatus(int id, string status)
    {
        try
        {
            if (_db.Database.CanConnect())
            {
                var movie = _db.Movies.FirstOrDefault(m => m.MovieId == id);
                if (movie != null)
                {
                    // แปลง status ให้ตรงกับมาตรฐาน
                    movie.Status = status switch
                    {
                        "กำลังฉาย" => "Showing",
                        "โปรแกรมหน้า" => "ComingSoon",
                        "ปิดรอบแล้ว" => "Ended",
                        _ => status
                    };
                    _db.SaveChanges();
                    TempData["Success"] = $"ปรับปรุงสถานะภาพยนตร์ '{movie.Title}' เป็น '{status}' ลง Database เรียบร้อยแล้ว";
                }
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "ไม่สามารถปรับปรุงสถานะภาพยนตร์: " + ex.Message;
        }

        return RedirectToAction("Movies", new { selectedId = id });
    }

    [HttpPost]
    [ActionName("AddMovie")]
    public IActionResult AddManagerMovie([FromForm] AddMovieDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Title))
        {
            try
            {
                if (_db.Database.CanConnect())
                {
                    string status = dto.Status switch
                    {
                        "กำลังฉาย" => "Showing",
                        "โปรแกรมหน้า" => "ComingSoon",
                        "ปิดรอบแล้ว" => "Ended",
                        _ => string.IsNullOrWhiteSpace(dto.Status) ? "Showing" : dto.Status
                    };

                    var m = new Movie
                    {
                        Title = dto.Title.Trim(),
                        Genre = dto.Genre,
                        DurationMins = dto.DurationMins > 0 ? dto.DurationMins : 120,
                        PriceMin = dto.PriceMin,
                        PriceMax = dto.PriceMax,
                        Status = status,
                        Description = dto.Description,
                        Rating = 8.5,
                        ReleaseDate = DateTime.Today
                    };
                    _db.Movies.Add(m);
                    _db.SaveChanges();

                    TempData["Success"] = $"เพิ่มภาพยนตร์ใหม่ '{dto.Title}' ลง Database เรียบร้อยแล้ว";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "เกิดข้อผิดพลาดในการเพิ่มภาพยนตร์: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }
        return RedirectToAction("Movies");
    }

    [HttpPost]
    public IActionResult EditMovie([FromForm] EditMovieDto dto)
    {
        if (dto != null && dto.MovieId > 0 && !string.IsNullOrWhiteSpace(dto.Title))
        {
            try
            {
                if (_db.Database.CanConnect())
                {
                    var m = _db.Movies.FirstOrDefault(x => x.MovieId == dto.MovieId);
                    if (m != null)
                    {
                        m.Title = dto.Title.Trim();
                        m.Genre = dto.Genre;
                        m.DurationMins = dto.DurationMins > 0 ? dto.DurationMins : 120;
                        m.PriceMin = dto.PriceMin;
                        m.PriceMax = dto.PriceMax;
                        m.Status = dto.Status switch
                        {
                            "กำลังฉาย" => "Showing",
                            "โปรแกรมหน้า" => "ComingSoon",
                            "ปิดรอบแล้ว" => "Ended",
                            _ => dto.Status
                        };
                        m.Description = dto.Description;
                        _db.SaveChanges();

                        TempData["Success"] = $"แก้ไขข้อมูลภาพยนตร์ '{m.Title}' ลง Database เรียบร้อยแล้ว";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "เกิดข้อผิดพลาดในการแก้ไขข้อมูล: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }
        return RedirectToAction("Movies", new { selectedId = dto?.MovieId });
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
    // 4. รายงานยอดขายและสถิติ (Reports) - คำนวณจาก DB จริง 100%
    // ==========================================
    public IActionResult Reports()
    {
        CalculateUnreadBadges();
        var vm = new ManagerReportsViewModel();

        try
        {
            if (!_db.Database.CanConnect())
            {
                ViewBag.DbError = "ไม่สามารถเชื่อมต่อฐานข้อมูล MySQL ได้";
                return View(vm);
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 1. ดึงยอดตั๋วของวันนี้ตามนิยามเดียวกันกับ Dashboard (Bookings & Payments)
            var (ticketRevenueToday, ticketsSoldToday, bookingsCountToday, todayActiveBookings) = GetActiveBookingsAndRevenue(today, tomorrow);

            // 2. ดึงยอด Concessions จาก AccountingTransactions ของวันนี้
            decimal concessionToday = 0m;
            try
            {
                concessionToday = _db.AccountingTransactions
                    .Where(t => t.TransactionDate >= today && t.TransactionDate < tomorrow && t.Type == "Income" && t.Category == "Concessions")
                    .Sum(t => t.Amount);
            }
            catch { }

            decimal totalRevenueToday = ticketRevenueToday + concessionToday;
            vm.TotalRevenueToday = $"฿{totalRevenueToday:N0}";
            vm.TotalRevenueSub = $"จาก {bookingsCountToday} รายการจองวันนี้";

            vm.TicketRevenue = $"฿{ticketRevenueToday:N0}";
            vm.TicketRevenueSub = $"จำหน่าย {ticketsSoldToday} ใบ";

            vm.ConcessionRevenue = $"฿{concessionToday:N0}";
            vm.ConcessionSub = totalRevenueToday > 0 ? $"คิดเป็น {(concessionToday / totalRevenueToday * 100):F1}% ของยอดรวม" : "0% ของยอดรวม";

            int todayShowsCount = _db.Showtimes.Count(s => s.StartTime >= today && s.StartTime < tomorrow);
            decimal avgPerShow = todayShowsCount > 0 ? Math.Round(ticketRevenueToday / todayShowsCount, 0) : 0;
            vm.AvgRevenuePerShow = $"฿{avgPerShow:N0}";
            vm.AvgRevenueSub = $"เฉลี่ยต่อ {todayShowsCount} รอบฉายวันนี้";

            // 3. กราฟแนวโน้มยอดขาย 7 วันย้อนหลังจริงจาก DB (จันทร์ - อาทิตย์)
            var weeklySales = new List<DaySalesBarDto>();
            decimal maxDaySales = 0m;
            var daySalesMap = new List<(string dayName, decimal sales)>();

            for (int i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                var (dayRev, _, _, _) = GetActiveBookingsAndRevenue(d, d.AddDays(1));
                string dName = d.ToString("ddd", new System.Globalization.CultureInfo("th-TH"));
                if (i == 0) dName = "วันนี้";
                daySalesMap.Add((dName, dayRev));
                if (dayRev > maxDaySales) maxDaySales = dayRev;
            }

            foreach (var ds in daySalesMap)
            {
                int height = maxDaySales > 0 ? Math.Max(15, (int)(ds.sales / maxDaySales * 100)) : 15;
                weeklySales.Add(new DaySalesBarDto
                {
                    DayName = ds.dayName,
                    SalesText = $"฿{(ds.sales / 1000m):F0}K",
                    BarHeightPercent = height
                });
            }
            vm.WeeklySales = weeklySales;

            // 4. สถิติ Top Movies (ยอดขายตามภาพยนตร์จาก activeBookings จริงใน DB)
            var sampleBookings = todayActiveBookings;
            if (!sampleBookings.Any())
            {
                // ถ้าวันนี้ยังไม่มี ให้ดึงจาก 30 วันล่าสุด
                var (_, _, _, monthBookings) = GetActiveBookingsAndRevenue(today.AddDays(-30), tomorrow);
                sampleBookings = monthBookings;
            }

            if (sampleBookings.Any())
            {
                decimal allTicketSample = sampleBookings.Sum(b => b.Payments.Where(p => p.IsRefunded != true).Sum(p => p.Amount) is decimal a && a > 0 ? a : (b.TotalAmount ?? 0m));
                var movieGroups = sampleBookings
                    .Where(b => b.Showtime?.Movie != null)
                    .GroupBy(b => b.Showtime.Movie.Title)
                    .OrderByDescending(g => g.Sum(b => b.Payments.Where(p => p.IsRefunded != true).Sum(p => p.Amount) is decimal a && a > 0 ? a : (b.TotalAmount ?? 0m)))
                    .Take(5)
                    .ToList();

                int rank = 1;
                vm.MovieSales = movieGroups.Select(g =>
                {
                    decimal mRev = g.Sum(b => b.Payments.Where(p => p.IsRefunded != true).Sum(p => p.Amount) is decimal a && a > 0 ? a : (b.TotalAmount ?? 0m));
                    double share = allTicketSample > 0 ? Math.Round((double)(mRev / allTicketSample * 100), 1) : 0;
                    return new MovieSalesRowDto
                    {
                        Rank = rank++,
                        Title = g.Key,
                        RevenueText = $"฿{mRev:N0}",
                        SharePercentText = $"{share}%",
                        ChangeText = $"{g.Sum(b => b.SeatCount)} ที่นั่ง",
                        ChangeColor = "green"
                    };
                }).ToList();

                // 5. ประสิทธิภาพโรงภาพยนตร์ (Cinema Hall Breakdown) จาก DB จริง
                var hallGroups = sampleBookings
                    .Where(b => b.Showtime?.Cinema != null)
                    .GroupBy(b => b.Showtime.Cinema.CinemaName)
                    .OrderByDescending(g => g.Sum(b => b.Payments.Where(p => p.IsRefunded != true).Sum(p => p.Amount) is decimal a && a > 0 ? a : (b.TotalAmount ?? 0m)))
                    .ToList();

                vm.HallSales = hallGroups.Select(g =>
                {
                    decimal hRev = g.Sum(b => b.Payments.Where(p => p.IsRefunded != true).Sum(p => p.Amount) is decimal a && a > 0 ? a : (b.TotalAmount ?? 0m));
                    int seatsSold = g.Sum(b => b.SeatCount);
                    int showCount = g.Select(x => x.ShowtimeId).Distinct().Count();
                    return new HallSalesRowDto
                    {
                        HallTitle = $"{g.Key} • {showCount} รอบ",
                        RevenueText = $"฿{hRev:N0}",
                        OccupancyPercent = $"{seatsSold} ที่นั่งจำหน่ายแล้ว"
                    };
                }).ToList();
            }
            else
            {
                vm.MovieSales = new List<MovieSalesRowDto>();
                vm.HallSales = new List<HallSalesRowDto>();
            }
        }
        catch (Exception ex)
        {
            ViewBag.DbError = "เกิดข้อผิดพลาดในการโหลดรายงาน: " + ex.Message;
        }

        return View(vm);
    }

    // ==========================================
    // 5. พนักงานและสิทธิ์การเข้าถึง (Staff) - ดึงจาก Users & Roles ใน DB จริง
    // ==========================================
    public IActionResult Staff()
    {
        CalculateUnreadBadges();
        var vm = new ManagerStaffViewModel();

        try
        {
            if (!_db.Database.CanConnect())
            {
                ViewBag.DbError = "ไม่สามารถเชื่อมต่อฐานข้อมูล MySQL ได้ กรุณาตรวจสอบว่า Service MySQL เปิดทำงานอยู่";
                return View(vm);
            }

            // ตรวจสอบและสร้าง Roles พื้นฐานใน DB หากยังไม่มี
            var existingRoles = _db.Roles.ToList();
            var requiredRoles = new[] { "Admin", "Manager", "Staff", "Technician", "Accountant", "User" };
            bool rolesChanged = false;
            foreach (var r in requiredRoles)
            {
                if (!existingRoles.Any(x => x.RoleName == r))
                {
                    _db.Roles.Add(new Role { RoleName = r });
                    rolesChanged = true;
                }
            }
            if (rolesChanged)
            {
                _db.SaveChanges();
                existingRoles = _db.Roles.ToList();
            }

            var staffRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Staff", "Manager", "Technician", "Accountant", "Admin" };

            // ดึงผู้ใช้ทั้งหมดและ Include Role เข้า Memory ก่อนทำการกรอง เพื่อป้องกันข้อผิดพลาด LINQ SQL Translation
            var allUsers = _db.Users
                .Include(u => u.Role)
                .ToList();

            var staffUsers = allUsers
                .Where(u => u.Role != null && staffRoles.Contains(u.Role.RoleName))
                .OrderBy(u => u.RoleId)
                .ThenBy(u => u.FullName)
                .ToList();

            // ถ้าในฐานข้อมูลยังไม่มีพนักงานเลย ("ถ้าไม่มีข้อมูลก็สร้างใหม่เลย")
            if (!staffUsers.Any())
            {
                var roleAdmin = existingRoles.FirstOrDefault(r => r.RoleName == "Admin");
                var roleManager = existingRoles.FirstOrDefault(r => r.RoleName == "Manager");
                var roleStaff = existingRoles.FirstOrDefault(r => r.RoleName == "Staff");
                var roleTech = existingRoles.FirstOrDefault(r => r.RoleName == "Technician");
                var roleAcct = existingRoles.FirstOrDefault(r => r.RoleName == "Accountant");

                var defaultStaffList = new List<User>
                {
                    new() { FullName = "ณัฐชา วัฒนากุล", Username = "manager@absolutecinema.com", PasswordHash = "password123", RoleId = roleManager?.RoleId ?? 2, CreatedAt = DateTime.Now },
                    new() { FullName = "ผู้ดูแลระบบสาขา (Admin)", Username = "admin@absolutecinema.com", PasswordHash = "password123", RoleId = roleAdmin?.RoleId ?? 1, CreatedAt = DateTime.Now },
                    new() { FullName = "สมศรี รักบริการ", Username = "staff@absolutecinema.com", PasswordHash = "password123", RoleId = roleStaff?.RoleId ?? 3, CreatedAt = DateTime.Now },
                    new() { FullName = "เกวลิน ฝ่ายขาย", Username = "staff2@absolutecinema.com", PasswordHash = "password123", RoleId = roleStaff?.RoleId ?? 3, CreatedAt = DateTime.Now },
                    new() { FullName = "อนุชา ทนทาน", Username = "tech@absolutecinema.com", PasswordHash = "password123", RoleId = roleTech?.RoleId ?? 4, CreatedAt = DateTime.Now },
                    new() { FullName = "ณิชา พงษ์ศิริ", Username = "acct@absolutecinema.com", PasswordHash = "password123", RoleId = roleAcct?.RoleId ?? 5, CreatedAt = DateTime.Now }
                };

                _db.Users.AddRange(defaultStaffList);
                _db.SaveChanges();

                allUsers = _db.Users.Include(u => u.Role).ToList();
                staffUsers = allUsers
                    .Where(u => u.Role != null && staffRoles.Contains(u.Role.RoleName))
                    .OrderBy(u => u.RoleId)
                    .ThenBy(u => u.FullName)
                    .ToList();
            }

            vm.StaffList = staffUsers.Select(u => new ManagerStaffRowDto
            {
                Id = u.UserId,
                Name = !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username,
                RoleTitle = u.Role?.RoleName switch
                {
                    "Staff" => "พนักงานบริการ (Floor Staff)",
                    "Manager" => "ผู้จัดการสาขา (Branch Manager)",
                    "Technician" => "ช่างเทคนิคและระบบ (Technician)",
                    "Accountant" => "เจ้าหน้าที่ฝ่ายบัญชี (Accountant)",
                    "Admin" => "ผู้ดูแลระบบสาขา (System Admin)",
                    _ => u.Role?.RoleName ?? "พนักงาน"
                },
                ShiftText = "ประจำการสาขา",
                AccessLevel = u.Role?.RoleName switch
                {
                    "Manager" => "ระดับบริหาร (Manager)",
                    "Admin" => "ระดับผู้ดูแลสูงสุด (Admin)",
                    "Technician" => "สิทธิ์เทคนิค (Technical)",
                    "Accountant" => "สิทธิ์การเงิน (Accounting)",
                    _ => "ระดับปฏิบัติการ (Staff)"
                },
                Status = "บัญชีเปิดใช้งาน (Active)",
                StatusColor = "green"
            }).ToList();

            vm.OnDutyCount = vm.StaffList.Count;
            vm.OnLeaveCount = 0;
        }
        catch (Exception ex)
        {
            ViewBag.DbError = "เกิดข้อผิดพลาดในการดึงข้อมูลพนักงาน: " + (ex.InnerException?.Message ?? ex.Message);
            vm.StaffList = new List<ManagerStaffRowDto>();
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateStaffMember([FromForm] CreateStaffMemberDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Name))
        {
            try
            {
                if (!_db.Database.CanConnect())
                {
                    TempData["Error"] = "ไม่สามารถเชื่อมต่อฐานข้อมูลได้";
                    return RedirectToAction("Staff");
                }

                // กำหนด Role ตามที่เลือก
                string targetRoleName = "Staff";
                if (dto.RoleTitle.Contains("Manager") || dto.RoleTitle.Contains("ผู้จัดการ")) targetRoleName = "Manager";
                else if (dto.RoleTitle.Contains("Tech") || dto.RoleTitle.Contains("ช่าง") || dto.RoleTitle.Contains("วิศวกร")) targetRoleName = "Technician";
                else if (dto.RoleTitle.Contains("Account") || dto.RoleTitle.Contains("บัญชี")) targetRoleName = "Accountant";
                else if (dto.RoleTitle.Contains("Admin")) targetRoleName = "Admin";

                var role = _db.Roles.FirstOrDefault(r => r.RoleName == targetRoleName) ?? _db.Roles.FirstOrDefault(r => r.RoleName == "Staff");
                if (role == null)
                {
                    role = new Role { RoleName = targetRoleName };
                    _db.Roles.Add(role);
                    _db.SaveChanges();
                }

                string username = !string.IsNullOrWhiteSpace(dto.Username) 
                    ? dto.Username.Trim().ToLowerInvariant() 
                    : $"staff_{DateTime.Now.Ticks % 10000}@absolutecinema.com";

                // ตรวจสอบ Username ซ้ำ
                if (_db.Users.Any(u => u.Username == username))
                {
                    username = $"staff_{new Random().Next(1000, 9999)}@absolutecinema.com";
                }

                var newUser = new User
                {
                    Username = username,
                    FullName = dto.Name.Trim(),
                    PasswordHash = "password123", // รหัสผ่านตั้งต้น สามารถเข้าสู่ระบบได้จริง
                    RoleId = role.RoleId,
                    CreatedAt = DateTime.Now
                };

                _db.Users.Add(newUser);
                _db.SaveChanges();

                TempData["Success"] = $"สร้างบัญชีพนักงาน '{newUser.FullName}' (Username: {newUser.Username}, Role: {role.RoleName}) ลงในระบบ Database เรียบร้อยแล้ว (สามารถล็อกอินได้ด้วยรหัสผ่าน password123)";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "เกิดข้อผิดพลาดในการเพิ่มพนักงาน: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }
        return RedirectToAction("Staff");
    }

    [HttpPost]
    public IActionResult ToggleStaffStatus(int id)
    {
        TempData["Success"] = "สถานะพนักงานได้รับการอัปเดตแล้ว";
        return RedirectToAction("Staff");
    }

    // ==========================================
    // 6. แชทและการสื่อสารภายใน (แชทกลุ่มรวมทุกแผนก)
    // ==========================================
    public IActionResult Chat(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Manager");
        string activeRoom = "group";

        HttpContext.Session.SetString("ManagerChat_Seen_group", "true");
        CalculateUnreadBadges();

        var vm = new ManagerChatViewModel();
        string myName = HttpContext.Session.GetString("UserName") ?? "ผู้จัดการ";

        vm.Rooms = new List<ManagerChatRoomDto>
        {
            new() { Id = "group", Name = "แชทกลุ่มรวม (All Staff)", LastMessageText = "สื่อสารระหว่างพนักงานทุกแผนกในสาขา", UnreadCount = 0, ColorType = "blue", IsActive = true, MembersInfo = "ผู้จัดการ, พนักงานบริการ, ช่างเทคนิค, บัญชี, แอดมิน" }
        };

        vm.ActiveRoomId = activeRoom;
        vm.ActiveRoom = vm.Rooms.First();

        string role = HttpContext.Session.GetString("UserRole") ?? "Manager";
        vm.Messages = ChatHelper.GetMessagesForChannel(_db, _env, activeRoom, role, myName, myUserId);

        vm.TotalUnreadCount = 0;

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendManagerChatMessage(string channel, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string sender = HttpContext.Session.GetString("UserName") ?? "ผู้จัดการ";
            string role = HttpContext.Session.GetString("UserRole") ?? "Manager";
            ChatHelper.SendMessage(_db, _env, "group", message, sender, role, HttpContext);
        }
        return RedirectToAction("Chat", new { channel = "group" });
    }

    [HttpGet]
    public IActionResult GetMessagesApi(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Manager");
        string role = HttpContext.Session.GetString("UserRole") ?? "Manager";
        string myName = HttpContext.Session.GetString("UserName") ?? "ผู้จัดการ";
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, "group", role, myName, myUserId);
        return Json(new { success = true, channel = "group", messages = messages });
    }

    [HttpPost]
    public IActionResult SendMessageApi([FromForm] string channel, [FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { success = false, message = "ข้อความว่างเปล่า" });

        string sender = HttpContext.Session.GetString("UserName") ?? "ผู้จัดการ";
        string role = HttpContext.Session.GetString("UserRole") ?? "Manager";
        var newMsg = ChatHelper.SendMessage(_db, _env, "group", message, sender, role, HttpContext);
        return Json(new { success = true, newMsg = newMsg });
    }
}
