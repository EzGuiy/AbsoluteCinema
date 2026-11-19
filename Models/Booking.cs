using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Booking
{
    public int BookingId { get; set; }

    public int UserId { get; set; }

    public int ShowtimeId { get; set; }

    public decimal? TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? BookedAt { get; set; }

    public string? SeatNumbers { get; set; }

    public int SeatCount { get; set; } = 1;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Showtime Showtime { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
