using Kawin_67091861_002.Models;

namespace Kawin_67091861_002.ViewModels
{
    public class HomeViewModel
    {
        // หนังเด่น Hero Banner
        public MovieItemViewModel? FeaturedMovie { get; set; }

        // หนังยอดนิยม 4 เรื่อง
        public List<MovieItemViewModel> PopularMovies { get; set; } = new();

        // หนังที่กำลังฉายอยู่ตอนนี้
        public List<MovieItemViewModel> NowShowingMovies { get; set; } = new();

        // แหล่งที่มาของข้อมูล (มาจาก DB หรือจากไฟล์ JSON สำรอง)
        public bool IsFromDatabase { get; set; }
        public string? DatabaseStatusMessage { get; set; }
    }

    public class MovieItemViewModel
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Description { get; set; } = "";
        public int DurationMins { get; set; }
        public decimal PriceMin { get; set; }
        public decimal PriceMax { get; set; }
        public double Rating { get; set; }
        public string? AgeRating { get; set; }
        public string? SpecialTag { get; set; }
        public string? PosterUrl { get; set; }
        public int SeatsRemaining { get; set; } = 30;
        public List<string> Showtimes { get; set; } = new();

        // ฟิลด์เพิ่มเติมสำหรับหน้าโปรแกรมหน้าและรอบพิเศษ
        public string? ReleaseDateText { get; set; }
        public string? ShowtimeText { get; set; }
        public string? PriceText { get; set; }
        public string? BadgeText { get; set; }
        public string? SeatsBadgeText { get; set; }
        public bool IsLowSeats { get; set; }
    }
}
