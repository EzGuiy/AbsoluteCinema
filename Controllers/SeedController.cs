using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.Helpers;
using System.Text.Json;

namespace Kawin_67091861_002.Controllers;

/// <summary>
/// Controller สำหรับจัดการข้อมูลใน database ได้ง่ายๆ ผ่าน UI
/// ไม่ต้อง query เอง — กดปุ่ม Seed ข้อมูลตัวอย่างลง DB ได้เลย
/// และสามารถดู/ส่งออกข้อมูลเป็นไฟล์ได้
/// </summary>
public class SeedController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public SeedController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ==========================================
    // หน้าจัดการข้อมูล
    // ==========================================
    public IActionResult Index()
    {
        bool dbConnected = false;
        string? dbError = null;

        int moviesCount = 0;
        int cinemasCount = 0;
        int showtimesCount = 0;
        int usersCount = 0;
        int rolesCount = 0;
        int bookingsCount = 0;

        try
        {
            if (_db.Database.CanConnect())
            {
                dbConnected = true;
                moviesCount = _db.Movies.Count();
                cinemasCount = _db.Cinemas.Count();
                showtimesCount = _db.Showtimes.Count();
                usersCount = _db.Users.Count();
                rolesCount = _db.Roles.Count();
                bookingsCount = _db.Bookings.Count();
            }
            else
            {
                dbError = "ไม่สามารถเชื่อมต่อไปยัง MySQL Server ได้ (ตรวจสอบว่าเปิด Service MySQL เช่น XAMPP แล้วหรือไม่)";
            }
        }
        catch (Exception ex)
        {
            dbConnected = false;
            dbError = "เชื่อมต่อ MySQL ไม่สำเร็จ: " + (ex.InnerException?.Message ?? ex.Message);
        }

        ViewBag.DbConnected = dbConnected;
        ViewBag.DbError = dbError;
        ViewBag.Stats = new
        {
            Movies = moviesCount,
            Cinemas = cinemasCount,
            Showtimes = showtimesCount,
            Users = usersCount,
            Roles = rolesCount,
            Bookings = bookingsCount
        };

        return View();
    }

    // ==========================================
    // Seed ข้อมูลตัวอย่างทั้งหมด (กดปุ่มเดียว)
    // ==========================================
    [HttpPost]
    public IActionResult SeedAll()
    {
        try
        {
            if (!_db.Database.CanConnect())
            {
                TempData["Error"] = "❌ ไม่สามารถเชื่อมต่อ MySQL ได้ กรุณาเปิด MySQL Server ก่อนดำเนินการ";
                return RedirectToAction("Index");
            }

            // ตรวจสอบหรือสร้างคอลัมน์ใหม่ใน MySQL ให้พร้อมอัตโนมัติ
            EnsureColumnsExist();

            SeedRoles();
            SeedCinemas();
            SeedMovies();
            SeedShowtimes();
            ChatHelper.EnsureDatabaseChatReady(_db, _env);

            TempData["Success"] = "✅ Seed ข้อมูลสำเร็จ! เพิ่มข้อมูลภาพยนตร์ โรงภาพยนตร์ และข้อความแชททั้งหมดลง Database แล้ว";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "❌ เกิดข้อผิดพลาด: " + (ex.InnerException?.Message ?? ex.Message);
        }

        return RedirectToAction("Index");
    }

    private void EnsureColumnsExist()
    {
        try
        {
            // ลองรัน ALTER TABLE เพิ่ม columns ในกรณีที่ยังไม่มี
            var conn = _db.Database.GetDbConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();

            var columnsToAdd = new Dictionary<string, string>
            {
                { "Genre", "VARCHAR(100) NULL" },
                { "Description", "TEXT NULL" },
                { "PosterUrl", "VARCHAR(500) NULL" },
                { "TrailerUrl", "VARCHAR(500) NULL" },
                { "PriceMin", "DECIMAL(10,2) NOT NULL DEFAULT 0.00" },
                { "PriceMax", "DECIMAL(10,2) NOT NULL DEFAULT 0.00" },
                { "Rating", "DOUBLE NOT NULL DEFAULT 0" },
                { "ReleaseDate", "DATE NULL" }
            };

            foreach (var col in columnsToAdd)
            {
                try
                {
                    cmd.CommandText = $"ALTER TABLE movies ADD COLUMN {col.Key} {col.Value};";
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    // ถ้ามี column อยู่แล้วจะ throw error ข้ามไป
                }
            }

            try
            {
                cmd.CommandText = "ALTER TABLE cinemas ADD COLUMN TotalSeats INT NOT NULL DEFAULT 100;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            try
            {
                cmd.CommandText = "ALTER TABLE bookings ADD COLUMN SeatNumbers VARCHAR(500) NULL;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            try
            {
                cmd.CommandText = "ALTER TABLE bookings ADD COLUMN SeatCount INT NOT NULL DEFAULT 1;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            try
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS accounting_transactions (
                        TransactionId INT AUTO_INCREMENT PRIMARY KEY,
                        Type VARCHAR(20) NOT NULL DEFAULT 'Income',
                        Category VARCHAR(100) NOT NULL,
                        Description VARCHAR(255) NOT NULL,
                        Amount DECIMAL(12, 2) NOT NULL DEFAULT 0.00,
                        TransactionDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ReferenceNo VARCHAR(50) NOT NULL,
                        Notes TEXT NULL,
                        CreatedBy VARCHAR(100) NOT NULL DEFAULT 'Head Accountant'
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                cmd.ExecuteNonQuery();
            }
            catch { }

            conn.Close();
        }
        catch { }
    }

    // ==========================================
    // ลบข้อมูลทั้งหมด (Reset)
    // ==========================================
    [HttpPost]
    public IActionResult ResetAll()
    {
        try
        {
            if (!_db.Database.CanConnect())
            {
                TempData["Error"] = "❌ ไม่สามารถเชื่อมต่อ MySQL ได้";
                return RedirectToAction("Index");
            }

            // ลบตามลำดับ FK
            _db.Payments.RemoveRange(_db.Payments);
            _db.Bookings.RemoveRange(_db.Bookings);
            _db.Showtimes.RemoveRange(_db.Showtimes);
            _db.Operations.RemoveRange(_db.Operations);
            _db.Chats.RemoveRange(_db.Chats);
            _db.Movies.RemoveRange(_db.Movies);
            _db.Cinemas.RemoveRange(_db.Cinemas);
            _db.SaveChanges();

            TempData["Success"] = "🗑️ ลบข้อมูลทั้งหมดสำเร็จ (ยกเว้น Users & Roles)";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "❌ เกิดข้อผิดพลาด: " + (ex.InnerException?.Message ?? ex.Message);
        }

        return RedirectToAction("Index");
    }

    // ==========================================
    // ดู Database ทั้งหมดเป็นตาราง
    // ==========================================
    public IActionResult ViewDatabase()
    {
        try
        {
            if (_db.Database.CanConnect())
            {
                var data = new
                {
                    Source = "MySQL Database (Active)",
                    Movies = _db.Movies.OrderBy(m => m.MovieId).Select(m => new
                    {
                        m.MovieId,
                        m.Title,
                        m.Genre,
                        m.DurationMins,
                        m.Status,
                        m.Description,
                        m.PriceMin,
                        m.PriceMax,
                        m.Rating,
                        m.ReleaseDate,
                        m.PosterUrl
                    }).ToList(),

                    Cinemas = _db.Cinemas.OrderBy(c => c.CinemaId).Select(c => new
                    {
                        c.CinemaId,
                        c.CinemaName,
                        c.Status,
                        c.TotalSeats
                    }).ToList(),

                    Showtimes = _db.Showtimes
                        .Include(s => s.Movie)
                        .Include(s => s.Cinema)
                        .OrderBy(s => s.StartTime)
                        .Select(s => new
                        {
                            s.ShowtimeId,
                            Movie = s.Movie.Title,
                            Cinema = s.Cinema.CinemaName,
                            Start = s.StartTime.ToString("yyyy-MM-dd HH:mm"),
                            End = s.EndTime.ToString("yyyy-MM-dd HH:mm")
                        }).ToList(),

                    Users = _db.Users
                        .Include(u => u.Role)
                        .OrderBy(u => u.UserId)
                        .Select(u => new
                        {
                            u.UserId,
                            u.Username,
                            u.FullName,
                            Role = u.Role.RoleName,
                            u.CreatedAt
                        }).ToList(),

                    Roles = _db.Roles.OrderBy(r => r.RoleId).Select(r => new
                    {
                        r.RoleId,
                        r.RoleName
                    }).ToList(),

                    Bookings = _db.Bookings
                        .Include(b => b.User)
                        .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                        .Include(b => b.Showtime).ThenInclude(s => s.Cinema)
                        .OrderByDescending(b => b.BookedAt)
                        .Select(b => new
                        {
                            b.BookingId,
                            User = b.User.FullName,
                            Movie = b.Showtime.Movie.Title,
                            Cinema = b.Showtime.Cinema.CinemaName,
                            Seats = b.SeatNumbers ?? "-",
                            Count = b.SeatCount,
                            Amount = b.TotalAmount ?? 0,
                            b.Status,
                            Date = b.BookedAt.HasValue ? b.BookedAt.Value.ToString("yyyy-MM-dd HH:mm") : "-"
                        }).ToList(),

                    Payments = _db.Payments
                        .Include(p => p.Booking)
                        .OrderByDescending(p => p.PaymentDate)
                        .Select(p => new
                        {
                            p.PaymentId,
                            p.BookingId,
                            p.TaxInvoiceNo,
                            p.Amount,
                            Date = p.PaymentDate.HasValue ? p.PaymentDate.Value.ToString("yyyy-MM-dd HH:mm") : "-"
                        }).ToList(),

                    Chats = _db.Chats
                        .Include(c => c.Sender).ThenInclude(u => u.Role)
                        .Include(c => c.TargetRoleNavigation)
                        .OrderByDescending(c => c.SentAt)
                        .Select(c => new
                        {
                            c.ChatId,
                            Sender = c.Sender.FullName ?? c.Sender.Username,
                            SenderRole = c.Sender.Role != null ? c.Sender.Role.RoleName : "-",
                            TargetRole = c.TargetRoleNavigation != null ? c.TargetRoleNavigation.RoleName : "General",
                            c.Message,
                            Date = c.SentAt.HasValue ? c.SentAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-"
                        }).ToList()
                };

                return View(data);
            }
        }
        catch { }

        // ถ้าเชื่อมต่อ DB ไม่ได้ ให้แสดงจากไฟล์ JSON แทน
        return ViewFromFile();
    }

    private IActionResult ViewFromFile()
    {
        var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
        if (System.IO.File.Exists(jsonPath))
        {
            var jsonText = System.IO.File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(jsonText);

            var movies = new List<dynamic>();
            if (doc.RootElement.TryGetProperty("Movies", out var mArray))
            {
                foreach (var item in mArray.EnumerateArray())
                {
                    string? pUrl = item.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null;
                    string itemTitle = item.GetProperty("Title").GetString() ?? "";
                    if (string.IsNullOrEmpty(pUrl))
                    {
                        if (itemTitle.Contains("ซุส") || itemTitle.Contains("ตุ๊ดตู่")) pUrl = "/images/zeus.jpg";
                        else if (itemTitle.Contains("โหน่ง") || itemTitle.Contains("เท่ง") || itemTitle.Contains("หม่ำ")) pUrl = "/images/tengmam.jpg";
                        else if (itemTitle.Contains("สายลับ") || itemTitle.Contains("บ๊อก")) pUrl = "/images/bokbok.jpg";
                    }

                    movies.Add(new
                    {
                        MovieId = item.GetProperty("MovieId").GetInt32(),
                        Title = itemTitle,
                        Genre = item.GetProperty("Genre").GetString() ?? "",
                        DurationMins = item.GetProperty("DurationMins").GetInt32(),
                        Status = item.GetProperty("Status").GetString() ?? "",
                        Description = item.GetProperty("Description").GetString() ?? "",
                        PriceMin = item.GetProperty("PriceMin").GetDecimal(),
                        PriceMax = item.GetProperty("PriceMax").GetDecimal(),
                        Rating = item.GetProperty("Rating").GetDouble(),
                        ReleaseDate = (DateTime?)DateTime.Parse(item.GetProperty("ReleaseDate").GetString() ?? "2026-10-15"),
                        PosterUrl = pUrl
                    });
                }
            }

            var cinemas = new List<dynamic>();
            if (doc.RootElement.TryGetProperty("Cinemas", out var cArray))
            {
                foreach (var item in cArray.EnumerateArray())
                {
                    cinemas.Add(new
                    {
                        CinemaId = item.GetProperty("CinemaId").GetInt32(),
                        CinemaName = item.GetProperty("CinemaName").GetString() ?? "",
                        Status = item.GetProperty("Status").GetString() ?? "",
                        TotalSeats = item.GetProperty("TotalSeats").GetInt32()
                    });
                }
            }

            var bookingsList = new List<dynamic>();
            var paymentsList = new List<dynamic>();
            var bookingsFile = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
            if (System.IO.File.Exists(bookingsFile))
            {
                try
                {
                    var bText = System.IO.File.ReadAllText(bookingsFile);
                    using var bDoc = JsonDocument.Parse(bText);
                    if (bDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        int pId = 1;
                        foreach (var b in bDoc.RootElement.EnumerateArray())
                        {
                            int bId = b.TryGetProperty("BookingId", out var bid) ? bid.GetInt32() : 1000;
                            string cName = b.TryGetProperty("CustomerName", out var cn) ? (cn.GetString() ?? "-") : "-";
                            string seats = b.TryGetProperty("SeatNumbers", out var sn) ? (sn.GetString() ?? "-") : "-";
                            int sCount = b.TryGetProperty("SeatCount", out var sc) ? sc.GetInt32() : 1;
                            decimal amt = b.TryGetProperty("TotalAmount", out var ta) ? ta.GetDecimal() : 0;
                            string status = b.TryGetProperty("Status", out var st) ? (st.GetString() ?? "Confirmed") : "Confirmed";
                            string bAt = b.TryGetProperty("BookedAt", out var ba) ? (ba.GetString() ?? "-") : "-";
                            string inv = b.TryGetProperty("TaxInvoiceNo", out var iv) ? (iv.GetString() ?? "-") : "-";

                            bookingsList.Add(new
                            {
                                BookingId = bId,
                                User = cName,
                                Movie = "ภาพยนตร์",
                                Cinema = "Gold Class",
                                Seats = seats,
                                Count = sCount,
                                Amount = amt,
                                Status = status,
                                Date = bAt
                            });

                            paymentsList.Add(new
                            {
                                PaymentId = pId++,
                                BookingId = bId,
                                TaxInvoiceNo = inv,
                                Amount = amt,
                                Date = bAt
                            });
                        }
                    }
                }
                catch { }
            }

            var fallbackData = new
            {
                Source = "ไฟล์ข้อมูลสำรอง (Data/movies.json & Data/bookings.json)",
                Movies = movies,
                Cinemas = cinemas,
                Showtimes = new List<dynamic>(),
                Users = new List<dynamic>(),
                Roles = new List<dynamic>(),
                Bookings = bookingsList,
                Payments = paymentsList,
                Chats = ChatHelper.ReadMessagesFromJson(_env).Select(m => new
                {
                    ChatId = m.Id,
                    Sender = m.SenderName,
                    SenderRole = "-",
                    TargetRole = m.ChannelId,
                    Message = m.MessageText,
                    Date = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList()
            };

            return View(fallbackData);
        }

        return View(new
        {
            Source = "ไม่มีข้อมูล",
            Movies = new List<dynamic>(),
            Cinemas = new List<dynamic>(),
            Showtimes = new List<dynamic>(),
            Users = new List<dynamic>(),
            Roles = new List<dynamic>(),
            Bookings = new List<dynamic>(),
            Payments = new List<dynamic>(),
            Chats = new List<dynamic>()
        });
    }

    // ==========================================
    // Export เป็น JSON file ดาวน์โหลดได้
    // ==========================================
    public IActionResult ExportJson()
    {
        try
        {
            if (_db.Database.CanConnect())
            {
                var data = new
                {
                    ExportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Source = "Absolute Cinema MySQL Database",
                    Movies = _db.Movies.OrderBy(m => m.MovieId).ToList(),
                    Cinemas = _db.Cinemas.OrderBy(c => c.CinemaId).ToList(),
                    Showtimes = _db.Showtimes
                        .Include(s => s.Movie)
                        .Include(s => s.Cinema)
                        .OrderBy(s => s.StartTime)
                        .Select(s => new
                        {
                            s.ShowtimeId,
                            s.MovieId,
                            MovieTitle = s.Movie.Title,
                            s.CinemaId,
                            CinemaName = s.Cinema.CinemaName,
                            s.StartTime,
                            s.EndTime
                        }).ToList(),
                    Users = _db.Users.Include(u => u.Role)
                        .OrderBy(u => u.UserId)
                        .Select(u => new
                        {
                            u.UserId,
                            u.Username,
                            u.FullName,
                            Role = u.Role.RoleName,
                            u.CreatedAt
                        }).ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                return File(
                    System.Text.Encoding.UTF8.GetBytes(json),
                    "application/json",
                    $"absolute_cinema_db_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                );
            }
        }
        catch { }

        // ถ้าต่อ DB ไม่ได้ ส่งไฟล์ Data/movies.json ให้ดาวน์โหลด
        var fallbackPath = Path.Combine(_env.ContentRootPath, "Data", "movies.json");
        if (System.IO.File.Exists(fallbackPath))
        {
            var bytes = System.IO.File.ReadAllBytes(fallbackPath);
            return File(bytes, "application/json", "movies_sample_data.json");
        }

        return NotFound();
    }

    // ==========================================
    // Private helper methods
    // ==========================================

    private void SeedRoles()
    {
        var existingRoles = _db.Roles.Select(r => r.RoleName).ToList();
        var rolesToAdd = new[] { "Admin", "User", "Accountant", "Technician", "Manager", "Staff" };

        foreach (var r in rolesToAdd)
        {
            if (!existingRoles.Contains(r))
            {
                _db.Roles.Add(new Role { RoleName = r });
            }
        }
        _db.SaveChanges();
    }

    private void SeedCinemas()
    {
        if (!_db.Cinemas.Any())
        {
            _db.Cinemas.AddRange(
                new Cinema { CinemaName = "IMAX Hall 1", Status = "Ready", TotalSeats = 200 },
                new Cinema { CinemaName = "Dolby Atmos Hall 2", Status = "Ready", TotalSeats = 180 },
                new Cinema { CinemaName = "Standard Hall 3", Status = "Ready", TotalSeats = 150 },
                new Cinema { CinemaName = "VIP Hall 4", Status = "Ready", TotalSeats = 80 }
            );
            _db.SaveChanges();
        }
    }

    private void SeedMovies()
    {
        if (!_db.Movies.Any())
        {
            _db.Movies.AddRange(
                new Movie
                {
                    Title = "ตุ๊ดตู่กู้ชาติ: ยุทธการตุ๊ดครองเมือง",
                    DurationMins = 135,
                    Status = "Showing",
                    Genre = "แอ็คชั่น / ตลก",
                    Description = "พบกับมหากาพย์ภาพยนตร์แอ็คชั่นฟอร์มยักษ์แห่งปี การต่อสู้เพื่อกอบกู้เอกราชครั้งสุดท้ายที่จะตัดสินชะตากรรมของคนทั้งชาติ ชมระบบเสียง Dolby Atmos ดีที่สุดวันนี้",
                    PriceMin = 240, PriceMax = 380,
                    Rating = 9.5, ReleaseDate = new DateTime(2026, 10, 15),
                    PosterUrl = "/images/zeus.jpg"
                },
                new Movie
                {
                    Title = "โหน่งเท่ง",
                    DurationMins = 110,
                    Status = "Showing",
                    Genre = "ตลกฮาหน้า",
                    Description = "เรื่องราวความฮาสุดป่วนที่จะทำให้คุณหัวเราะจนกรามค้าง การรวมตัวของสุดยอดดาวตลก",
                    PriceMin = 200, PriceMax = 320,
                    Rating = 10.0, ReleaseDate = new DateTime(2026, 10, 20),
                    PosterUrl = "/images/tengmam.jpg"
                },
                new Movie
                {
                    Title = "มหัศจรรย์สิงห์",
                    DurationMins = 130,
                    Status = "Showing",
                    Genre = "แอ็คชั่น / ผจญภัย",
                    Description = "การผจญภัยอันยิ่งใหญ่ในดินแดนลี้ลับ พลังแห่งสิงห์ผู้พิทักษ์ที่ตื่นขึ้นมาเพื่อปกป้องโลก",
                    PriceMin = 220, PriceMax = 350,
                    Rating = 8.0, ReleaseDate = new DateTime(2026, 10, 25)
                },
                new Movie
                {
                    Title = "สายลับ ดิบ ตาย",
                    DurationMins = 125,
                    Status = "Showing",
                    Genre = "ระทึกขวัญ / ดราม่า",
                    Description = "ภารกิจสายลับสุดระห่ำ ความลับระดับชาติที่ไม่อาจเปิดเผย และการหักหลังที่ไม่มีใครคาดคิด",
                    PriceMin = 240, PriceMax = 360,
                    Rating = 10.0, ReleaseDate = new DateTime(2026, 11, 1),
                    PosterUrl = "/images/bokbok.jpg"
                },
                new Movie
                {
                    Title = "บทเพลงสุดโหด",
                    DurationMins = 118,
                    Status = "Showing",
                    Genre = "ดราม่า / ดนตรี",
                    Description = "เรื่องราวของนักเปียโนผู้ทุ่มเทชีวิตเพื่อสร้างบทเพลงที่สมบูรณ์แบบ ท่ามกลางเปลวไฟและความเจ็บปวด",
                    PriceMin = 200, PriceMax = 340,
                    Rating = 8.0, ReleaseDate = new DateTime(2026, 10, 28)
                },
                new Movie
                {
                    Title = "สมรภูมิผู้ชีพ",
                    DurationMins = 140,
                    Status = "Showing",
                    Genre = "แอ็คชั่น / สงคราม",
                    Description = "มหากาพย์สงครามที่ยิ่งใหญ่ การเสียสละของเหล่าทหารกล้าในสมรภูมิสุดท้าย",
                    PriceMin = 240, PriceMax = 380,
                    Rating = 8.8, ReleaseDate = new DateTime(2026, 10, 18)
                },
                new Movie
                {
                    Title = "ดั่งวาสนา",
                    DurationMins = 115,
                    Status = "Showing",
                    Genre = "โรแมนติก / ดราม่า",
                    Description = "พรหมลิขิตที่นำพาสองหัวใจมาพบกัน ทว่าเส้นทางแห่งความรักไม่ได้โรยด้วยกลีบกุหลาบ",
                    PriceMin = 180, PriceMax = 320,
                    Rating = 8.2, ReleaseDate = new DateTime(2026, 11, 2)
                },
                new Movie
                {
                    Title = "เขาหลอนโรงเรียนเก่า",
                    DurationMins = 108,
                    Status = "Showing",
                    Genre = "สยองขวัญ",
                    Description = "ความลับสุดสะพรึงในรั้วโรงเรียนเก่าที่ถูกปิดตาย วิญญาณที่ยังคงวนเวียนรอคอยการกลับมา",
                    PriceMin = 200, PriceMax = 350,
                    Rating = 7.9, ReleaseDate = new DateTime(2026, 10, 31)
                },
                new Movie
                {
                    Title = "มหานครไซไฟ 2099",
                    DurationMins = 150,
                    Status = "Showing",
                    Genre = "ไซไฟ / แอ็คชั่น",
                    Description = "ในปี 2099 มหานครแห่งเทคโนโลยีล้ำยุค เมื่อปัญญาประดิษฐ์เริ่มควบคุมมนุษยชาติ",
                    PriceMin = 250, PriceMax = 400,
                    Rating = 9.0, ReleaseDate = new DateTime(2026, 11, 5)
                }
            );
            _db.SaveChanges();
        }
    }

    private void SeedShowtimes()
    {
        if (!_db.Showtimes.Any())
        {
            var movies = _db.Movies.Where(m => m.Status == "Showing").ToList();
            var cinemas = _db.Cinemas.ToList();

            if (!movies.Any() || !cinemas.Any()) return;

            var today = DateTime.Today;
            var showtimes = new List<Showtime>();

            for (int day = 0; day < 3; day++)
            {
                var date = today.AddDays(day);
                var timeSlots = new[] { 11, 13, 15, 17, 19, 21 };

                foreach (var movie in movies)
                {
                    var cinemaIndex = 0;
                    var numShows = Math.Min(3 + (movie.MovieId % 3), timeSlots.Length);

                    for (int i = 0; i < numShows; i++)
                    {
                        var cinema = cinemas[cinemaIndex % cinemas.Count];
                        var startHour = timeSlots[i % timeSlots.Length];
                        var startMinute = (movie.MovieId * 10 + i * 15) % 60;
                        var startTime = date.AddHours(startHour).AddMinutes(startMinute);
                        var endTime = startTime.AddMinutes(movie.DurationMins);

                        showtimes.Add(new Showtime
                        {
                            MovieId = movie.MovieId,
                            CinemaId = cinema.CinemaId,
                            StartTime = startTime,
                            EndTime = endTime
                        });

                        cinemaIndex++;
                    }
                }
            }

            _db.Showtimes.AddRange(showtimes);
            _db.SaveChanges();
        }
    }
}
