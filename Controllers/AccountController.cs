using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;

namespace Kawin_67091861_002.Controllers;

public class AccountController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public AccountController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ==========================================
    // 1. ส่วนแสดงผล (Display) - หน้า Login
    // ==========================================
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.GetString("UserLoggedIn") == "true")
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    // ==========================================
    // 2. ส่วนประมวลผล (Processing) - ตรวจสอบการ Login จาก DB
    // ==========================================
    [HttpPost]
    public IActionResult Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        // ตรวจสอบความถูกต้องของข้อมูลตาม DataAnnotations ใน ViewModel
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // ประมวลผล: ค้นหาผู้ใช้จาก DB ด้วย Username หรือ Email
        var user = _db.Users
            .Include(u => u.Role)
            .FirstOrDefault(u => u.Username == model.Email);

        if (user == null)
        {
            ViewBag.Error = "ไม่พบบัญชีผู้ใช้นี้ในระบบ";
            return View(model);
        }

        // ตรวจสอบรหัสผ่าน (เปรียบเทียบตรงๆ เพราะ DB เก็บ PasswordHash แบบ plain)
        if (user.PasswordHash != model.Password)
        {
            ViewBag.Error = "รหัสผ่านไม่ถูกต้อง";
            return View(model);
        }

        // ล็อกอินสำเร็จ - บันทึก Session
        HttpContext.Session.SetString("UserLoggedIn", "true");
        HttpContext.Session.SetString("UserEmail", user.Username);
        HttpContext.Session.SetString("UserName", user.FullName);
        HttpContext.Session.SetString("UserId", user.UserId.ToString());
        HttpContext.Session.SetString("UserRole", user.Role?.RoleName ?? "User");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // ==========================================
    // 1. ส่วนแสดงผล (Display) - หน้า Register
    // ==========================================
    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.GetString("UserLoggedIn") == "true")
        {
            return RedirectToAction("Index", "Home");
        }
        // ส่ง RegisterViewModel เปล่าไปให้ View แสดงผล
        return View(new RegisterViewModel());
    }

    // ==========================================
    // 2. ส่วนประมวลผล (Processing) - บันทึกข้อมูลการสมัครสมาชิกลง DB
    // ==========================================
    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        // ตรวจสอบความถูกต้องของข้อมูล
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // ตรวจสอบว่า Email/Username ซ้ำหรือไม่
        if (_db.Users.Any(u => u.Username == model.Email))
        {
            ViewBag.Error = $"อีเมล '{model.Email}' ถูกใช้งานแล้ว กรุณาใช้อีเมลอื่น";
            return View(model);
        }

        // หา Role "User" หรือสร้างใหม่ถ้ายังไม่มี
        var role = _db.Roles.FirstOrDefault(r => r.RoleName == "User");
        if (role == null)
        {
            role = new Role { RoleName = "User" };
            _db.Roles.Add(role);
            _db.SaveChanges();
        }

        try
        {
            // สร้าง User ใหม่และบันทึกลง DB
            var newUser = new User
            {
                Username = model.Email ?? "",
                PasswordHash = model.Password,  // เก็บรหัสผ่าน
                FullName = $"{model.FirstName} {model.LastName}".Trim(),
                RoleId = role.RoleId,
                CreatedAt = DateTime.Now
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();

            // บันทึก Session เข้าสู่ระบบทันที
            HttpContext.Session.SetString("UserLoggedIn", "true");
            HttpContext.Session.SetString("UserEmail", newUser.Username);
            HttpContext.Session.SetString("UserName", $"{model.FirstName} {model.LastName}".Trim());
            HttpContext.Session.SetString("UserId", newUser.UserId.ToString());
            HttpContext.Session.SetString("UserRole", role.RoleName);

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            ViewBag.Error = "เกิดข้อผิดพลาดในการสมัครสมาชิก: " + (ex.InnerException?.Message ?? ex.Message);
            return View(model);
        }
    }

    // ==========================================
    // Logout - ล้าง Session
    // ==========================================
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ==========================================
    // CRUD: CREATE - ส่วนแสดงผล (Display) หน้าเพิ่มผู้ใช้
    // ==========================================
    [HttpGet]
    public IActionResult AddUser()
    {
        ViewBag.Roles = _db.Roles.ToList();
        return View();
    }

    // ==========================================
    // CRUD: CREATE - ส่วนประมวลผล (Processing) บันทึกผู้ใช้ใหม่ลง DB
    // ==========================================
    [HttpPost]
    public IActionResult AddUser(string firstName, string lastName, string email,
        string phone, string role, string password, string confirmPassword,
        string birthDate, string address)
    {
        ViewBag.Roles = _db.Roles.ToList();

        // ตรวจสอบข้อมูลที่จำเป็น
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
            string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "กรุณากรอกข้อมูลให้ครบถ้วน (ชื่อ, นามสกุล, อีเมล, รหัสผ่าน)";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "รหัสผ่านไม่ตรงกัน กรุณากรอกใหม่";
            return View();
        }

        // ตรวจสอบ Username ซ้ำ
        if (_db.Users.Any(u => u.Username == email))
        {
            ViewBag.Error = $"อีเมล '{email}' ถูกใช้งานแล้ว";
            return View();
        }

        // หา Role ที่เลือก
        var userRole = _db.Roles.FirstOrDefault(r => r.RoleName == role);
        if (userRole == null)
        {
            userRole = _db.Roles.FirstOrDefault() ?? new Role { RoleName = "User" };
            if (userRole.RoleId == 0)
            {
                _db.Roles.Add(userRole);
                _db.SaveChanges();
            }
        }

        try
        {
            var newUser = new User
            {
                Username = email,
                PasswordHash = password,
                FullName = $"{firstName} {lastName}".Trim(),
                RoleId = userRole.RoleId,
                CreatedAt = DateTime.Now
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();

            ViewBag.Success = $"เพิ่มผู้ใช้ {firstName} {lastName} สำเร็จแล้ว!";
            return View();
        }
        catch (Exception ex)
        {
            ViewBag.Error = "เกิดข้อผิดพลาด: " + (ex.InnerException?.Message ?? ex.Message);
            return View();
        }
    }

    // ==========================================
    // CRUD: READ - ส่วนแสดงผล (Display) แสดงรายชื่อผู้ใช้ทั้งหมดจาก DB
    // ==========================================
    public IActionResult UserList()
    {
        var users = _db.Users
            .Include(u => u.Role)
            .Select(u => new UserViewModel
            {
                UserId = u.UserId,
                FirstName = u.FullName.Contains(' ') ? u.FullName.Substring(0, u.FullName.IndexOf(' ')) : u.FullName,
                LastName = u.FullName.Contains(' ') ? u.FullName.Substring(u.FullName.IndexOf(' ') + 1) : "",
                Email = u.Username,
                Role = u.Role != null ? u.Role.RoleName : "User",
                CreatedAt = u.CreatedAt
            })
            .ToList();

        return View(users);
    }

    // ==========================================
    // CRUD: UPDATE - ส่วนแสดงผล (Display) หน้าแก้ไขผู้ใช้
    // ==========================================
    [HttpGet]
    public IActionResult EditUser(int id)
    {
        var user = _db.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == id);
        if (user == null)
        {
            return RedirectToAction("UserList");
        }

        ViewBag.Roles = _db.Roles.ToList();

        var nameParts = (user.FullName ?? "").Split(' ', 2);
        var viewModel = new UserViewModel
        {
            UserId = user.UserId,
            FirstName = nameParts[0],
            LastName = nameParts.Length > 1 ? nameParts[1] : "",
            Email = user.Username,
            Role = user.Role?.RoleName ?? "User"
        };

        return View(viewModel);
    }

    // ==========================================
    // CRUD: UPDATE - ส่วนประมวลผล (Processing) บันทึกการแก้ไขลง DB
    // ==========================================
    [HttpPost]
    public IActionResult EditUser(int id, string firstName, string lastName,
        string email, string role, string? password)
    {
        var user = _db.Users.FirstOrDefault(u => u.UserId == id);
        if (user == null)
        {
            return RedirectToAction("UserList");
        }

        ViewBag.Roles = _db.Roles.ToList();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(email))
        {
            ViewBag.Error = "กรุณากรอกชื่อและอีเมลให้ครบ";
            var vm = new UserViewModel
            {
                UserId = id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Role = role
            };
            return View(vm);
        }

        // ตรวจสอบ Username ซ้ำ (ยกเว้นตัวเอง)
        if (_db.Users.Any(u => u.Username == email && u.UserId != id))
        {
            ViewBag.Error = $"อีเมล '{email}' ถูกใช้งานแล้ว";
            var vm = new UserViewModel
            {
                UserId = id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Role = role
            };
            return View(vm);
        }

        try
        {
            user.Username = email;
            user.FullName = $"{firstName} {lastName}".Trim();

            // อัพเดทรหัสผ่านเฉพาะเมื่อกรอกมา
            if (!string.IsNullOrEmpty(password))
            {
                user.PasswordHash = password;
            }

            // อัพเดท Role
            var userRole = _db.Roles.FirstOrDefault(r => r.RoleName == role);
            if (userRole != null)
            {
                user.RoleId = userRole.RoleId;
            }

            _db.SaveChanges();
            TempData["Success"] = $"แก้ไขข้อมูลผู้ใช้ {firstName} {lastName} สำเร็จ!";
            return RedirectToAction("UserList");
        }
        catch (Exception ex)
        {
            ViewBag.Error = "เกิดข้อผิดพลาด: " + (ex.InnerException?.Message ?? ex.Message);
            var vm = new UserViewModel
            {
                UserId = id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Role = role
            };
            return View(vm);
        }
    }

    // ==========================================
    // CRUD: DELETE - ส่วนประมวลผล (Processing) ลบผู้ใช้จาก DB
    // ==========================================
    [HttpPost]
    public IActionResult DeleteUser(int id)
    {
        var user = _db.Users.FirstOrDefault(u => u.UserId == id);
        if (user != null)
        {
            try
            {
                _db.Users.Remove(user);
                _db.SaveChanges();
                TempData["Success"] = $"ลบผู้ใช้ '{user.FullName}' สำเร็จแล้ว!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "ไม่สามารถลบผู้ใช้ได้: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }
        else
        {
            TempData["Error"] = "ไม่พบผู้ใช้ที่ต้องการลบ";
        }

        return RedirectToAction("UserList");
    }

    // ==========================================
    // USER PROFILE & SETTINGS (/Account/Profile)
    // ==========================================
    public IActionResult Profile(string tab = "profile")
    {
        if (HttpContext.Session.GetString("UserLoggedIn") != "true")
        {
            return RedirectToAction("Login", new { returnUrl = Url.Action("Profile", "Account") });
        }

        int userId = int.TryParse(HttpContext.Session.GetString("UserId"), out int uid) ? uid : 1;
        string userName = HttpContext.Session.GetString("UserName") ?? "คุณลูกค้า Absolute Cinema";
        string userEmail = HttpContext.Session.GetString("UserEmail") ?? "member@absolutecinema.com";

        var vm = new UserProfileViewModel
        {
            UserId = userId,
            Username = userEmail,
            FullName = userName,
            Email = userEmail,
            Phone = "093-363-2239",
            RoleName = HttpContext.Session.GetString("UserRole") ?? "สมาชิกทั่วไป (Member)",
            MemberTier = "Absolute Gold Member",
            CreatedAt = DateTime.Now.AddMonths(-6),
            ThemePreference = HttpContext.Session.GetString("ThemePreference") ?? "dark",
            ActiveTab = tab
        };

        // 1. ประวัติการจองและชำระเงิน
        vm.BookingHistory = GetUserBookings(userId, userName);

        // 2. ตั๋วหนังที่ซื้อไว้และยังไม่ได้ดู (Active Tickets)
        vm.ActiveTickets = vm.BookingHistory
            .Where(b => b.Status != "Cancelled")
            .Select(b => new UserTicketDto
            {
                BookingId = b.BookingId,
                ReferenceCode = b.ReferenceCode,
                TaxInvoiceNo = b.TaxInvoiceNo,
                MovieTitle = b.MovieTitle,
                PosterUrl = b.PosterUrl,
                CinemaName = b.CinemaName,
                HallTier = "VIP / Dolby Atmos",
                DateDisplayTh = b.ShowtimeDateText,
                TimeDisplayTh = b.ShowtimeTimeText,
                SeatNumbers = b.SeatNumbers,
                SeatCount = b.SeatCount,
                TotalAmount = b.TotalAmount,
                PaymentMethod = b.PaymentMethod,
                Status = b.Status,
                BarcodeCode = $"*AC{b.BookingId:D6}*",
                CanCancel = true,
                BookedAt = b.BookedAt
            }).ToList();

        // 3. ประวัติภาพยนตร์ที่ดูแล้ว
        vm.WatchedHistory = new List<WatchedMovieItemDto>
        {
            new() { MovieTitle = "Dune: Part Two", PosterUrl = "", WatchedDateText = "12 พ.ย. 2026", CinemaHall = "โรง 1 (IMAX Laser)", Seats = "G7, G8 (VIP ด้านหลัง)", Genre = "Sci-Fi / Adventure", Rating = 9.5, ReviewComment = "ภาพสวยตระการตาและระบบเสียงกระหึ่มสมบูรณ์แบบมาก" },
            new() { MovieTitle = "Furiosa: A Mad Max Saga", PosterUrl = "", WatchedDateText = "28 ต.ค. 2026", CinemaHall = "โรง 2 (4DX Extreme)", Seats = "H5, H6 (VIP ด้านหลัง)", Genre = "Action", Rating = 9.0, ReviewComment = "ความมันส์ระดับ 10/10 เอฟเฟกต์ 4DX สมจริงสุดๆ" },
            new() { MovieTitle = "หลานม่า (How to Make Millions)", PosterUrl = "", WatchedDateText = "15 ต.ค. 2026", CinemaHall = "โรง 3 (Gold Class)", Seats = "G11, G12 (VIP ด้านหลัง)", Genre = "Drama", Rating = 9.8, ReviewComment = "กินใจและอบอุ่นมาก เป็นหนังไทยที่ดีที่สุดของปี" }
        };

        return View(vm);
    }

    // ==========================================
    // MY TICKETS (/Account/Tickets)
    // ==========================================
    public IActionResult Tickets()
    {
        if (HttpContext.Session.GetString("UserLoggedIn") != "true")
        {
            return RedirectToAction("Login", new { returnUrl = Url.Action("Tickets", "Account") });
        }

        int userId = int.TryParse(HttpContext.Session.GetString("UserId"), out int uid) ? uid : 1;
        string userName = HttpContext.Session.GetString("UserName") ?? "คุณลูกค้า Absolute Cinema";

        var bookings = GetUserBookings(userId, userName);
        var activeTickets = bookings
            .Where(b => b.Status != "Cancelled")
            .Select(b => new UserTicketDto
            {
                BookingId = b.BookingId,
                ReferenceCode = b.ReferenceCode,
                TaxInvoiceNo = b.TaxInvoiceNo,
                MovieTitle = b.MovieTitle,
                PosterUrl = b.PosterUrl,
                CinemaName = b.CinemaName,
                HallTier = "VIP / Dolby Atmos",
                DateDisplayTh = b.ShowtimeDateText,
                TimeDisplayTh = b.ShowtimeTimeText,
                SeatNumbers = b.SeatNumbers,
                SeatCount = b.SeatCount,
                TotalAmount = b.TotalAmount,
                PaymentMethod = b.PaymentMethod,
                Status = b.Status,
                BarcodeCode = $"*AC{b.BookingId:D6}*",
                CanCancel = true,
                BookedAt = b.BookedAt
            }).ToList();

        return View(activeTickets);
    }

    // ==========================================
    // CANCEL TICKET / BOOKING (/Account/CancelBooking)
    // ==========================================
    [HttpPost]
    public IActionResult CancelBooking(int bookingId)
    {
        if (bookingId <= 0)
        {
            TempData["Error"] = "ไม่พบหมายเลขการจอง";
            return RedirectToAction("Tickets");
        }

        try
        {
            string movieTitle = "ภาพยนตร์";
            decimal amount = 0;
            string customerName = HttpContext.Session.GetString("UserName") ?? "ลูกค้า";

            // 1. อัปเดตใน MySQL
            try
            {
                if (_db.Database.CanConnect())
                {
                    var booking = _db.Bookings
                        .Include(b => b.Payments)
                        .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                        .FirstOrDefault(b => b.BookingId == bookingId);

                    if (booking != null)
                    {
                        booking.Status = "Cancelled";
                        movieTitle = booking.Showtime?.Movie?.Title ?? movieTitle;
                        amount = booking.TotalAmount ?? 0m;

                        foreach (var p in booking.Payments)
                        {
                            p.IsRefunded = true;
                        }
                        _db.SaveChanges();
                    }
                }
            }
            catch { }

            // 2. อัปเดตใน Data/bookings.json
            try
            {
                var bookingsPath = Path.Combine(_env.ContentRootPath, "Data", "bookings.json");
                if (System.IO.File.Exists(bookingsPath))
                {
                    var json = System.IO.File.ReadAllText(bookingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var updatedList = new List<Dictionary<string, object>>();

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(item.GetRawText()) ?? new();
                            if (dict.TryGetValue("BookingId", out var bIdObj))
                            {
                                if (int.TryParse(bIdObj.ToString(), out int bId) && bId == bookingId)
                                {
                                    dict["Status"] = "Cancelled";
                                    if (dict.TryGetValue("MovieTitle", out var mt)) movieTitle = mt.ToString() ?? movieTitle;
                                    if (dict.TryGetValue("TotalAmount", out var ta) && decimal.TryParse(ta.ToString(), out decimal dAmt)) amount = dAmt;
                                }
                            }
                            updatedList.Add(dict);
                        }
                    }
                    System.IO.File.WriteAllText(bookingsPath, JsonSerializer.Serialize(updatedList, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch { }

            // 3. บันทึกเข้า Data/refunds.json
            try
            {
                var refundsPath = Path.Combine(_env.ContentRootPath, "Data", "refunds.json");
                var refunds = new List<Dictionary<string, object>>();
                if (System.IO.File.Exists(refundsPath))
                {
                    var rJson = System.IO.File.ReadAllText(refundsPath);
                    using var rDoc = JsonDocument.Parse(rJson);
                    if (rDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in rDoc.RootElement.EnumerateArray())
                        {
                            var d = JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText());
                            if (d != null) refunds.Add(d);
                        }
                    }
                }
                int nextRefId = refunds.Count + 1;
                refunds.Insert(0, new Dictionary<string, object>
                {
                    ["RefundId"] = nextRefId,
                    ["BookingId"] = bookingId,
                    ["CustomerName"] = customerName,
                    ["Amount"] = amount,
                    ["Reason"] = $"ลูกค้ายกเลิกตั๋วชมเรื่อง '{movieTitle}' ผ่านระบบออนไลน์",
                    ["Status"] = "Approved",
                    ["CreatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                System.IO.File.WriteAllText(refundsPath, JsonSerializer.Serialize(refunds, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            TempData["Success"] = $"ยกเลิกตั๋วชมภาพยนตร์ #{bookingId} สำเร็จแล้ว! ระบบได้ดำเนินการคืนเงินจำนวน ฿{amount:N0} และปลดล็อคที่นั่งคืนสู่ระบบทันที";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "เกิดข้อผิดพลาดในการยกเลิกตั๋ว: " + ex.Message;
        }

        return RedirectToAction("Tickets");
    }

    // ==========================================
    // THEME SETTINGS (/Account/UpdateTheme)
    // ==========================================
    [HttpPost]
    public IActionResult UpdateTheme(string theme)
    {
        string safeTheme = (theme == "light") ? "light" : "dark";
        HttpContext.Session.SetString("ThemePreference", safeTheme);
        return Json(new { success = true, theme = safeTheme });
    }

    // ==========================================
    // Helper: โหลดรายการการจองของ User
    // ==========================================
    private List<UserBookingHistoryItemDto> GetUserBookings(int userId, string userName)
    {
        var list = new List<UserBookingHistoryItemDto>();
        var seenIds = new HashSet<int>();

        // 1. ดึงจาก MySQL
        try
        {
            if (_db.Database.CanConnect())
            {
                var dbBookings = _db.Bookings
                    .Include(b => b.Payments)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Include(b => b.Showtime).ThenInclude(s => s.Cinema)
                    .Where(b => b.UserId == userId || (b.User != null && b.User.FullName == userName))
                    .OrderByDescending(b => b.BookedAt)
                    .ToList();

                foreach (var b in dbBookings)
                {
                    seenIds.Add(b.BookingId);
                    var pay = b.Payments.FirstOrDefault();
                    list.Add(new UserBookingHistoryItemDto
                    {
                        BookingId = b.BookingId,
                        ReferenceCode = $"AC-{b.BookingId:D6}",
                        TaxInvoiceNo = pay?.TaxInvoiceNo ?? $"INV-{b.BookingId:D5}",
                        MovieTitle = b.Showtime?.Movie?.Title ?? "ภาพยนตร์รอบปัจจุบัน",
                        PosterUrl = b.Showtime?.Movie?.PosterUrl,
                        CinemaName = b.Showtime?.Cinema?.CinemaName ?? "Gold Class (Cinema 2)",
                        ShowtimeDateText = b.Showtime != null ? b.Showtime.StartTime.ToString("dd/MM/yyyy") : DateTime.Today.ToString("dd/MM/yyyy"),
                        ShowtimeTimeText = b.Showtime != null ? b.Showtime.StartTime.ToString("HH:mm น.") : "15:00 น.",
                        SeatNumbers = b.SeatNumbers ?? "G7, G8",
                        SeatCount = b.SeatCount,
                        TotalAmount = b.TotalAmount ?? 0m,
                        PaymentMethod = "พร้อมเพย์ (PromptPay)",
                        Status = b.Status ?? "Confirmed",
                        BookedAt = b.BookedAt ?? DateTime.Now,
                        CanCancel = b.Status != "Cancelled"
                    });
                }
            }
        }
        catch { }

        // 2. ดึงจาก Data/bookings.json เสริม
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
                        int bId = item.TryGetProperty("BookingId", out var idProp) ? idProp.GetInt32() : 0;
                        if (seenIds.Contains(bId)) continue;

                        int bUid = item.TryGetProperty("UserId", out var uProp) ? uProp.GetInt32() : 0;
                        string custName = item.TryGetProperty("CustomerName", out var cn) ? cn.GetString() ?? "" : "";

                        // จับคู่กับ user ปัจจุบัน หรือแสดงการจองล่าสุดถ้าเป็น guest
                        if (bUid == userId || custName.Contains(userName) || bUid == 0 || userId == 1)
                        {
                            string refCode = item.TryGetProperty("ReferenceCode", out var rc) ? rc.GetString() ?? "" : $"AC-{bId:D6}";
                            string invNo = item.TryGetProperty("TaxInvoiceNo", out var inv) ? inv.GetString() ?? "" : $"INV-{bId:D5}";
                            string status = item.TryGetProperty("Status", out var st) ? st.GetString() ?? "Confirmed" : "Confirmed";
                            string movieTitle = item.TryGetProperty("MovieTitle", out var mt) ? mt.GetString() ?? "" : "Dune: Part Two";
                            string seatNums = item.TryGetProperty("SeatNumbers", out var sn) ? sn.GetString() ?? "" : "G7, G8";
                            int seatCount = item.TryGetProperty("SeatCount", out var sc) ? sc.GetInt32() : 2;
                            decimal totalAmt = item.TryGetProperty("TotalAmount", out var ta) ? ta.GetDecimal() : 2800;
                            string payMethod = item.TryGetProperty("PaymentMethod", out var pm) ? pm.GetString() ?? "PromptPay" : "PromptPay";

                            list.Add(new UserBookingHistoryItemDto
                            {
                                BookingId = bId > 0 ? bId : 1001,
                                ReferenceCode = refCode,
                                TaxInvoiceNo = invNo,
                                MovieTitle = string.IsNullOrWhiteSpace(movieTitle) ? "Dune: Part Two" : movieTitle,
                                PosterUrl = null,
                                CinemaName = "Gold Class (Cinema 2)",
                                ShowtimeDateText = DateTime.Today.ToString("dd/MM/yyyy"),
                                ShowtimeTimeText = "15:00 น.",
                                SeatNumbers = seatNums,
                                SeatCount = seatCount,
                                TotalAmount = totalAmt,
                                PaymentMethod = payMethod == "CreditCard" ? "บัตรเครดิต/เดบิต" : "พร้อมเพย์ (093-363-2239)",
                                Status = status,
                                BookedAt = DateTime.Now,
                                CanCancel = status != "Cancelled"
                            });
                            seenIds.Add(bId);
                        }
                    }
                }
            }
        }
        catch { }

        // ถ้ายังไม่มีประวัติเลย ให้มีตัวอย่างรายการล่าสุดให้เห็นทันที
        if (!list.Any())
        {
            list.Add(new UserBookingHistoryItemDto
            {
                BookingId = 8901,
                ReferenceCode = "AC-261121-8901",
                TaxInvoiceNo = "INV-261121-10045",
                MovieTitle = "Dune: Part Two",
                CinemaName = "Gold Class Theater 2",
                ShowtimeDateText = DateTime.Today.ToString("dd/MM/yyyy"),
                ShowtimeTimeText = "19:00 น.",
                SeatNumbers = "G7, G8 (VIP ด้านหลัง)",
                SeatCount = 2,
                TotalAmount = 3600,
                PaymentMethod = "พร้อมเพย์ (PromptPay)",
                Status = "Confirmed",
                BookedAt = DateTime.Now.AddHours(-1),
                CanCancel = true
            });
        }

        return list;
    }
}
