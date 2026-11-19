using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.ViewModels;

public class ManagerDashboardViewModel
{
    // 4 Key Stat Cards
    public decimal TodayRevenue { get; set; } = 428650;
    public string RevenueChangeText { get; set; } = "+12.4%";

    public int TicketsSold { get; set; } = 1842;
    public string TicketsGoalText { get; set; } = "78% เป้าหมาย";

    public double AvgAttendancePercent { get; set; } = 78.6;
    public string AttendanceChangeText { get; set; } = "+5.2%";

    public int TodayShowtimesCount { get; set; } = 48;
    public string RemainingShowtimesText { get; set; } = "อีก 19 รอบ";

    // Upcoming showtimes
    public List<UpcomingShowtimeDto> UpcomingShowtimes { get; set; } = new();

    // Movie status management items
    public List<MovieStatusCardDto> MovieStatuses { get; set; } = new();

    // Team Chat notifications
    public List<ManagerTeamChatNotificationDto> ChatNotifications { get; set; } = new();

    // Current Branch
    public string BranchName { get; set; } = "สาขาสยามสแควร์";
    public string BranchHallsText { get; set; } = "8 โรง • เปิดให้บริการปกติ";
    public string ManagerName { get; set; } = "ณัฐชา วัฒนากุล";
}

public class UpcomingShowtimeDto
{
    public string TimeText { get; set; } = ""; // 12:30
    public string MovieTitle { get; set; } = ""; // Dune: Part Two
    public string HallAndType { get; set; } = ""; // โรง 1 • IMAX
    public string SeatsSoldText { get; set; } = ""; // 188 / 220
    public string Status { get; set; } = "กำลังขาย"; // กำลังขาย, ใกล้เต็ม
    public string StatusBadgeColor { get; set; } = "green"; // green, red
}

public class MovieStatusCardDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = ""; // กำหนดฉาย 27 มิ.ย.
    public string Status { get; set; } = ""; // พร้อมฉาย, กำลังฉาย, โปรแกรมหน้า
    public string BadgeColor { get; set; } = "yellow"; // green, yellow, gray
    public bool CanSetReady { get; set; }
}

public class ManagerTeamChatNotificationDto
{
    public string DepartmentName { get; set; } = ""; // ฝ่ายบัญชี
    public string LastMessage { get; set; } = ""; // อนุมัติยอดเงินส่วนแบ่งรอบฉายแล้ว
    public int UnreadCount { get; set; }
    public string AvatarColor { get; set; } = "blue"; // blue, orange, green, brown
}

public class AddMovieDto
{
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "แอ็คชั่น / ไซไฟ";
    public int DurationMins { get; set; } = 120;
    public decimal PriceMin { get; set; } = 220;
    public decimal PriceMax { get; set; } = 350;
    public string Status { get; set; } = "Showing";
    public string Description { get; set; } = "";
}

