using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.ViewModels;

// ==========================================
// 1. Movie Detail Page ViewModel
// ==========================================
public class MovieDetailViewModel
{
    public int MovieId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMins { get; set; }
    public string Genre { get; set; } = "";
    public string Description { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public double Rating { get; set; } = 8.5;
    public string AgeRating { get; set; } = "เรท 18+";
    public string FormatTag { get; set; } = "Dolby Cinema";
    public string Director { get; set; } = "คริสโตเฟอร์ โนแลน";
    public string LeadCast { get; set; } = "มาร์ค วอห์ลเบิร์ก, ทอม แฮงส์, คิลเลียน เมอร์ฟี";
    public decimal PriceMin { get; set; } = 240;
    public decimal PriceMax { get; set; } = 380;

    public List<DateOptionViewModel> AvailableDates { get; set; } = new();
    public List<CinemaShowtimeGroupViewModel> CinemaShowtimeGroups { get; set; } = new();

    public string SelectedDateStr { get; set; } = "";
    public int? SelectedShowtimeId { get; set; }
}

public class DateOptionViewModel
{
    public DateTime Date { get; set; }
    public string DayNameTh { get; set; } = "";     // พุธ, พฤหัส, ศุกร์
    public string DateNumberTh { get; set; } = "";  // 15 พ.ย.
    public string FullDisplayTh { get; set; } = ""; // พุธ 15 พ.ย.
    public bool IsSelected { get; set; }
    public bool IsToday { get; set; }
}

public class CinemaShowtimeGroupViewModel
{
    public int CinemaId { get; set; }
    public string CinemaName { get; set; } = "";          // Ultra Screen, Gold Class, The Bed Cinema
    public string HallTier { get; set; } = "";            // Normal, Premium, VIP
    public decimal PricePerSeat { get; set; }             // 1800, 2400, 3500
    public List<ShowtimeSlotViewModel> Slots { get; set; } = new();
}

public class ShowtimeSlotViewModel
{
    public int ShowtimeId { get; set; }
    public string TimeStr { get; set; } = "";            // 15:00
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int AvailableSeats { get; set; } = 80;
    public bool IsSelected { get; set; }
}

// ==========================================
// 2. Seat Selection Page ViewModel
// ==========================================
public class SeatSelectionViewModel
{
    public int ShowtimeId { get; set; }
    public int MovieId { get; set; }
    public string MovieTitle { get; set; } = "";
    public string Genre { get; set; } = "";
    public int DurationMins { get; set; }
    public string? PosterUrl { get; set; }
    public double Rating { get; set; }

    public int CinemaId { get; set; }
    public string CinemaName { get; set; } = "";
    public string HallTier { get; set; } = "";

    public DateTime ShowtimeDate { get; set; }
    public string DateDisplayTh { get; set; } = "";       // วันนี้ 15 พ.ย. 2026
    public string TimeDisplayTh { get; set; } = "";       // 15:00 น.

    public decimal BasePricePerSeat { get; set; } = 1400; // 1400 บาท/ที่นั่ง
    public decimal PremiumPricePerSeat { get; set; } = 1800; // 1800 บาท/ที่นั่ง (แถว VIP A-B)

    public List<string> Rows { get; set; } = new() { "A", "B", "C", "D", "E", "F", "G", "H" };
    public int ColumnsCount { get; set; } = 14;

    public List<string> OccupiedSeats { get; set; } = new();
    public List<string> VipRows { get; set; } = new() { "A", "B" };
}

// ==========================================
// 3. Payment Page ViewModel
// ==========================================
public class PaymentViewModel
{
    public int ShowtimeId { get; set; }
    public int MovieId { get; set; }
    public string MovieTitle { get; set; } = "";
    public string? PosterUrl { get; set; }
    public string CinemaName { get; set; } = "";
    public string HallTier { get; set; } = "";
    public string DateDisplayTh { get; set; } = "";
    public string TimeDisplayTh { get; set; } = "";

    public List<string> SelectedSeats { get; set; } = new();
    public string SelectedSeatsString { get; set; } = "";
    public int SeatCount { get; set; } = 1;

    public decimal PricePerSeat { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; }
    public string AppliedPromoCode { get; set; } = "";
    public string BookingReferenceCode { get; set; } = "";
}

// ==========================================
// 4. API DTOs for Booking & Realtime Availability
// ==========================================
public class ConfirmBookingDto
{
    public int ShowtimeId { get; set; }
    public int MovieId { get; set; }
    public string SelectedSeats { get; set; } = ""; // e.g. "E7, E8"
    public decimal TotalAmount { get; set; }
    public string? PromoCode { get; set; }
    public string PaymentMethod { get; set; } = "PromptPay"; // PromptPay, CreditCard, BankTransfer
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
}

public class BookingResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int? BookingId { get; set; }
    public string ReferenceCode { get; set; } = "";
    public string TaxInvoiceNo { get; set; } = "";
    public string MovieTitle { get; set; } = "";
    public string CinemaName { get; set; } = "";
    public string ShowtimeStr { get; set; } = "";
    public string Seats { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime BookedAt { get; set; }
}

