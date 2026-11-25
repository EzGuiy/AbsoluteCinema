using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.ViewModels;

public class StaffDashboardViewModel
{
    // 4 KPI Stat Cards
    public int ExpectedCustomersToday { get; set; } = 1842;
    public string ExpectedCustomersSubtext { get; set; } = "+155 คนเทียบกับเมื่อวาน";
    public string ExpectedCustomersChange { get => ExpectedCustomersSubtext; set => ExpectedCustomersSubtext = value; }

    public int TodayBookingsCount { get; set; } = 892;
    public string TodayBookingsSubtext { get; set; } = "จองผ่านออนไลน์ 73% (651 รายการ)";

    public int ActiveHallsCount { get; set; } = 8;
    public string ActiveHallsSubtext { get; set; } = "กำลังฉาย 5 โรง • เตรียมโรง 3 โรง";

    public double HallsReadinessPercent { get; set; } = 87.5;
    public string HallsReadinessSubtext { get; set; } = "ตรวจความพร้อมแล้ว 7 จาก 8 โรง";

    // Cinema Halls Grid
    public List<CinemaHallCardDto> Halls { get; set; } = new();

    // Staff Shift & Checklist Tasks
    public List<StaffShiftTaskDto> ShiftTasks { get; set; } = new();

    // รายการรหัสตั๋วและการจองล่าสุดของลูกค้าฝั่งพนักงาน
    public List<StaffRecentBookingDto> RecentBookings { get; set; } = new();

    public string StaffName { get; set; } = "สมศรี รักบริการ";
    public string StaffRole { get; set; } = "พนักงานบริการลูกค้า (Floor Staff)";
    public string ShiftText { get; set; } = "กะปฏิบัติการ: 09:00 - 18:00 น.";
}

public class StaffRecentBookingDto
{
    public int BookingId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string MovieTitle { get; set; } = "";
    public string CinemaName { get; set; } = "";
    public string ShowtimeText { get; set; } = "";
    public string SeatNumbers { get; set; } = "";
    public int SeatCount { get; set; } = 1;
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Confirmed";
    public string BookedAtText { get; set; } = "";
}

public class CinemaHallCardDto
{
    public int HallId { get; set; }
    public string HallName { get; set; } = ""; // e.g. "โรง 1 - IMAX Laser"
    public string HallType { get; set; } = ""; // IMAX, Dolby Atmos, etc.
    public int TotalSeats { get; set; } = 200;
    public int BookedSeatsCount { get; set; } = 188;
    public int AvailableSeatsCount => Math.Max(0, TotalSeats - BookedSeatsCount);
    public double OccupancyPercent => TotalSeats > 0 ? Math.Round((double)BookedSeatsCount / TotalSeats * 100, 1) : 0;

    // Movie Showing Info
    public string CurrentMovieTitle { get; set; } = "";
    public string CurrentMovieGenre { get; set; } = "";
    public string PosterUrl { get; set; } = "";
    public string StartTimeText { get; set; } = ""; // 12:30
    public string EndTimeText { get; set; } = ""; // 15:15
    public int RemainingMinutes { get; set; } = 25;
    public int ProgressPercent { get; set; } = 75;

    // Showing Status
    public string ShowingStatus { get; set; } = "กำลังฉาย"; // กำลังฉาย, ใกล้จบ - รอทำความสะอาด, กำลังทำความสะอาด, พร้อมเข้าชม
    public string ShowingStatusColor { get; set; } = "blue"; // blue, amber, orange, green

    // Cleanliness & Readiness Status
    public string CleanlinessStatus { get; set; } = "สะอาดเรียบร้อย"; // สะอาดเรียบร้อย, รอทำความสะอาด, กำลังทำความสะอาด
    public bool IsCleaned { get; set; } = true;
    public string LastCleanedTime { get; set; } = "11:45 น.";
    public string CleanedBy { get; set; } = "สมศรี";

    // Next Movie
    public string NextMovieTitle { get; set; } = "";
    public string NextShowtimeText { get; set; } = "";

    // Seats Map for this hall
    public List<SeatStatusDto> Seats { get; set; } = new();
}

public class SeatStatusDto
{
    public string SeatCode { get; set; } = ""; // e.g. C4, C5
    public string Row { get; set; } = "C";
    public int Number { get; set; } = 4;
    public string Tier { get; set; } = "Normal"; // Normal, Premium, VIP
    public bool IsBooked { get; set; }
    public bool IsCheckedIn { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string BookingCode { get; set; } = "";
    public decimal Price { get; set; }
    public string PaymentMethod { get; set; } = "PromptPay";
    public string BookedAtText { get; set; } = "";
}

public class StaffShiftTaskDto
{
    public string TimeText { get; set; } = "";
    public string HallName { get; set; } = "";
    public string TaskTitle { get; set; } = "";
    public string Status { get; set; } = "รอดำเนินการ"; // เสร็จแล้ว, กำลังทำ, รอดำเนินการ
    public string BadgeColor { get; set; } = "orange"; // green, blue, orange
}

public class CallTechnicianDto
{
    public int HallId { get; set; }
    public string HallName { get; set; } = "โรง 1";
    public string IssueType { get; set; } = "ทดสอบระบบเสียง / ลำโพง";
    public string Description { get; set; } = "";
    public string Urgency { get; set; } = "สูง";
    public string ReportedBy { get; set; } = "สมศรี (Floor Staff)";
}

public class InspectHallDto
{
    public int HallId { get; set; }
    public bool TrashCleared { get; set; }
    public bool SeatsInspected { get; set; }
    public bool FloorMopped { get; set; }
    public bool ScreenAndSoundTested { get; set; }
    public bool AcAndLightingChecked { get; set; }
    public string Notes { get; set; } = "";
    public string StaffName { get; set; } = "สมศรี รักบริการ";
}
