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
    public string Subtitle { get; set; } = "";
    public string Status { get; set; } = "";
    public string BadgeColor { get; set; } = "yellow";
    public bool CanSetReady { get; set; }
}

public class ManagerTeamChatNotificationDto
{
    public string DepartmentName { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public int UnreadCount { get; set; }
    public string AvatarColor { get; set; } = "blue";
}

// ==========================================
// 1. Showtimes Page Models
// ==========================================
public class ManagerShowtimeSlotDto
{
    public int ShowtimeId { get; set; }
    public string TimeText { get; set; } = ""; // 11:00
    public string MovieTitle { get; set; } = ""; // Dune: Part Two
    public int BookedSeats { get; set; }
    public int TotalSeats { get; set; }
    public string DotColor { get; set; } = "green"; // green, red, blue
}

public class ManagerHallScheduleDto
{
    public int HallNumber { get; set; }
    public string HallName { get; set; } = ""; // โรง 1
    public string HallType { get; set; } = ""; // IMAX Laser
    public List<ManagerShowtimeSlotDto> Slots { get; set; } = new();
}

public class ManagerShowtimesViewModel
{
    public string DateDisplay { get; set; } = "15 กันยายน 2569 (วันนี้)";
    public string HallFilter { get; set; } = "ทั้งหมด (IMAX, 4DX, Standard)";
    public List<ManagerHallScheduleDto> Halls { get; set; } = new();
}

public class CreateShowtimeDto
{
    public int HallNumber { get; set; } = 1;
    public string MovieTitle { get; set; } = "Dune: Part Two";
    public string TimeText { get; set; } = "19:00";
    public int TotalSeats { get; set; } = 220;
}

// ==========================================
// 2. Movies Management Page Models
// ==========================================
public class ManagerMovieCardDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "";
    public int DurationMins { get; set; } = 120;
    public decimal PriceMin { get; set; }
    public decimal PriceMax { get; set; }
    public double Rating { get; set; }
    public string ReleaseDateText { get; set; } = "";
    public string StatusBadge { get; set; } = "กำลังฉาย";
    public string StatusColor { get; set; } = "green";
    public string Description { get; set; } = "";
    public string PosterUrl { get; set; } = "";
}

public class ManagerMoviesViewModel
{
    public List<ManagerMovieCardDto> Movies { get; set; } = new();
    public ManagerMovieCardDto? SelectedMovie { get; set; }
    public string ActiveFilter { get; set; } = "all";
}

// ==========================================
// 3. Promotions Page Models
// ==========================================
public class ManagerPromotionItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = ""; // อังคารคู่สุดคุ้ม ฿299
    public string Description { get; set; } = ""; // ส่วนลดสำหรับตั๋ว 2 ใบวันอังคาร
    public string DiscountType { get; set; } = ""; // ส่วนลดราคาตั๋ว
    public string DurationText { get; set; } = ""; // 1 ม.ค. - 30 ก.ย. 69
    public string UsageCountText { get; set; } = ""; // 1,245 ครั้ง
    public string Status { get; set; } = "กำลังใช้งาน"; // กำลังใช้งาน, หมดอายุแล้ว, ฉบับร่าง
    public string StatusColor { get; set; } = "green"; // green, grey, amber
}

public class ManagerPromotionsViewModel
{
    public string HotTitle { get; set; } = "อังคารคู่สุดคุ้ม ฿299 ( weekday special )";
    public string HotDesc { get; set; } = "กระตุ้นยอดขายรอบเช้า-บ่าย วันธรรมดาเพิ่มขึ้นถึง 34% ของเป้าหมายสัปดาห์นี้";
    public string HotSub { get; set; } = "แคมเปญเปิดทำงานอัตโนมัติ • สิ้นสุด 30 ก.ย. 2569";
    public List<ManagerPromotionItemDto> Promotions { get; set; } = new();
}

public class CreatePromotionDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string DiscountType { get; set; } = "ส่วนลดราคาตั๋ว";
    public string DurationText { get; set; } = "1 ต.ค. - 31 ธ.ค. 69";
    public string Status { get; set; } = "กำลังใช้งาน";
}

// ==========================================
// 4. Sales Reports Page Models
// ==========================================
public class DaySalesBarDto
{
    public string DayName { get; set; } = ""; // จันทร์
    public string SalesText { get; set; } = "฿120K";
    public int BarHeightPercent { get; set; } = 40;
}

public class MovieSalesRowDto
{
    public int Rank { get; set; }
    public string Title { get; set; } = "";
    public string RevenueText { get; set; } = "";
    public string SharePercentText { get; set; } = "";
    public string ChangeText { get; set; } = "";
    public string ChangeColor { get; set; } = "green";
}

public class HallSalesRowDto
{
    public string HallTitle { get; set; } = ""; // โรง 1 (IMAX) ฉาย 3 รอบ
    public string RevenueText { get; set; } = "฿142,500";
    public string OccupancyPercent { get; set; } = "84.5% อัตราที่นั่ง";
}

public class ManagerReportsViewModel
{
    public string TotalRevenueToday { get; set; } = "฿428,650";
    public string TotalRevenueSub { get; set; } = "+12.4% เทียบสัปดาห์ก่อน";
    public string TicketRevenue { get; set; } = "฿295,400";
    public string TicketRevenueSub { get; set; } = "รวม 1,842 ใบจำหน่าย";
    public string ConcessionRevenue { get; set; } = "฿133,250";
    public string ConcessionSub { get; set; } = "คิดเป็น 31% ของยอดรวม";
    public string AvgRevenuePerShow { get; set; } = "฿8,930";
    public string AvgRevenueSub { get; set; } = "+5.2% การเติบโตต่อที่นั่ง";

    public List<DaySalesBarDto> WeeklySales { get; set; } = new();
    public List<MovieSalesRowDto> MovieSales { get; set; } = new();
    public List<HallSalesRowDto> HallSales { get; set; } = new();
}

// ==========================================
// 5. Staff & Roles Page Models
// ==========================================
public class ManagerStaffRowDto
{
    public int Id { get; set; }
    public string Name { get; set; } = ""; // สมเกียรติ แดนสวรรค์
    public string RoleTitle { get; set; } = ""; // พนักงานขายตั๋ว
    public string ShiftText { get; set; } = ""; // เวรเช้า (08:00 - 16:00)
    public string AccessLevel { get; set; } = ""; // ระดับเริ่มต้น
    public string Status { get; set; } = "ปฏิบัติงาน (On-duty)"; // ปฏิบัติงาน (On-duty), ลากิจ, ออฟฟิศนี้
    public string StatusColor { get; set; } = "green"; // green, red, grey
}

public class ManagerStaffViewModel
{
    public int OnDutyCount { get; set; } = 12;
    public int OnLeaveCount { get; set; } = 2;
    public List<ManagerStaffRowDto> StaffList { get; set; } = new();
}

public class CreateStaffMemberDto
{
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string RoleTitle { get; set; } = "Staff";
    public string ShiftText { get; set; } = "เวรเช้า (08:00 - 16:00)";
    public string AccessLevel { get; set; } = "ระดับเริ่มต้น";
}

public class EditMovieDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "";
    public int DurationMins { get; set; } = 120;
    public decimal PriceMin { get; set; } = 200;
    public decimal PriceMax { get; set; } = 350;
    public string Status { get; set; } = "Showing";
    public string Description { get; set; } = "";
    public string? PosterUrl { get; set; }
}

// ==========================================
// 6. Organization Chat Page Models
// ==========================================
public class ManagerChatRoomDto
{
    public string Id { get; set; } = ""; // admin, accounting, technician, staff
    public string Name { get; set; } = ""; // แอดมินสาขา
    public string LastMessageText { get; set; } = "";
    public int UnreadCount { get; set; }
    public string ColorType { get; set; } = "gold"; // gold, blue, green, brown
    public bool IsActive { get; set; }
    public string MembersInfo { get; set; } = "สมาชิกทั้งหมด 6 คน • ออนไลน์ 3 คน";
}

public class ManagerChatMessageDto
{
    public int Id { get; set; }
    public string ChannelId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string TimeText { get; set; } = "";
    public string MessageText { get; set; } = "";
    public bool IsMine { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ManagerChatViewModel
{
    public string ActiveRoomId { get; set; } = "admin";
    public ManagerChatRoomDto ActiveRoom { get; set; } = new();
    public List<ManagerChatRoomDto> Rooms { get; set; } = new();
    public List<ManagerChatMessageDto> Messages { get; set; } = new();
    public int TotalUnreadCount { get; set; }
}

public class AddMovieDto
{
    public string Title { get; set; } = "";
    public string ThaiTitle { get; set; } = "";
    public string Genre { get; set; } = "แอ็คชั่น / ไซไฟ";
    public int DurationMins { get; set; } = 120;
    public decimal PriceMin { get; set; } = 220;
    public decimal PriceMax { get; set; } = 350;
    public string Status { get; set; } = "Showing";
    public string Description { get; set; } = "";
    public string DirectorAndCast { get; set; } = "";
}
