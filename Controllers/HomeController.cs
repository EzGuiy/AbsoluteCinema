using System.Diagnostics;
using System.Text.Json;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;
using Kawin_67091861_002.Helpers;
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

                        string? poster = !string.IsNullOrEmpty(m.PosterUrl) ? m.PosterUrl : ResolveLocalOrJsonPoster(m.MovieId, m.Title);

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
                            PosterUrl = poster,
                            SeatsRemaining = Math.Max(5, totalSeats - bookedSeats),
                            Showtimes = todayShowtimes
                        };
                    }).ToList();

                    model.FeaturedMovie = movieList.FirstOrDefault(m => m.Title.Contains("ตุ๊ดตู่") || m.Title.Contains("ซุส")) ?? movieList.OrderByDescending(m => m.Rating).FirstOrDefault();
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

                        if (!showtimes.Any())
                        {
                            showtimes = new List<string> { "11:30", "14:15", "17:00", "19:45" };
                        }

                        int mId = m.GetProperty("MovieId").GetInt32();
                        string mTitle = m.GetProperty("Title").GetString() ?? "";
                        string? poster = m.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null;
                        if (string.IsNullOrEmpty(poster))
                        {
                            poster = ResolveLocalOrJsonPoster(mId, mTitle);
                        }

                        items.Add(new MovieItemViewModel
                        {
                            MovieId = mId,
                            Title = mTitle,
                            DurationMins = m.GetProperty("DurationMins").GetInt32(),
                            Genre = m.GetProperty("Genre").GetString() ?? "",
                            Description = m.GetProperty("Description").GetString() ?? "",
                            PriceMin = m.GetProperty("PriceMin").GetDecimal(),
                            PriceMax = m.GetProperty("PriceMax").GetDecimal(),
                            Rating = m.GetProperty("Rating").GetDouble(),
                            AgeRating = m.TryGetProperty("AgeRating", out var ar) ? ar.GetString() : "ทั่วไป",
                            SpecialTag = m.TryGetProperty("SpecialTag", out var tag) ? tag.GetString() : null,
                            SeatsRemaining = m.TryGetProperty("SeatsRemaining", out var sr) ? sr.GetInt32() : 25,
                            Showtimes = showtimes,
                            PosterUrl = poster
                        });
                    }

                    model.FeaturedMovie = items.FirstOrDefault(i => i.Title.Contains("ตุ๊ดตู่") || i.Title.Contains("ซุส")) ?? items.FirstOrDefault();
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
                        PosterUrl = !string.IsNullOrEmpty(m.PosterUrl) ? m.PosterUrl : ResolveLocalOrJsonPoster(m.MovieId, m.Title)
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
                                string t = m.GetProperty("Title").GetString() ?? "";
                                string? poster = m.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null;
                                if (string.IsNullOrEmpty(poster))
                                {
                                    poster = ResolveLocalOrJsonPoster(id, t);
                                }

                                movies.Add(new MovieItemViewModel
                                {
                                    MovieId = id,
                                    Title = t,
                                    DurationMins = m.GetProperty("DurationMins").GetInt32(),
                                    Genre = m.GetProperty("Genre").GetString() ?? "",
                                    PriceMin = m.GetProperty("PriceMin").GetDecimal(),
                                    PriceMax = m.GetProperty("PriceMax").GetDecimal(),
                                    Rating = m.GetProperty("Rating").GetDouble(),
                                    PosterUrl = poster
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
                    vm.PosterUrl = !string.IsNullOrEmpty(dbMovie.PosterUrl) ? dbMovie.PosterUrl : ResolveLocalOrJsonPoster(dbMovie.MovieId, dbMovie.Title);
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

        // กำหนดรอบฉายที่ถูกเลือกเริ่มต้น โดยเลือกรอบที่ยังไม่ผ่านเวลา
        var allSlots = vm.CinemaShowtimeGroups.SelectMany(g => g.Slots).ToList();
        var validSlots = allSlots.Where(s => s.StartTime >= DateTime.Now).ToList();

        if (showtimeId.HasValue && validSlots.Any(s => s.ShowtimeId == showtimeId.Value))
        {
            vm.SelectedShowtimeId = showtimeId.Value;
        }
        else
        {
            vm.SelectedShowtimeId = validSlots.FirstOrDefault()?.ShowtimeId;
        }

        foreach (var s in allSlots)
        {
            s.IsSelected = (vm.SelectedShowtimeId.HasValue && s.ShowtimeId == vm.SelectedShowtimeId.Value);
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
                            if (string.IsNullOrEmpty(vm.PosterUrl))
                            {
                                vm.PosterUrl = ResolveLocalOrJsonPoster(movieId, vm.Title);
                            }
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

        try
        {
            if (_db.Database.CanConnect())
            {
                var targetDate = date.Date;
                var nextDay = targetDate.AddDays(1);

                var dbShowtimes = _db.Showtimes
                    .Include(s => s.Cinema)
                    .Include(s => s.Bookings)
                    .Where(s => s.MovieId == movieId && s.StartTime >= targetDate && s.StartTime < nextDay)
                    .OrderBy(s => s.StartTime)
                    .ToList();

                if (!dbShowtimes.Any())
                {
                    // Ensure Movie & Cinemas exist in DB, then seed showtimes for this movie and date
                    var dbMovie = _db.Movies.FirstOrDefault(m => m.MovieId == movieId);
                    if (dbMovie == null)
                    {
                        var tempVm = new MovieDetailViewModel();
                        LoadMovieDetailFromJson(movieId, tempVm);
                        dbMovie = new Movie
                        {
                            Title = string.IsNullOrEmpty(tempVm.Title) ? $"ภาพยนตร์ #{movieId}" : tempVm.Title,
                            DurationMins = tempVm.DurationMins > 0 ? tempVm.DurationMins : 120,
                            Genre = tempVm.Genre,
                            Description = tempVm.Description,
                            Rating = tempVm.Rating,
                            PriceMin = tempVm.PriceMin > 0 ? tempVm.PriceMin : 1400,
                            PriceMax = tempVm.PriceMax > 0 ? tempVm.PriceMax : 3500,
                            PosterUrl = tempVm.PosterUrl,
                            Status = "Showing"
                        };
                        _db.Movies.Add(dbMovie);
                        _db.SaveChanges();
                        movieId = dbMovie.MovieId;
                    }

                    var cinemas = _db.Cinemas.OrderBy(c => c.CinemaId).Take(3).ToList();
                    if (!cinemas.Any())
                    {
                        cinemas.Add(new Cinema { CinemaName = "Ultra Screen (Normal)", TotalSeats = 100, Status = "Ready" });
                        cinemas.Add(new Cinema { CinemaName = "Gold Class (Premium)", TotalSeats = 80, Status = "Ready" });
                        cinemas.Add(new Cinema { CinemaName = "The Bed Cinema (VIP)", TotalSeats = 40, Status = "Ready" });
                        _db.Cinemas.AddRange(cinemas);
                        _db.SaveChanges();
                    }

                    int dur = dbMovie.DurationMins > 0 ? dbMovie.DurationMins : 120;
                    foreach (var c in cinemas)
                    {
                        var slotTimes = (c.CinemaId % 3) switch
                        {
                            1 => new[] { (11, 30), (14, 15), (18, 0), (21, 30) },
                            2 => new[] { (12, 0), (15, 0), (19, 0) },
                            _ => new[] { (13, 30), (17, 0), (21, 0) }
                        };
                        foreach (var (h, m) in slotTimes)
                        {
                            var st = targetDate.AddHours(h).AddMinutes(m);
                            _db.Showtimes.Add(new Showtime
                            {
                                MovieId = movieId,
                                CinemaId = c.CinemaId,
                                StartTime = st,
                                EndTime = st.AddMinutes(dur)
                            });
                        }
                    }
                    _db.SaveChanges();

                    dbShowtimes = _db.Showtimes
                        .Include(s => s.Cinema)
                        .Include(s => s.Bookings)
                        .Where(s => s.MovieId == movieId && s.StartTime >= targetDate && s.StartTime < nextDay)
                        .OrderBy(s => s.StartTime)
                        .ToList();
                }

                if (dbShowtimes.Any())
                {
                    var grouped = dbShowtimes.GroupBy(s => s.CinemaId).OrderBy(g => g.Key);
                    foreach (var grp in grouped)
                    {
                        var cinema = grp.First().Cinema;
                        string cName = cinema?.CinemaName ?? $"Cinema {grp.Key}";
                        string tier = cName.Contains("Bed") || cName.Contains("VIP") ? "VIP"
                                    : (cName.Contains("Gold") || cName.Contains("Premium") || cName.Contains("Dolby")) ? "Premium"
                                    : "Normal";
                        decimal price = tier == "VIP" ? 3500 : (tier == "Premium" ? 2400 : 1800);
                        int cap = cinema?.TotalSeats ?? 100;

                        groups.Add(new CinemaShowtimeGroupViewModel
                        {
                            CinemaId = grp.Key,
                            CinemaName = cName,
                            HallTier = tier,
                            PricePerSeat = price,
                            Slots = grp.OrderBy(s => s.StartTime).Select(s =>
                            {
                                int booked = s.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.SeatCount);
                                return new ShowtimeSlotViewModel
                                {
                                    ShowtimeId = s.ShowtimeId,
                                    TimeStr = s.StartTime.ToString("HH:mm"),
                                    StartTime = s.StartTime,
                                    EndTime = s.EndTime == default ? s.StartTime.AddHours(2) : s.EndTime,
                                    AvailableSeats = Math.Max(0, cap - booked)
                                };
                            }).ToList()
                        });
                    }
                    return groups;
                }
            }
        }
        catch { }

        // Fallback when DB unavailable
        groups.Add(new CinemaShowtimeGroupViewModel
        {
            CinemaId = 1,
            CinemaName = "Ultra Screen (Normal)",
            HallTier = "Normal",
            PricePerSeat = 1800,
            Slots = new List<ShowtimeSlotViewModel>
            {
                new() { ShowtimeId = (movieId * 100) + 1, TimeStr = "11:30", StartTime = date.Date.AddHours(11).AddMinutes(30), AvailableSeats = 72 },
                new() { ShowtimeId = (movieId * 100) + 2, TimeStr = "14:15", StartTime = date.Date.AddHours(14).AddMinutes(15), AvailableSeats = 64 },
                new() { ShowtimeId = (movieId * 100) + 3, TimeStr = "18:00", StartTime = date.Date.AddHours(18), AvailableSeats = 72 },
                new() { ShowtimeId = (movieId * 100) + 4, TimeStr = "21:30", StartTime = date.Date.AddHours(21).AddMinutes(30), AvailableSeats = 64 }
            }
        });

        groups.Add(new CinemaShowtimeGroupViewModel
        {
            CinemaId = 2,
            CinemaName = "Gold Class (Premium)",
            HallTier = "Premium",
            PricePerSeat = 2400,
            Slots = new List<ShowtimeSlotViewModel>
            {
                new() { ShowtimeId = (movieId * 100) + 5, TimeStr = "12:00", StartTime = date.Date.AddHours(12), AvailableSeats = 45 },
                new() { ShowtimeId = (movieId * 100) + 6, TimeStr = "15:00", StartTime = date.Date.AddHours(15), AvailableSeats = 45 },
                new() { ShowtimeId = (movieId * 100) + 7, TimeStr = "19:00", StartTime = date.Date.AddHours(19), AvailableSeats = 52 }
            }
        });

        groups.Add(new CinemaShowtimeGroupViewModel
        {
            CinemaId = 3,
            CinemaName = "The Bed Cinema (VIP)",
            HallTier = "VIP",
            PricePerSeat = 3500,
            Slots = new List<ShowtimeSlotViewModel>
            {
                new() { ShowtimeId = (movieId * 100) + 8, TimeStr = "13:30", StartTime = date.Date.AddHours(13).AddMinutes(30), AvailableSeats = 28 },
                new() { ShowtimeId = (movieId * 100) + 9, TimeStr = "17:00", StartTime = date.Date.AddHours(17), AvailableSeats = 28 },
                new() { ShowtimeId = (movieId * 100) + 10, TimeStr = "21:00", StartTime = date.Date.AddHours(21), AvailableSeats = 28 }
            }
        });

        return groups;
    }

    // =========================================================================
    // 2. Seat Selection Page (/Home/SelectSeat)
    // =========================================================================
    public IActionResult SelectSeat(int showtimeId, int? movieId, string? date = null)
    {
        int mId = movieId ?? (showtimeId / 100);
        if (mId <= 0) mId = 1;

        if (HttpContext.Session.GetString("UserLoggedIn") != "true")
        {
            string returnUrl = Url.Action("SelectSeat", "Home", new { showtimeId = showtimeId, movieId = mId, date = date }) ?? "/";
            TempData["AuthNotice"] = "กรุณาเข้าสู่ระบบสมาชิกก่อนดำเนินการเลือกที่นั่งชมภาพยนตร์";
            return RedirectToAction("Login", "Account", new { returnUrl = returnUrl });
        }

        DateTime targetDate = DateTime.Today;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var pd))
        {
            targetDate = pd.Date;
        }

        // Check real Showtime from DB first
        Showtime? dbShowtime = null;
        try
        {
            if (_db.Database.CanConnect())
            {
                dbShowtime = _db.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Cinema)
                    .FirstOrDefault(s => s.ShowtimeId == showtimeId);
            }
        }
        catch { }

        var vm = new SeatSelectionViewModel
        {
            ShowtimeId = showtimeId,
            VipRows = new List<string> { "G", "H" }
        };

        if (dbShowtime != null)
        {
            mId = dbShowtime.MovieId;
            targetDate = dbShowtime.StartTime.Date;
            if (dbShowtime.StartTime < DateTime.Now)
            {
                TempData["AuthNotice"] = "รอบฉายนี้ได้ผ่านเวลาไปแล้ว กรุณาเลือกรอบฉายใหม่";
                return RedirectToAction("MovieDetail", "Home", new { id = mId, date = targetDate.ToString("yyyy-MM-dd") });
            }

            vm.MovieId = mId;
            vm.MovieTitle = dbShowtime.Movie?.Title ?? "ภาพยนตร์";
            vm.Genre = dbShowtime.Movie?.Genre ?? "ทั่วไป";
            vm.DurationMins = dbShowtime.Movie?.DurationMins ?? 120;
            vm.Rating = dbShowtime.Movie?.Rating ?? 8.5;
            vm.PosterUrl = !string.IsNullOrEmpty(dbShowtime.Movie?.PosterUrl) ? dbShowtime.Movie.PosterUrl : ResolveLocalOrJsonPoster(mId, vm.MovieTitle);

            string cName = dbShowtime.Cinema?.CinemaName ?? "Gold Class";
            string tier = cName.Contains("Bed") || cName.Contains("VIP") ? "The Bed Cinema (VIP)"
                        : (cName.Contains("Gold") || cName.Contains("Premium") || cName.Contains("Dolby")) ? "Gold Class (Premium)"
                        : "Ultra Screen (Normal)";
            decimal vipPrice = tier.Contains("VIP") ? 3500 : (tier.Contains("Premium") ? 2400 : 1800);
            decimal basePrice = tier.Contains("VIP") ? 2800 : (tier.Contains("Premium") ? 1800 : 1400);

            vm.CinemaId = dbShowtime.CinemaId;
            vm.CinemaName = cName;
            vm.HallTier = tier;
            vm.BasePricePerSeat = basePrice;
            vm.PremiumPricePerSeat = vipPrice;
            vm.TimeDisplayTh = dbShowtime.StartTime.ToString("HH:mm") + " น.";
        }
        else
        {
            var allGroups = GenerateCinemaShowtimes(mId, targetDate);
            var allSlots = allGroups.SelectMany(g => g.Slots).ToList();
            var targetedSlot = allSlots.FirstOrDefault(s => s.ShowtimeId == showtimeId);
            if (targetedSlot != null && targetDate == DateTime.Today && targetedSlot.StartTime < DateTime.Now)
            {
                TempData["AuthNotice"] = "รอบฉายนี้ได้ผ่านเวลาไปแล้ว กรุณาเลือกรอบฉายใหม่";
                return RedirectToAction("MovieDetail", "Home", new { id = mId, date = targetDate.ToString("yyyy-MM-dd") });
            }

            var targetedGroup = allGroups.FirstOrDefault(g => g.Slots.Any(s => s.ShowtimeId == showtimeId));
            vm.MovieId = mId;
            var tempDetail = new MovieDetailViewModel();
            LoadMovieDetailFromJson(mId, tempDetail);
            vm.MovieTitle = tempDetail.Title;
            vm.Genre = tempDetail.Genre;
            vm.DurationMins = tempDetail.DurationMins;
            vm.Rating = tempDetail.Rating;
            vm.PosterUrl = tempDetail.PosterUrl;

            if (targetedGroup != null)
            {
                vm.CinemaId = targetedGroup.CinemaId;
                vm.CinemaName = targetedGroup.CinemaName;
                vm.HallTier = targetedGroup.HallTier;
                vm.BasePricePerSeat = targetedGroup.PricePerSeat > 400 ? (targetedGroup.PricePerSeat - 400) : 1400;
                vm.PremiumPricePerSeat = targetedGroup.PricePerSeat;
                vm.TimeDisplayTh = targetedSlot != null ? $"{targetedSlot.TimeStr} น." : "15:00 น.";
            }
            else
            {
                vm.CinemaId = 2;
                vm.CinemaName = "Gold Class Theater 2";
                vm.HallTier = "Gold Class (Premium)";
                vm.BasePricePerSeat = 1400;
                vm.PremiumPricePerSeat = 1800;
                vm.TimeDisplayTh = "15:00 น.";
            }
        }

        vm.ShowtimeDate = targetDate;
        vm.SelectedDateStr = targetDate.ToString("yyyy-MM-dd");
        vm.DateDisplayTh = targetDate == DateTime.Today
            ? $"วันนี้ {targetDate.Day} {GetThaiMonthAbbr(targetDate.Month)} {targetDate.Year + 543}"
            : $"{GetThaiDayAbbr(targetDate.DayOfWeek)} {targetDate.Day} {GetThaiMonthAbbr(targetDate.Month)} {targetDate.Year + 543}";

        vm.OccupiedSeats = GetOccupiedSeatsInternal(showtimeId, targetDate);

        return View(vm);
    }

    // =========================================================================
    // 3. Realtime API: ดึงที่นั่งที่จองแล้ว (/Home/GetOccupiedSeats?showtimeId=...)
    // =========================================================================
    [HttpGet]
    public IActionResult GetOccupiedSeats(int showtimeId, string? date = null)
    {
        DateTime? targetDate = DateTime.TryParse(date, out var pd) ? pd.Date : null;
        var occupied = GetOccupiedSeatsInternal(showtimeId, targetDate);
        return Json(new
        {
            success = true,
            showtimeId = showtimeId,
            occupiedSeats = occupied,
            totalOccupied = occupied.Count,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    private List<string> GetOccupiedSeatsInternal(int showtimeId, DateTime? targetDate = null)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. ดึงจาก MySQL
        try
        {
            if (_db.Database.CanConnect())
            {
                var dbBookings = _db.Bookings
                    .Where(b => b.ShowtimeId == showtimeId && b.Status != "Cancelled" && b.Status != "Refunded")
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

        // 2. ดึงจากไฟล์สำรอง Data/bookings.json เฉพาะกรณีที่ยังไม่ได้เชื่อมต่อ DB หรือวันที่ตรงกัน
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
                            if (item.TryGetProperty("Status", out var statusProp) &&
                                (statusProp.GetString() == "Cancelled" || statusProp.GetString() == "Refunded"))
                            {
                                continue;
                            }
                            if (targetDate.HasValue && item.TryGetProperty("ShowtimeDateText", out var sdtProp))
                            {
                                var itemDate = sdtProp.GetString();
                                if (!string.IsNullOrEmpty(itemDate) && itemDate != targetDate.Value.ToString("dd/MM/yyyy"))
                                {
                                    continue;
                                }
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

        return result.OrderBy(s => s).ToList();
    }

    // =========================================================================
    // 4. Payment Page (/Home/Payment)
    // =========================================================================
    public IActionResult Payment(int showtimeId, string seats, decimal? total, string? promo, string? date = null)
    {
        if (string.IsNullOrWhiteSpace(seats))
        {
            return RedirectToAction("SelectSeat", new { showtimeId = showtimeId, date = date });
        }

        int mId = showtimeId / 100;
        if (mId <= 0) mId = 1;

        DateTime targetDate = DateTime.Today;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var pd))
        {
            targetDate = pd.Date;
        }

        var seatList = seats.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim().ToUpper())
                            .Distinct()
                            .ToList();

        string movieTitle = "";
        string? posterUrl = null;
        string cinemaName = "Gold Class (Cinema 2)";
        string hallTier = "Gold Class (Premium)";
        string timeDisplay = "15:00 น.";
        decimal basePrice = 1400;
        decimal vipPrice = 1800;

        Showtime? dbShowtime = null;
        try
        {
            if (_db.Database.CanConnect())
            {
                dbShowtime = _db.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Cinema)
                    .FirstOrDefault(s => s.ShowtimeId == showtimeId);
            }
        }
        catch { }

        if (dbShowtime != null)
        {
            mId = dbShowtime.MovieId;
            targetDate = dbShowtime.StartTime.Date;
            movieTitle = dbShowtime.Movie?.Title ?? "ภาพยนตร์";
            posterUrl = !string.IsNullOrEmpty(dbShowtime.Movie?.PosterUrl) ? dbShowtime.Movie.PosterUrl : ResolveLocalOrJsonPoster(mId, movieTitle);
            cinemaName = dbShowtime.Cinema?.CinemaName ?? "Gold Class";
            hallTier = cinemaName.Contains("Bed") || cinemaName.Contains("VIP") ? "The Bed Cinema (VIP)"
                     : (cinemaName.Contains("Gold") || cinemaName.Contains("Premium") || cinemaName.Contains("Dolby")) ? "Gold Class (Premium)"
                     : "Ultra Screen (Normal)";
            vipPrice = hallTier.Contains("VIP") ? 3500 : (hallTier.Contains("Premium") ? 2400 : 1800);
            basePrice = hallTier.Contains("VIP") ? 2800 : (hallTier.Contains("Premium") ? 1800 : 1400);
            timeDisplay = dbShowtime.StartTime.ToString("HH:mm") + " น.";
        }
        else
        {
            var tempDetail = new MovieDetailViewModel();
            LoadMovieDetailFromJson(mId, tempDetail);
            movieTitle = tempDetail.Title;
            posterUrl = tempDetail.PosterUrl;

            var allGroups = GenerateCinemaShowtimes(mId, targetDate);
            var targetedGroup = allGroups.FirstOrDefault(g => g.Slots.Any(s => s.ShowtimeId == showtimeId));
            var targetedSlot = allGroups.SelectMany(g => g.Slots).FirstOrDefault(s => s.ShowtimeId == showtimeId);
            if (targetedGroup != null)
            {
                cinemaName = targetedGroup.CinemaName;
                hallTier = targetedGroup.HallTier;
                vipPrice = targetedGroup.PricePerSeat;
                basePrice = targetedGroup.PricePerSeat > 400 ? (targetedGroup.PricePerSeat - 400) : 1400;
            }
            if (targetedSlot != null)
            {
                timeDisplay = $"{targetedSlot.TimeStr} น.";
            }
        }

        decimal subtotal = 0;
        foreach (var s in seatList)
        {
            if (s.StartsWith("G") || s.StartsWith("H"))
            {
                subtotal += vipPrice;
            }
            else
            {
                subtotal += basePrice;
            }
        }
        decimal pricePerSeat = seatList.Count > 0 ? Math.Round(subtotal / seatList.Count, 0) : basePrice;
        decimal discount = 0;
        if (!string.IsNullOrEmpty(promo) && (promo.ToUpper() == "CINEMA50" || promo.ToUpper() == "WELCOME" || promo.ToUpper() == "VIP15"))
        {
            discount = subtotal * 0.15m;
        }

        decimal finalTotal = total ?? (subtotal - discount);

        string ppPhone = Kawin_67091861_002.Helpers.PromptPayHelper.TARGET_PHONE;
        string ppPayload = Kawin_67091861_002.Helpers.PromptPayHelper.GeneratePromptPayPayload(ppPhone, finalTotal);
        string ppQrUrl = $"https://promptpay.io/{ppPhone}/{finalTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}.png";

        string dateDisplay = targetDate == DateTime.Today
            ? $"วันนี้ {targetDate.Day} {GetThaiMonthAbbr(targetDate.Month)} {targetDate.Year + 543}"
            : $"{GetThaiDayAbbr(targetDate.DayOfWeek)} {targetDate.Day} {GetThaiMonthAbbr(targetDate.Month)} {targetDate.Year + 543}";

        var vm = new PaymentViewModel
        {
            ShowtimeId = showtimeId,
            MovieId = mId,
            MovieTitle = movieTitle,
            PosterUrl = posterUrl,
            CinemaName = cinemaName,
            HallTier = hallTier,
            SelectedDateStr = targetDate.ToString("yyyy-MM-dd"),
            DateDisplayTh = dateDisplay,
            TimeDisplayTh = timeDisplay,
            SelectedSeats = seatList,
            SelectedSeatsString = string.Join(", ", seatList),
            SeatCount = seatList.Count,
            PricePerSeat = pricePerSeat,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TotalAmount = finalTotal,
            AppliedPromoCode = promo ?? "",
            BookingReferenceCode = "AC-" + DateTime.Now.ToString("yyMMdd") + "-" + new Random().Next(1000, 9999),
            PromptPayPhone = ppPhone,
            PromptPayPhoneFormatted = Kawin_67091861_002.Helpers.PromptPayHelper.TARGET_PHONE_DISPLAY,
            PromptPayPayload = ppPayload,
            PromptPayQrUrl = ppQrUrl
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

        DateTime targetDate = DateTime.Today;
        if (!string.IsNullOrEmpty(dto.ShowtimeDate) && DateTime.TryParse(dto.ShowtimeDate, out var pd))
        {
            targetDate = pd.Date;
        }

        var seatList = dto.SelectedSeats
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToUpper())
            .Distinct()
            .ToList();

        var currentlyOccupied = GetOccupiedSeatsInternal(dto.ShowtimeId, targetDate);
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

        int resolvedUserId = 0;
        var sessionUidStr = HttpContext.Session.GetString("UserId");
        if (!string.IsNullOrEmpty(sessionUidStr) && int.TryParse(sessionUidStr, out int parsedUid))
        {
            resolvedUserId = parsedUid;
        }

        string movieTitle = "";
        string? posterUrl = null;
        string cinemaName = "Gold Class (Cinema 2)";
        string hallTier = "VIP";
        string timeDisplayStr = "15:00 น.";
        DateTime showtimeStartTime = targetDate.AddHours(15);

        // 1. บันทึกลง MySQL
        try
        {
            if (_db.Database.CanConnect())
            {
                if (resolvedUserId <= 0 || !_db.Users.Any(u => u.UserId == resolvedUserId))
                {
                    var sessionEmailKey = HttpContext.Session.GetString("UserEmail") ?? "";
                    var matchedUser = !string.IsNullOrEmpty(sessionEmailKey)
                        ? _db.Users.FirstOrDefault(u => u.Username == sessionEmailKey)
                        : null;
                    if (matchedUser != null)
                    {
                        resolvedUserId = matchedUser.UserId;
                    }
                    else
                    {
                        var memberUser = _db.Users.FirstOrDefault(u => u.Username == "member") ?? _db.Users.FirstOrDefault();
                        resolvedUserId = memberUser?.UserId ?? 1;
                    }
                }

                var dbShowtime = _db.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Cinema)
                    .FirstOrDefault(s => s.ShowtimeId == dto.ShowtimeId);

                int validShowtimeId = dto.ShowtimeId;
                if (dbShowtime != null)
                {
                    movieTitle = dbShowtime.Movie?.Title ?? movieTitle;
                    posterUrl = dbShowtime.Movie?.PosterUrl;
                    cinemaName = dbShowtime.Cinema?.CinemaName ?? cinemaName;
                    hallTier = cinemaName.Contains("VIP") || cinemaName.Contains("Bed") ? "VIP" : "Premium";
                    showtimeStartTime = dbShowtime.StartTime;
                    targetDate = dbShowtime.StartTime.Date;
                    timeDisplayStr = dbShowtime.StartTime.ToString("HH:mm") + " น.";
                }
                else
                {
                    var anyMovie = _db.Movies.FirstOrDefault(m => m.MovieId == dto.MovieId) ?? _db.Movies.FirstOrDefault();
                    if (anyMovie == null)
                    {
                        anyMovie = new Movie { Title = "ภาพยนตร์", DurationMins = 120 };
                        _db.Movies.Add(anyMovie);
                        _db.SaveChanges();
                    }
                    var anyCinema = _db.Cinemas.FirstOrDefault() ?? new Cinema { CinemaName = "Gold Class", TotalSeats = 100 };
                    if (anyCinema.CinemaId == 0) { _db.Cinemas.Add(anyCinema); _db.SaveChanges(); }

                    var newSt = new Showtime
                    {
                        MovieId = anyMovie.MovieId,
                        CinemaId = anyCinema.CinemaId,
                        StartTime = targetDate.AddHours(15),
                        EndTime = targetDate.AddHours(17)
                    };
                    _db.Showtimes.Add(newSt);
                    _db.SaveChanges();
                    validShowtimeId = newSt.ShowtimeId;
                    movieTitle = anyMovie.Title;
                    posterUrl = anyMovie.PosterUrl;
                    cinemaName = anyCinema.CinemaName;
                    showtimeStartTime = newSt.StartTime;
                }

                var booking = new Booking
                {
                    UserId = resolvedUserId,
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
                refCode = $"AC-{bookingId:D6}";

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

        if (string.IsNullOrEmpty(movieTitle))
        {
            var tempDetail = new MovieDetailViewModel();
            LoadMovieDetailFromJson(dto.MovieId, tempDetail);
            movieTitle = tempDetail.Title;
            posterUrl ??= tempDetail.PosterUrl;
        }
        if (string.IsNullOrEmpty(posterUrl))
        {
            posterUrl = ResolveLocalOrJsonPoster(dto.MovieId, movieTitle);
        }

        // ดึงอีเมลและเบอร์โทรศัพท์ของลูกค้า
        string sessionEmail = HttpContext.Session.GetString("UserEmail") ?? "";
        string sessionPhone = HttpContext.Session.GetString("UserPhone") ?? "";
        string resolvedPhone = !string.IsNullOrWhiteSpace(dto.CustomerPhone) ? dto.CustomerPhone : sessionPhone;
        if (string.IsNullOrWhiteSpace(resolvedPhone))
        {
            resolvedPhone = UserProfileHelper.GetPhone(sessionEmail, resolvedUserId);
        }
        if (string.IsNullOrWhiteSpace(resolvedPhone))
        {
            resolvedPhone = "081-xxx-xxxx";
        }

        string resolvedEmail = !string.IsNullOrWhiteSpace(dto.CustomerEmail) ? dto.CustomerEmail : sessionEmail;
        if (string.IsNullOrWhiteSpace(resolvedEmail))
        {
            resolvedEmail = "member@absolutecinema.com";
        }

        string resolvedCustName = !string.IsNullOrWhiteSpace(dto.CustomerName)
            ? dto.CustomerName
            : (HttpContext.Session.GetString("UserName") ?? "คุณลูกค้า Absolute Cinema");

        string dateLabel = targetDate == DateTime.Today
            ? $"วันนี้ {timeDisplayStr}"
            : $"{GetThaiDayAbbr(targetDate.DayOfWeek)} {targetDate.Day} {GetThaiMonthAbbr(targetDate.Month)} {targetDate.Year + 543} | {timeDisplayStr}";

        // 2. สำรองข้อมูลลงไฟล์ Data/bookings.json
        SaveBookingToFile(new
        {
            BookingId = bookingId > 0 ? bookingId : new Random().Next(1000, 9999),
            UserId = resolvedUserId,
            ReferenceCode = refCode,
            TaxInvoiceNo = invoiceNo,
            ShowtimeId = dto.ShowtimeId,
            MovieId = dto.MovieId,
            MovieTitle = movieTitle,
            PosterUrl = posterUrl,
            CinemaName = cinemaName,
            HallTier = hallTier,
            ShowtimeDateText = targetDate.ToString("dd/MM/yyyy"),
            ShowtimeTimeText = timeDisplayStr,
            ShowtimeStartTime = showtimeStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            SeatNumbers = string.Join(", ", seatList),
            SeatCount = seatList.Count,
            TotalAmount = dto.TotalAmount,
            PaymentMethod = dto.PaymentMethod,
            CustomerName = resolvedCustName,
            CustomerPhone = resolvedPhone,
            CustomerEmail = resolvedEmail,
            Status = "Confirmed",
            BookedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            SavedToDatabase = savedToDb
        });

        return Json(new BookingResponseDto
        {
            Success = true,
            Message = "การชำระเงินสำเร็จ บันทึกการจองและออกตั๋วภาพยนตร์เรียบร้อยแล้ว",
            BookingId = bookingId > 0 ? bookingId : 1001,
            ReferenceCode = refCode,
            TaxInvoiceNo = invoiceNo,
            MovieTitle = movieTitle,
            CinemaName = cinemaName,
            ShowtimeStr = dateLabel,
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

    private string? ResolveLocalOrJsonPoster(int movieId, string? title)
    {
        title = title ?? "";

        // 1. ตรวจสอบชื่อเรื่องยอดนิยมหรือรูปที่มีการระบุในเครื่อง
        if (title.Contains("ซุส")) return "/images/zeus.jpg";
        if (title.Contains("โหน่ง") || title.Contains("เท่ง") || title.Contains("หม่ำ")) return "/images/tengmam.jpg";
        if (title.Contains("สายลับ") || title.Contains("บ๊อก")) return "/images/bokbok.jpg";

        // 2. ตรวจสอบตาม MovieId เริ่มต้น
        if (movieId == 1) return "/images/zeus.jpg";
        if (movieId == 2) return "/images/tengmam.jpg";
        if (movieId == 4) return "/images/bokbok.jpg";

        // 3. ตรวจสอบใน Data/movies.json
        try
        {
            var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
            if (System.IO.File.Exists(jsonPath))
            {
                var jsonText = System.IO.File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("Movies", out var mArray))
                {
                    foreach (var m in mArray.EnumerateArray())
                    {
                        int id = m.GetProperty("MovieId").GetInt32();
                        string t = m.GetProperty("Title").GetString() ?? "";
                        if (id == movieId || (!string.IsNullOrEmpty(title) && t.Contains(title)))
                        {
                            if (m.TryGetProperty("PosterUrl", out var pu))
                            {
                                string? url = pu.GetString();
                                if (!string.IsNullOrEmpty(url)) return url;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }
}
