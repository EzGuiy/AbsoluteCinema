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

    public IActionResult Index()
    {
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

        // 4. การแจ้งเตือนแชททีมงาน (Team Chat)
        vm.ChatNotifications = new List<ManagerTeamChatNotificationDto>
        {
            new() { DepartmentName = "ฝ่ายบัญชี", LastMessage = "อนุมัติยอดเงินส่วนแบ่งรอบฉายแล้ว", UnreadCount = 2, AvatarColor = "blue" },
            new() { DepartmentName = "แอดมินสาขา", LastMessage = "ขอรอบฉายเพิ่มเรื่องหลานม่า โรง 4", UnreadCount = 3, AvatarColor = "orange" },
            new() { DepartmentName = "ช่างเทคนิคประจำสาขา", LastMessage = "แก้ไขระบบเสียงโรง 2 เรียบร้อย", UnreadCount = 1, AvatarColor = "green" },
            new() { DepartmentName = "พนักงานบริการ", LastMessage = "แจ้งขอพนักงานตรวจตั๋วเพิ่ม", UnreadCount = 2, AvatarColor = "brown" }
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

    [HttpPost]
    public IActionResult AddMovie([FromForm] AddMovieDto dto)
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

            TempData["Success"] = $"เพิ่มภาพยนตร์ '{dto.Title}' เข้าสู่ระบบแล้ว";
        }

        return RedirectToAction("Index");
    }

    public IActionResult Showtimes() => RedirectToAction("Index");
    public IActionResult Movies() => RedirectToAction("Index");
    public IActionResult Promotions() => RedirectToAction("Index");
    public IActionResult Reports() => RedirectToAction("Index");
    public IActionResult Staff() => RedirectToAction("Index");
    public IActionResult Chat() => RedirectToAction("Index");

    // Helper
    private List<MovieStatusCardDto> GetMovieStatuses()
    {
        return new List<MovieStatusCardDto>
        {
            new() { MovieId = 1, Title = "A Quiet Place: Day One", Subtitle = "กำหนดฉาย 27 มิ.ย.", Status = "โปรแกรมหน้า", BadgeColor = "gray", CanSetReady = true },
            new() { MovieId = 2, Title = "Inside Out 2", Subtitle = "เริ่มฉายวันนี้", Status = "พร้อมฉาย", BadgeColor = "yellow", CanSetReady = false },
            new() { MovieId = 3, Title = "Bad Boys: Ride or Die", Subtitle = "เหลือ 18 รอบ", Status = "กำลังฉาย", BadgeColor = "green", CanSetReady = false }
        };
    }
}

