using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Cinema
{
    public int CinemaId { get; set; }

    public string CinemaName { get; set; } = null!;

    public string? Status { get; set; }

    public int TotalSeats { get; set; }

    public virtual ICollection<Operation> Operations { get; set; } = new List<Operation>();

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
