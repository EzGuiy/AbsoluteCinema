using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Movie
{
    public int MovieId { get; set; }

    public string Title { get; set; } = null!;

    public int DurationMins { get; set; }

    public string? Status { get; set; }

    public string? Genre { get; set; }

    public string? Description { get; set; }

    public string? PosterUrl { get; set; }

    public string? TrailerUrl { get; set; }

    public decimal PriceMin { get; set; }

    public decimal PriceMax { get; set; }

    public double Rating { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
