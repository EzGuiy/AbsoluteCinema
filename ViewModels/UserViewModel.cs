using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.ViewModels;

public class UserViewModel
{
    public int UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Role { get; set; }
    public string? BirthDate { get; set; }
    public string? Address { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class UserProfileViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string RoleName { get; set; } = "สมาชิกทั่วไป (Member)";
    public string MemberTier { get; set; } = "Absolute Gold Member";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string ThemePreference { get; set; } = "dark"; // "dark" or "light"

    // ประวัติการจองและการจ่ายเงิน
    public List<UserBookingHistoryItemDto> BookingHistory { get; set; } = new();

    // ประวัติภาพยนตร์ที่ดูแล้ว
    public List<WatchedMovieItemDto> WatchedHistory { get; set; } = new();

    // ตั๋วหนังที่ซื้อไว้และยังไม่ได้ดู (Active Tickets)
    public List<UserTicketDto> ActiveTickets { get; set; } = new();

    public string ActiveTab { get; set; } = "profile"; // "profile", "history", "watched", "settings"
}

public class UserBookingHistoryItemDto
{
    public int BookingId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string TaxInvoiceNo { get; set; } = "";
    public string MovieTitle { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string CinemaName { get; set; } = "";
    public string ShowtimeDateText { get; set; } = "";
    public string ShowtimeTimeText { get; set; } = "";
    public string SeatNumbers { get; set; } = "";
    public int SeatCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "พร้อมเพย์ (PromptPay)";
    public string Status { get; set; } = "Confirmed";
    public DateTime BookedAt { get; set; }
    public bool CanCancel { get; set; }
}

public class WatchedMovieItemDto
{
    public string MovieTitle { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string WatchedDateText { get; set; } = "";
    public string CinemaHall { get; set; } = "";
    public string Seats { get; set; } = "";
    public string Genre { get; set; } = "Action / Sci-Fi";
    public double Rating { get; set; } = 9.2;
    public string ReviewComment { get; set; } = "ภาพยนตร์ยอดเยี่ยม ระบบเสียงและภาพคมชัดสมจริงมาก";
}

public class UserTicketDto
{
    public int BookingId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string TaxInvoiceNo { get; set; } = "";
    public string MovieTitle { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string CinemaName { get; set; } = "";
    public string HallTier { get; set; } = "";
    public string DateDisplayTh { get; set; } = "";
    public string TimeDisplayTh { get; set; } = "";
    public string SeatNumbers { get; set; } = "";
    public int SeatCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "พร้อมเพย์ (PromptPay)";
    public string Status { get; set; } = "Confirmed";
    public string BarcodeCode { get; set; } = "";
    public bool CanCancel { get; set; } = true;
    public DateTime BookedAt { get; set; }
}
