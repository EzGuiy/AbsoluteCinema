using System.Diagnostics;
using System.Text.Json;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kawin_67091861_002.Controllers;

public class HomeController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public HomeController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public IActionResult Index()
    {
        var model = new HomeViewModel();

        // 1. ลองดึงข้อมูลจาก Database ดูก่อน
        bool loadedFromDb = false;
        try
        {
            if (_db.Database.CanConnect())
            {
                var dbMovies = _db.Movies
                    .Include(m => m.Showtimes)
                        .ThenInclude(s => s.Cinema)
                    .Include(m => m.Showtimes)
                        .ThenInclude(s => s.Bookings)
                    .ToList();

                if (dbMovies.Any())
                {
                    // แปลงข้อมูลจาก DB เป็น MovieItemViewModel
                    var movieList = dbMovies.Select(m =>
                    {
                        var todayShowtimes = m.Showtimes
                            .OrderBy(s => s.StartTime)
                            .Select(s => s.StartTime.ToString("HH:mm"))
                            .ToList();

                        if (!todayShowtimes.Any())
                        {
                            todayShowtimes = new List<string> { "11:30", "14:15", "17:00", "19:45" };
                        }

                        var firstShowtime = m.Showtimes.FirstOrDefault();
                        var totalSeats = firstShowtime?.Cinema?.TotalSeats ?? 100;
                        var bookedSeats = firstShowtime?.Bookings?.Count ?? 0;

                        return new MovieItemViewModel
                        {
                            MovieId = m.MovieId,
                            Title = m.Title,
                            Genre = m.Genre ?? "ภาพยนตร์ทั่วไป",
                            Description = m.Description ?? "สัมผัสประสบการณ์การรับชมภาพยนตร์ระดับพรีเมียม",
                            DurationMins = m.DurationMins > 0 ? m.DurationMins : 120,
                            PriceMin = m.PriceMin > 0 ? m.PriceMin : 200,
                            PriceMax = m.PriceMax > 0 ? m.PriceMax : 350,
                            Rating = m.Rating > 0 ? m.Rating : 8.5,
                            AgeRating = "ความรุนแรงระดับ 18+",
                            SpecialTag = "IMAX SPECIAL",
                            PosterUrl = m.PosterUrl,
                            SeatsRemaining = Math.Max(5, totalSeats - bookedSeats),
                            Showtimes = todayShowtimes
                        };
                    }).ToList();

                    model.FeaturedMovie = movieList.FirstOrDefault(m => m.Title.Contains("ตุ๊ดตู่")) ?? movieList.OrderByDescending(m => m.Rating).FirstOrDefault();
                    model.PopularMovies = movieList.Where(m => m.MovieId != model.FeaturedMovie?.MovieId).Take(4).ToList();
                    model.NowShowingMovies = movieList.Skip(1).Take(6).ToList();
                    model.IsFromDatabase = true;
                    loadedFromDb = true;
                }
            }
        }
        catch
        {
            // หาก DB ยังไม่พร้อมใช้งาน ให้ใช้ไฟล์ JSON สำรอง
            loadedFromDb = false;
        }

        // 2. ถ้าใน DB ยังไม่มีข้อมูล หรือยังไม่ได้เปิด MySQL -> ดึงจาก Data/movies.json
        if (!loadedFromDb)
        {
            LoadFromJsonFile(model);
        }

        return View(model);
    }

    private void LoadFromJsonFile(HomeViewModel model)
    {
        try
        {
            var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var jsonText = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                if (root.TryGetProperty("Movies", out var moviesElement))
                {
                    var items = new List<MovieItemViewModel>();
                    foreach (var m in moviesElement.EnumerateArray())
                    {
                        var showtimes = new List<string>();
                        if (m.TryGetProperty("Showtimes", out var stArray))
                        {
                            foreach (var timeSlot in stArray.EnumerateArray())
                            {
                                showtimes.Add(timeSlot.GetString() ?? "");
                            }
                        }

                        items.Add(new MovieItemViewModel
                        {
                            MovieId = m.GetProperty("MovieId").GetInt32(),
                            Title = m.GetProperty("Title").GetString() ?? "",
                            DurationMins = m.GetProperty("DurationMins").GetInt32(),
                            Genre = m.GetProperty("Genre").GetString() ?? "",
                            Description = m.GetProperty("Description").GetString() ?? "",
                            PriceMin = m.GetProperty("PriceMin").GetDecimal(),
                            PriceMax = m.GetProperty("PriceMax").GetDecimal(),
                            Rating = m.GetProperty("Rating").GetDouble(),
                            AgeRating = m.TryGetProperty("AgeRating", out var ar) ? ar.GetString() : "ทั่วไป",
                            SpecialTag = m.TryGetProperty("SpecialTag", out var tag) ? tag.GetString() : null,
                            SeatsRemaining = m.TryGetProperty("SeatsRemaining", out var sr) ? sr.GetInt32() : 25,
                            Showtimes = showtimes
                        });
                    }

                    model.FeaturedMovie = items.FirstOrDefault(i => i.Title.Contains("ตุ๊ดตู่")) ?? items.FirstOrDefault();
                    model.PopularMovies = items.Where(i => i.MovieId >= 2 && i.MovieId <= 5).ToList();
                    model.NowShowingMovies = items.Where(i => i.MovieId >= 6).ToList();
                    model.IsFromDatabase = false;
                    model.DatabaseStatusMessage = "ขณะนี้แสดงข้อมูลแบบไฟล์ JSON สำรอง (สามารถคลิก 'จัดการข้อมูล' ด้านบนเพื่อ Seed ลง MySQL ได้ทันที)";
                }
            }
        }
        catch (Exception ex)
        {
            model.DatabaseStatusMessage = "เกิดข้อผิดพลาดในการโหลดไฟล์ JSON: " + ex.Message;
        }
    }

    public IActionResult NowShowing()
    {
        var movies = new List<MovieItemViewModel>();
        bool loadedFromDb = false;

        try
        {
            if (_db.Database.CanConnect())
            {
                var dbMovies = _db.Movies
                    .Where(m => m.Status == "Showing" && !m.Title.Contains("ตุ๊ดตู่"))
                    .ToList();

                if (dbMovies.Any())
                {
                    movies = dbMovies.Select(m => new MovieItemViewModel
                    {
                        MovieId = m.MovieId,
                        Title = m.Title,
                        Genre = m.Genre ?? "ภาพยนตร์ทั่วไป",
                        DurationMins = m.DurationMins > 0 ? m.DurationMins : 120,
                        PriceMin = m.PriceMin > 0 ? m.PriceMin : 200,
                        PriceMax = m.PriceMax > 0 ? m.PriceMax : 350,
                        Rating = m.Rating > 0 ? m.Rating : 8.0,
                        PosterUrl = m.PosterUrl
                    }).ToList();
                    loadedFromDb = true;
                }
            }
        }
        catch
        {
            loadedFromDb = false;
        }

        if (!loadedFromDb)
        {
            try
            {
                var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
                if (System.IO.File.Exists(jsonPath))
                {
                    var jsonText = System.IO.File.ReadAllText(jsonPath);
                    using var doc = JsonDocument.Parse(jsonText);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Movies", out var moviesElement))
                    {
                        foreach (var m in moviesElement.EnumerateArray())
                        {
                            int id = m.GetProperty("MovieId").GetInt32();
                            // หนัง 8 เรื่องตามหน้า Now Showing (MovieId >= 6)
                            if (id >= 6)
                            {
                                movies.Add(new MovieItemViewModel
                                {
                                    MovieId = id,
                                    Title = m.GetProperty("Title").GetString() ?? "",
                                    DurationMins = m.GetProperty("DurationMins").GetInt32(),
                                    Genre = m.GetProperty("Genre").GetString() ?? "",
                                    PriceMin = m.GetProperty("PriceMin").GetDecimal(),
                                    PriceMax = m.GetProperty("PriceMax").GetDecimal(),
                                    Rating = m.GetProperty("Rating").GetDouble(),
                                    PosterUrl = m.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null
                                });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return View(movies);
    }

    public IActionResult ComingSoon()
    {
        var movies = new List<MovieItemViewModel>();
        try
        {
            var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var jsonText = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;
                if (root.TryGetProperty("ComingSoonMovies", out var csElement))
                {
                    foreach (var m in csElement.EnumerateArray())
                    {
                        movies.Add(new MovieItemViewModel
                        {
                            MovieId = m.GetProperty("MovieId").GetInt32(),
                            Title = m.GetProperty("Title").GetString() ?? "",
                            Genre = m.GetProperty("Genre").GetString() ?? "",
                            ReleaseDateText = m.GetProperty("ReleaseDateText").GetString() ?? "",
                            BadgeText = m.TryGetProperty("BadgeText", out var bt) ? bt.GetString() : null,
                            PosterUrl = m.TryGetProperty("PosterType", out var pt) ? pt.GetString() : null
                        });
                    }
                }
            }
        }
        catch { }

        return View(movies);
    }

    public IActionResult Special()
    {
        var screenings = new List<MovieItemViewModel>();
        try
        {
            var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var jsonText = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;
                if (root.TryGetProperty("SpecialScreenings", out var ssElement))
                {
                    foreach (var s in ssElement.EnumerateArray())
                    {
                        screenings.Add(new MovieItemViewModel
                        {
                            MovieId = s.GetProperty("MovieId").GetInt32(),
                            Title = s.GetProperty("Title").GetString() ?? "",
                            BadgeText = s.GetProperty("BadgeText").GetString() ?? "",
                            ShowtimeText = s.GetProperty("ShowtimeText").GetString() ?? "",
                            PriceText = s.GetProperty("PriceText").GetString() ?? "",
                            SeatsBadgeText = s.GetProperty("SeatsBadgeText").GetString() ?? "",
                            IsLowSeats = s.GetProperty("IsLowSeats").GetBoolean(),
                            PosterUrl = s.TryGetProperty("PosterType", out var pt) ? pt.GetString() : null
                        });
                    }
                }
            }
        }
        catch { }

        return View(screenings);
    }

    public IActionResult Promotions()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // =========================================================================
    // 1. Movie Detail Page (/Home/MovieDetail/{id})
    // =========================================================================
    public IActionResult MovieDetail(int? id, string? date, int? showtimeId)
    {
        int movieId = id ?? 1;
        var vm = new MovieDetailViewModel();

        // 1. ค้นหาข้อมูลภาพยนตร์จาก Database หรือไฟล์ JSON สำรอง
        bool loaded = false;
        try
        {
            if (_db.Database.CanConnect())
            {
                var dbMovie = _db.Movies
                    .Include(m => m.Showtimes)
                        .ThenInclude(s => s.Cinema)
                    .FirstOrDefault(m => m.MovieId == movieId);

                if (dbMovie != null)
                {
                    vm.MovieId = dbMovie.MovieId;
                    vm.Title = dbMovie.Title;
                    vm.DurationMins = dbMovie.DurationMins > 0 ? dbMovie.DurationMins : 135;
                    vm.Genre = dbMovie.Genre ?? "แอ็คชั่น / ไซไฟ";
                    vm.Description = dbMovie.Description ?? "พบกับมหากาพย์ภาพยนตร์ฟอร์มยักษ์ การต่อสู้เพื่อกอบกู้เกียรติยศและชะตากรรมของมนุษยชาติในสมรภูมิที่ยิ่งใหญ่ที่สุด";
                    vm.Rating = dbMovie.Rating > 0 ? dbMovie.Rating : 8.8;
                    vm.PriceMin = dbMovie.PriceMin > 0 ? dbMovie.PriceMin : 1400;
                    vm.PriceMax = dbMovie.PriceMax > 0 ? dbMovie.PriceMax : 3500;
                    vm.PosterUrl = dbMovie.PosterUrl;
                    loaded = true;
                }
            }
        }
        catch { }

        if (!loaded)
        {
            LoadMovieDetailFromJson(movieId, vm);
        }

        // จัดเตรียมข้อมูลวันฉาย 5 วัน เริ่มจากวันนี้
        var today = DateTime.Today;
        var selectedDate = today;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
        {
            selectedDate = parsedDate;
        }

        vm.AvailableDates = new List<DateOptionViewModel>();
        for (int i = 0; i < 5; i++)
        {
            var d = today.AddDays(i);
            string dayTh = GetThaiDayAbbr(d.DayOfWeek);
            string dateTh = $"{d.Day} {GetThaiMonthAbbr(d.Month)}";
            bool isSel = d.Date == selectedDate.Date;

            vm.AvailableDates.Add(new DateOptionViewModel
            {
                Date = d,
                DayNameTh = dayTh,
                DateNumberTh = dateTh,
                FullDisplayTh = $"{dayTh} {dateTh}",
                IsToday = i == 0,
                IsSelected = isSel
            });
        }
        vm.SelectedDateStr = selectedDate.ToString("yyyy-MM-dd");

        // จัดเตรียมโรงภาพยนตร์และรอบฉาย
        vm.CinemaShowtimeGroups = GenerateCinemaShowtimes(movieId, selectedDate);

        // กำหนดรอบฉายที่ถูกเลือกเริ่มต้น
        if (showtimeId.HasValue && vm.CinemaShowtimeGroups.SelectMany(g => g.Slots).Any(s => s.ShowtimeId == showtimeId.Value))
        {
            vm.SelectedShowtimeId = showtimeId.Value;
        }
        else
        {
            var firstSlot = vm.CinemaShowtimeGroups.FirstOrDefault()?.Slots.FirstOrDefault();
            vm.SelectedShowtimeId = firstSlot?.ShowtimeId;
        }

        return View(vm);
    }

    private void LoadMovieDetailFromJson(int movieId, MovieDetailViewModel vm)
    {
        try
        {
            var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var jsonText = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                // ตรวจหาใน Movies, ComingSoon, หรือ SpecialScreenings
                bool found = false;
                if (root.TryGetProperty("Movies", out var mArray))
                {
                    foreach (var m in mArray.EnumerateArray())
                    {
                        if (m.GetProperty("MovieId").GetInt32() == movieId)
                        {
                            vm.MovieId = movieId;
                            vm.Title = m.GetProperty("Title").GetString() ?? "";
                            vm.DurationMins = m.GetProperty("DurationMins").GetInt32();
                            vm.Genre = m.GetProperty("Genre").GetString() ?? "แอ็คชั่น";
                            vm.Description = m.GetProperty("Description").GetString() ?? "";
                            vm.PriceMin = m.GetProperty("PriceMin").GetDecimal();
                            vm.PriceMax = m.GetProperty("PriceMax").GetDecimal();
                            vm.Rating = m.GetProperty("Rating").GetDouble();
                            vm.PosterUrl = m.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found && root.TryGetProperty("SpecialScreenings", out var ssArray))
                {
                    foreach (var s in ssArray.EnumerateArray())
                    {
                        if (s.GetProperty("MovieId").GetInt32() == movieId)
                        {
                            vm.MovieId = movieId;
                            vm.Title = s.GetProperty("Title").GetString() ?? "";
                            vm.DurationMins = 145;
                            vm.Genre = "พรีเมียม สเปเชียล";
                            vm.Description = "รอบฉายพิเศษระดับเอ็กซ์คลูซีฟ พร้อมเทคโนโลยีภาพและเสียงที่ดีที่สุด";
                            vm.Rating = 9.8;
                            vm.PriceMin = 1800;
                            vm.PriceMax = 3500;
                            vm.PosterUrl = s.TryGetProperty("PosterType", out var pt) ? pt.GetString() : null;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    // ค่าเริ่มต้นสำหรับภาพยนตร์ (ตรงตามแบบตัวอย่าง)
                    vm.MovieId = movieId;
                    vm.Title = "(The Emperor of Mankind) The Great Crusades";
                    vm.DurationMins = 135;
                    vm.Genre = "แอ็คชั่น / ไซไฟ / สงคราม";
                    vm.Description = "เมื่อจักรวรรดิของมวลมนุษยชาติต้องเผชิญหน้ากับศัตรูที่ไม่อาจหยั่งถึง สงครามแห่งการกอบกู้เอกราชครั้งสุดท้ายที่จะตัดสินชะตากรรมของกาแล็กซี การรวมพลังของเหล่านักรบผู้กล้าเพื่อความอยู่รอดของเผ่าพันธุ์มนุษย์";
                    vm.Rating = 8.5;
                    vm.Director = "เดอนี วีลเนิฟว์";
                    vm.LeadCast = "มาร์ค วอห์ลเบิร์ก, แอนโธนี ฮอปกินส์, ทิโมธี ชาลาเมต์";
                    vm.PriceMin = 1800;
                    vm.PriceMax = 3500;
                }
            }
        }
        catch
        {
            vm.MovieId = movieId;
            vm.Title = "(The Emperor of Mankind) The Great Crusades";
            vm.DurationMins = 135;
            vm.Genre = "แอ็คชั่น / ไซไฟ";
            vm.Description = "มหากาพย์ภาพยนตร์ฟอร์มยักษ์แห่งศตวรรษ การต่อสู้เพื่อกอบกู้เกียรติยศและอนาคตของมนุษยชาติ";
            vm.Rating = 8.5;
        }
    }

    private List<CinemaShowtimeGroupViewModel> GenerateCinemaShowtimes(int movieId, DateTime date)
    {
        var groups = new List<CinemaShowtimeGroupViewModel>();

        // 1. Ultra Screen (Normal)
        groups.Add(new CinemaShowtimeGroupViewModel
        {
            CinemaId = 1,
            CinemaName = "Ultra Screen (Normal)",
            HallTier = "Normal",
            PricePerSeat = 1800,
            Slots = new List<ShowtimeSlotViewModel>
            {
                new() { ShowtimeId = (movieId * 100) + 1, TimeStr = "18:00", StartTime = date.Date.AddHours(18), AvailableSeats = 72 },
                new() { ShowtimeId = (movieId * 100) + 2, TimeStr = "21:30", StartTime = date.Date.AddHours(21).AddMinutes(30), AvailableSeats = 64 }
            }
        });

        // 2. Gold Class (Premium)
        groups.Add(new CinemaShowtimeGroupViewModel
        {
            CinemaId = 2,
            CinemaName = "Gold Class (Premium)",
            HallTier = "Premium",
            PricePerSeat = 2400,
            Slots = new List<ShowtimeSlotViewModel>
            {
                new() { ShowtimeId = (movieId * 100) + 3, TimeStr = "15:00", StartTime = date.Date.AddHours(15), AvailableSeats = 45, IsSelected = true },
                new() { ShowtimeId = (movieId * 100) + 4, TimeStr = "19:00", StartTime = date.Date.AddHours(19), AvailableSeats = 52 }
            }
        });

        // 3. The Bed Cinema (VIP)
        groups.Add(new CinemaShowtimeGroupViewModel
        {
            CinemaId = 3,
            CinemaName = "The Bed Cinema (VIP)",
            HallTier = "VIP",
            PricePerSeat = 3500,
            Slots = new List<ShowtimeSlotViewModel>
            {
                new() { ShowtimeId = (movieId * 100) + 5, TimeStr = "21:00", StartTime = date.Date.AddHours(21), AvailableSeats = 28 }
            }
        });

        return groups;
    }

    // =========================================================================
    // 2. Seat Selection Page (/Home/SelectSeat)
    // =========================================================================
    public IActionResult SelectSeat(int showtimeId, int? movieId)
    {
        int mId = movieId ?? (showtimeId / 100);
        if (mId <= 0) mId = 1;

        var vm = new SeatSelectionViewModel
        {
            ShowtimeId = showtimeId,
            MovieId = mId
        };

        // โหลดข้อมูลหนัง
        var tempDetail = new MovieDetailViewModel();
        LoadMovieDetailFromJson(mId, tempDetail);
        vm.MovieTitle = tempDetail.Title;
        vm.Genre = tempDetail.Genre;
        vm.DurationMins = tempDetail.DurationMins;
        vm.Rating = tempDetail.Rating;
        vm.PosterUrl = tempDetail.PosterUrl;

        // คำนวณโรงและราคาตาม ShowtimeId
        int slotIndex = showtimeId % 100;
        if (slotIndex == 1 || slotIndex == 2)
        {
            vm.CinemaId = 1;
            vm.CinemaName = "Ultra Screen Theater 1";
            vm.HallTier = "Ultra Screen (Normal)";
            vm.BasePricePerSeat = 1400;
            vm.PremiumPricePerSeat = 1800;
            vm.TimeDisplayTh = slotIndex == 1 ? "18:00 น." : "21:30 น.";
        }
        else if (slotIndex == 5)
        {
            vm.CinemaId = 3;
            vm.CinemaName = "The Bed Cinema VIP 3";
            vm.HallTier = "The Bed Cinema (VIP)";
            vm.BasePricePerSeat = 2800;
            vm.PremiumPricePerSeat = 3500;
            vm.TimeDisplayTh = "21:00 น.";
        }
        else
        {
            vm.CinemaId = 2;
            vm.CinemaName = "Gold Class Theater 2";
            vm.HallTier = "Gold Class (Premium)";
            vm.BasePricePerSeat = 1400;
            vm.PremiumPricePerSeat = 1800;
            vm.TimeDisplayTh = slotIndex == 3 ? "15:00 น." : "19:00 น.";
        }

        var today = DateTime.Today;
        vm.ShowtimeDate = today;
        vm.DateDisplayTh = $"วันนี้ {today.Day} {GetThaiMonthAbbr(today.Month)} {today.Year + 543}";

        // ดึงที่นั่งที่ถูกจองแล้วแบบเรียลไทม์ (จาก DB + JSON storage)
        vm.OccupiedSeats = GetOccupiedSeatsInternal(showtimeId);

        return View(vm);
    }

    // =========================================================================
    // 3. Realtime API: ดึงที่นั่งที่จองแล้ว (/Home/GetOccupiedSeats?showtimeId=...)
    // =========================================================================
    [HttpGet]
    public IActionResult GetOccupiedSeats(int showtimeId)
    {
        var occupied = GetOccupiedSeatsInternal(showtimeId);
        return Json(new
        {
            success = true,
            showtimeId = showtimeId,
            occupiedSeats = occupied,
            totalOccupied = occupied.Count,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    private List<string> GetOccupiedSeatsInternal(int showtimeId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. ดึงจาก MySQL ถ้าเชื่อมต่อได้
        try
        {
            if (_db.Database.CanConnect())
            {
                var dbBookings = _db.Bookings
                    .Where(b => b.ShowtimeId == showtimeId && b.Status != "Cancelled")
                    .Select(b => b.SeatNumbers)
                    .ToList();

                foreach (var seatsStr in dbBookings)
                {
                    if (!string.IsNullOrWhiteSpace(seatsStr))
                    {
                        var seats = seatsStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var s in seats)
                        {
                            result.Add(s.Trim().ToUpper());
                        }
                    }
                }
            }
        }
        catch { }

        // 2. ดึงจากไฟล์สำรอง Data/bookings.json (บันทึกการจองล่าสุด)
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("ShowtimeId", out var stIdProp) && stIdProp.GetInt32() == showtimeId)
                        {
                            if (item.TryGetProperty("Status", out var statusProp) && statusProp.GetString() == "Cancelled")
                            {
                                continue;
                            }
                            if (item.TryGetProperty("SeatNumbers", out var snProp))
                            {
                                var seatsStr = snProp.GetString();
                                if (!string.IsNullOrWhiteSpace(seatsStr))
                                {
                                    var seats = seatsStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var s in seats)
                                    {
                                        result.Add(s.Trim().ToUpper());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // ถ้ายังไม่มีใครจองเลย ให้มีที่นั่งตัวอย่างบางส่วนถูกจองเพื่อความสมจริง (เช่น C5, D6, F10)
        if (!result.Any())
        {
            result.Add("C7");
            result.Add("C8");
            result.Add("D4");
            result.Add("F11");
            result.Add("F12");
        }

        return result.OrderBy(s => s).ToList();
    }

    // =========================================================================
    // 4. Payment Page (/Home/Payment)
    // =========================================================================
    public IActionResult Payment(int showtimeId, string seats, decimal? total, string? promo)
    {
        if (string.IsNullOrWhiteSpace(seats))
        {
            return RedirectToAction("SelectSeat", new { showtimeId = showtimeId });
        }

        int mId = showtimeId / 100;
        if (mId <= 0) mId = 1;

        var seatList = seats.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim().ToUpper())
                            .Distinct()
                            .ToList();

        var tempDetail = new MovieDetailViewModel();
        LoadMovieDetailFromJson(mId, tempDetail);

        decimal pricePerSeat = 1400;
        decimal subtotal = pricePerSeat * seatList.Count;
        decimal discount = 0;
        if (!string.IsNullOrEmpty(promo) && promo.ToUpper() == "CINEMA50")
        {
            discount = subtotal * 0.15m;
        }

        decimal finalTotal = total ?? (subtotal - discount);

        var vm = new PaymentViewModel
        {
            ShowtimeId = showtimeId,
            MovieId = mId,
            MovieTitle = tempDetail.Title,
            PosterUrl = tempDetail.PosterUrl,
            CinemaName = "Gold Class (Cinema 2)",
            HallTier = "Dolby Atmos / 4K Laser",
            DateDisplayTh = $"วันนี้ {DateTime.Today.Day} {GetThaiMonthAbbr(DateTime.Today.Month)} {DateTime.Today.Year + 543}",
            TimeDisplayTh = "15:00 น.",
            SelectedSeats = seatList,
            SelectedSeatsString = string.Join(", ", seatList),
            SeatCount = seatList.Count,
            PricePerSeat = pricePerSeat,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TotalAmount = finalTotal,
            AppliedPromoCode = promo ?? "",
            BookingReferenceCode = "AC-" + DateTime.Now.ToString("yyMMdd") + "-" + new Random().Next(1000, 9999)
        };

        return View(vm);
    }

    // =========================================================================
    // 5. Confirm Booking API: บันทึกลง MySQL Database & สำรองไฟล์ (/Home/ConfirmBooking)
    // =========================================================================
    [HttpPost]
    public IActionResult ConfirmBooking([FromBody] ConfirmBookingDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.SelectedSeats))
        {
            return Json(new { success = false, message = "ข้อมูลการจองไม่ถูกต้อง กรุณาเลือกที่นั่ง" });
        }

        var seatList = dto.SelectedSeats
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToUpper())
            .Distinct()
            .ToList();

        // ตรวจสอบที่นั่งแบบเรียลไทม์ว่ามีใครชิงตัดหน้าจองไปก่อนหรือไม่
        var currentlyOccupied = GetOccupiedSeatsInternal(dto.ShowtimeId);
        var conflictSeats = seatList.Where(s => currentlyOccupied.Contains(s)).ToList();
        if (conflictSeats.Any())
        {
            return Json(new
            {
                success = false,
                message = $"ขออภัย ที่นั่ง {string.Join(", ", conflictSeats)} เพิ่งถูกทำรายการจองไปแล้ว กรุณาเลือกที่นั่งอื่น"
            });
        }

        int bookingId = 0;
        string refCode = "AC-" + DateTime.Now.ToString("yyMMdd") + "-" + new Random().Next(1000, 9999);
        string invoiceNo = "INV-" + DateTime.Now.ToString("yyMMdd") + "-" + new Random().Next(10000, 99999);
        bool savedToDb = false;

        // 1. บันทึกลง MySQL ถ้าเชื่อมต่อได้
        try
        {
            if (_db.Database.CanConnect())
            {
                // ค้นหาหรือใช้ User Id ในระบบ
                int userId = 1;
                var sessionUser = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(sessionUser) && int.TryParse(sessionUser, out int sUid))
                {
                    userId = sUid;
                }
                else
                {
                    var firstUser = _db.Users.FirstOrDefault();
                    if (firstUser != null)
                    {
                        userId = firstUser.UserId;
                    }
                    else
                    {
                        // สร้าง default guest user
                        var guest = new User
                        {
                            Username = "guest_" + new Random().Next(100, 999),
                            FullName = dto.CustomerName ?? "Absolute Cinema Guest",
                            PasswordHash = "demo",
                            RoleId = 2,
                            CreatedAt = DateTime.Now
                        };
                        _db.Users.Add(guest);
                        _db.SaveChanges();
                        userId = guest.UserId;
                    }
                }

                // ตรวจสอบ showtimeId ในตาราง showtimes ถ้ายังไม่มี ให้เชื่อมกับรอบแรกที่มี
                int validShowtimeId = dto.ShowtimeId;
                var existingShowtime = _db.Showtimes.FirstOrDefault(s => s.ShowtimeId == dto.ShowtimeId);
                if (existingShowtime == null)
                {
                    var anyShowtime = _db.Showtimes.FirstOrDefault();
                    if (anyShowtime != null)
                    {
                        validShowtimeId = anyShowtime.ShowtimeId;
                    }
                    else
                    {
                        // สร้าง showtime ชั่วคราวให้ตรงกับระบบ
                        var anyMovie = _db.Movies.FirstOrDefault() ?? new Movie { Title = "ภาพยนตร์", DurationMins = 120 };
                        if (anyMovie.MovieId == 0) { _db.Movies.Add(anyMovie); _db.SaveChanges(); }
                        var anyCinema = _db.Cinemas.FirstOrDefault() ?? new Cinema { CinemaName = "Gold Class", TotalSeats = 100 };
                        if (anyCinema.CinemaId == 0) { _db.Cinemas.Add(anyCinema); _db.SaveChanges(); }

                        var newSt = new Showtime
                        {
                            MovieId = anyMovie.MovieId,
                            CinemaId = anyCinema.CinemaId,
                            StartTime = DateTime.Now.AddHours(2),
                            EndTime = DateTime.Now.AddHours(4)
                        };
                        _db.Showtimes.Add(newSt);
                        _db.SaveChanges();
                        validShowtimeId = newSt.ShowtimeId;
                    }
                }

                var booking = new Booking
                {
                    UserId = userId,
                    ShowtimeId = validShowtimeId,
                    TotalAmount = dto.TotalAmount,
                    Status = "Confirmed",
                    BookedAt = DateTime.Now,
                    SeatNumbers = string.Join(", ", seatList),
                    SeatCount = seatList.Count
                };

                _db.Bookings.Add(booking);
                _db.SaveChanges();
                bookingId = booking.BookingId;

                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = dto.TotalAmount,
                    TaxInvoiceNo = invoiceNo,
                    IsRefunded = false,
                    PaymentDate = DateTime.Now
                };

                _db.Payments.Add(payment);
                _db.SaveChanges();
                savedToDb = true;
            }
        }
        catch (Exception)
        {
            savedToDb = false;
        }

        // 2. สำรองข้อมูลลงไฟล์ Data/bookings.json เสมอ (ทำให้ดูข้อมูลแบบไฟล์ได้และไม่หาย)
        SaveBookingToFile(new
        {
            BookingId = bookingId > 0 ? bookingId : new Random().Next(1000, 9999),
            ReferenceCode = refCode,
            TaxInvoiceNo = invoiceNo,
            ShowtimeId = dto.ShowtimeId,
            MovieId = dto.MovieId,
            SeatNumbers = string.Join(", ", seatList),
            SeatCount = seatList.Count,
            TotalAmount = dto.TotalAmount,
            PaymentMethod = dto.PaymentMethod,
            CustomerName = dto.CustomerName ?? "คุณลูกค้า Absolute Cinema",
            CustomerPhone = dto.CustomerPhone ?? "081-xxx-xxxx",
            Status = "Confirmed",
            BookedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            SavedToDatabase = savedToDb
        });

        // ดึงชื่อหนัง
        var tempDetail = new MovieDetailViewModel();
        LoadMovieDetailFromJson(dto.MovieId, tempDetail);

        return Json(new BookingResponseDto
        {
            Success = true,
            Message = "การชำระเงินสำเร็จ บันทึกการจองและออกตั๋วภาพยนตร์เรียบร้อยแล้ว",
            BookingId = bookingId > 0 ? bookingId : 1001,
            ReferenceCode = refCode,
            TaxInvoiceNo = invoiceNo,
            MovieTitle = tempDetail.Title,
            CinemaName = "Gold Class (Cinema 2)",
            ShowtimeStr = "วันนี้ 15:00 น.",
            Seats = string.Join(", ", seatList),
            TotalAmount = dto.TotalAmount,
            BookedAt = DateTime.Now
        });
    }

    private void SaveBookingToFile(object bookingData)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "bookings.json");

            var list = new List<JsonElement>();
            if (System.IO.File.Exists(filePath))
            {
                var existingJson = System.IO.File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(existingJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        list.Add(el.Clone());
                    }
                }
            }

            var serializedBooking = JsonSerializer.SerializeToElement(bookingData);
            list.Add(serializedBooking);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var output = JsonSerializer.Serialize(list, options);
            System.IO.File.WriteAllText(filePath, output);
        }
        catch { }
    }

    private string GetThaiDayAbbr(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Sunday => "อาทิตย์",
        DayOfWeek.Monday => "จันทร์",
        DayOfWeek.Tuesday => "อังคาร",
        DayOfWeek.Wednesday => "พุธ",
        DayOfWeek.Thursday => "พฤหัส",
        DayOfWeek.Friday => "ศุกร์",
        DayOfWeek.Saturday => "เสาร์",
        _ => ""
    };

    private string GetThaiMonthAbbr(int month) => month switch
    {
        1 => "ม.ค.", 2 => "ก.พ.", 3 => "มี.ค.", 4 => "เม.ย.",
        5 => "พ.ค.", 6 => "มิ.ย.", 7 => "ก.ค.", 8 => "ส.ค.",
        9 => "ก.ย.", 10 => "ต.ค.", 11 => "พ.ย.", 12 => "ธ.ค.",
        _ => ""
    };

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
