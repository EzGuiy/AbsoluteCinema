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
using Kawin_67091861_002.Helpers;

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

        var vm = new LoginViewModel();
        if (Request.Cookies["RememberMe"] == "true")
        {
            vm.Email = Request.Cookies["RememberEmail"] ?? "";
            vm.Password = Request.Cookies["RememberPassword"] ?? "";
            vm.RememberMe = true;
        }

        return View(vm);
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

        // เตรียมข้อมูลเริ่มต้นใน DB และค้นหาผู้ใช้จาก DB ด้วย Username/Email
        DbInitializer.Initialize(_db, _env.ContentRootPath);

        string inputLower = (model.Email ?? "").Trim().ToLowerInvariant();
        User? user = null;
        bool dbConnected = false;
        try
        {
            if (_db.Database.CanConnect())
            {
                dbConnected = true;
                user = _db.Users
                    .Include(u => u.Role)
                    .FirstOrDefault(u => u.Username.ToLower() == inputLower);
            }
        }
        catch { }

        // Fallback เฉพาะกรณีที่ฐานข้อมูล MySQL ยังไม่ได้เชื่อมต่อเท่านั้น
        if (user == null && !dbConnected)
        {
            if (model.Password == "password123")
            {
                user = inputLower switch
                {
                    "manager" or "manager@absolutecinema.com" => new User { UserId = 2, Username = "manager", FullName = "ณัฐชา วัฒนากุล (ผู้จัดการสาขา)", PasswordHash = "password123", Role = new Role { RoleName = "Manager" } },
                    "staff" or "staff@absolutecinema.com" => new User { UserId = 3, Username = "staff", FullName = "สมศรี รักบริการ (พนักงานบริการ)", PasswordHash = "password123", Role = new Role { RoleName = "Staff" } },
                    "technician" or "tech@absolutecinema.com" => new User { UserId = 4, Username = "technician", FullName = "อนุชา ทนทาน (ช่างเทคนิค)", PasswordHash = "password123", Role = new Role { RoleName = "Technician" } },
                    "accountant" or "acct@absolutecinema.com" => new User { UserId = 5, Username = "accountant", FullName = "ณิชา พงษ์ศิริ (ฝ่ายบัญชี)", PasswordHash = "password123", Role = new Role { RoleName = "Accountant" } },
                    "admin" or "admin@absolutecinema.com" => new User { UserId = 1, Username = "admin", FullName = "ผู้ดูแลระบบสาขา (Admin)", PasswordHash = "password123", Role = new Role { RoleName = "Admin" } },
                    "member" or "member@absolutecinema.com" => new User { UserId = 6, Username = "member", FullName = "กิตติพงษ์ สุขใจ (สมาชิกทั่วไป)", PasswordHash = "password123", Role = new Role { RoleName = "User" } },
                    _ => null
                };
            }
        }

        if (user == null || user.PasswordHash != model.Password)
        {
            ViewBag.Error = "อีเมล/ชื่อผู้ใช้ หรือรหัสผ่านไม่ถูกต้อง";
            return View(model);
        }

        // กำหนดบทบาทและตำแหน่งงาน
        string roleName = user.Role?.RoleName ?? "User";
        string positionTitle = roleName switch
        {
            "Staff" => "พนักงานบริการ",
            "Manager" => "ผู้จัดการสาขา",
            "Technician" => "ช่างเทคนิค",
            "Accountant" => "ฝ่ายบัญชี",
            "Admin" => "ผู้ดูแลระบบ",
            _ => ""
        };

        // จัดรูปแบบชื่อพนักงานให้มี (ตำแหน่งงาน) กำกับไว้ด้วยเสมอ
        string displayName = user.FullName ?? user.Username;
        if (!string.IsNullOrEmpty(positionTitle) && !displayName.Contains("(") && !displayName.Contains(positionTitle))
        {
            displayName = $"{displayName} ({positionTitle})";
        }

        // จัดการคุกกี้สำหรับ "จดจำฉัน" (Remember Me)
        if (model.RememberMe)
        {
            var cookieOptions = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };
            Response.Cookies.Append("RememberEmail", model.Email ?? "", cookieOptions);
            Response.Cookies.Append("RememberPassword", model.Password ?? "", cookieOptions);
            Response.Cookies.Append("RememberMe", "true", cookieOptions);
        }
        else
        {
            Response.Cookies.Delete("RememberEmail");
            Response.Cookies.Delete("RememberPassword");
            Response.Cookies.Delete("RememberMe");
        }

        // ล็อกอินสำเร็จ - บันทึก Session
        HttpContext.Session.SetString("UserLoggedIn", "true");
        HttpContext.Session.SetString("UserEmail", user.Username);
        HttpContext.Session.SetString("UserName", displayName);
        HttpContext.Session.SetString("UserId", user.UserId.ToString());
        HttpContext.Session.SetString("UserRole", roleName);
        string userPhone = UserProfileHelper.GetPhone(user.Username, user.UserId);
        HttpContext.Session.SetString("UserPhone", userPhone);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // นำทางตามบทบาทของพนักงานโดยอัตโนมัติ
        return roleName switch
        {
            "Staff" => RedirectToAction("Index", "Staff"),
            "Manager" => RedirectToAction("Index", "Manager"),
            "Technician" => RedirectToAction("Index", "Operations"),
            "Accountant" => RedirectToAction("Dashboard", "Accountant"),
            "Admin" => RedirectToAction("Showtimes", "Manager"),
            _ => RedirectToAction("Index", "Home")
        };
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

            // บันทึกเบอร์โทรศัพท์ลง UserProfileHelper
            UserProfileHelper.SaveOrUpdate(_env.ContentRootPath, newUser.UserId, newUser.Username, newUser.FullName, model.Phone, role.RoleName);

            // บันทึก Session เข้าสู่ระบบทันที
            HttpContext.Session.SetString("UserLoggedIn", "true");
            HttpContext.Session.SetString("UserEmail", newUser.Username);
            HttpContext.Session.SetString("UserName", $"{model.FirstName} {model.LastName}".Trim());
            HttpContext.Session.SetString("UserId", newUser.UserId.ToString());
            HttpContext.Session.SetString("UserRole", role.RoleName);
            HttpContext.Session.SetString("UserPhone", model.Phone ?? "");

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
        ViewBag.Roles = GetSafeRolesList();
        return View();
    }

    // ==========================================
    // CRUD: CREATE - ส่วนประมวลผล (Processing) บันทึกผู้ใช้ใหม่ลง DB
    // ==========================================
    [HttpPost]
    public IActionResult AddUser(string firstName, string lastName, string email,
        string phone, string role, string password, string confirmPassword,
        string birthDate, string address, string? position = null)
    {
        ViewBag.Roles = GetSafeRolesList();

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

        try
        {
            // ตรวจสอบ Username ซ้ำใน DB
            if (_db.Database.CanConnect() && _db.Users.Any(u => u.Username == email))
            {
                ViewBag.Error = $"อีเมล '{email}' ถูกใช้งานแล้ว";
                return View();
            }

            // หา Role ที่เลือก
            var userRole = _db.Roles.FirstOrDefault(r => r.RoleName == role);
            if (userRole == null)
            {
                userRole = _db.Roles.FirstOrDefault() ?? new Role { RoleName = role ?? "User" };
                if (userRole.RoleId == 0)
                {
                    _db.Roles.Add(userRole);
                    _db.SaveChanges();
                }
            }

            // กำหนดชื่อตำแหน่งงานสำหรับพนักงาน
            string posTitle = !string.IsNullOrWhiteSpace(position) ? position.Trim() : role switch
            {
                "Staff" => "พนักงานบริการ",
                "Technician" => "ช่างเทคนิค",
                "Accountant" => "ฝ่ายบัญชี",
                "Manager" => "ผู้จัดการสาขา",
                "Admin" => "ผู้ดูแลระบบ",
                _ => ""
            };

            string fullDisplayName = $"{firstName} {lastName}".Trim();
            if (!string.IsNullOrEmpty(posTitle) && !fullDisplayName.Contains($"({posTitle})"))
            {
                fullDisplayName = $"{fullDisplayName} ({posTitle})";
            }

            var newUser = new User
            {
                Username = email,
                PasswordHash = password,
                FullName = fullDisplayName,
                RoleId = userRole.RoleId,
                CreatedAt = DateTime.Now
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();

            // บันทึกเบอร์โทรศัพท์ลง UserProfileHelper
            UserProfileHelper.SaveOrUpdate(_env.ContentRootPath, newUser.UserId, newUser.Username, fullDisplayName, phone, role);

            // หากเป็นพนักงาน ให้ซิงค์ลงใน Data/manager_staff.json ด้วย
            if (role != "User")
            {
                try
                {
                    var staffPath = Path.Combine(_env.ContentRootPath, "Data", "manager_staff.json");
                    List<ManagerStaffRowDto> staffList = new();
                    if (System.IO.File.Exists(staffPath))
                    {
                        var json = System.IO.File.ReadAllText(staffPath);
                        staffList = JsonSerializer.Deserialize<List<ManagerStaffRowDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                    int nextStaffId = staffList.Any() ? staffList.Max(s => s.Id) + 1 : 1;
                    staffList.Add(new ManagerStaffRowDto
                    {
                        Id = nextStaffId,
                        Name = $"{firstName} {lastName}".Trim(),
                        RoleTitle = string.IsNullOrEmpty(posTitle) ? role : posTitle,
                        ShiftText = "เวรเช้า (08:00 - 16:00)",
                        AccessLevel = role switch { "Manager" => "ระดับบริหาร", "Admin" => "ระดับผู้ดูแล", "Technician" => "ช่างเทคนิค", _ => "ระดับเริ่มต้น" },
                        Status = "ปฏิบัติงาน (On-duty)",
                        StatusColor = "green"
                    });
                    System.IO.File.WriteAllText(staffPath, JsonSerializer.Serialize(staffList, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }
            }

            ViewBag.Success = $"เพิ่มพนักงาน/ผู้ใช้ {fullDisplayName} สำเร็จแล้ว!";
            return View();
        }
        catch (Exception ex)
        {
            ViewBag.Error = "เกิดข้อผิดพลาด: " + (ex.InnerException?.Message ?? ex.Message);
            return View();
        }
    }

    private List<Role> GetSafeRolesList()
    {
        try
        {
            if (_db.Database.CanConnect())
            {
                var rList = _db.Roles.ToList();
                if (rList.Any()) return rList;
            }
        }
        catch { }

        return new List<Role>
        {
            new() { RoleId = 1, RoleName = "Admin" },
            new() { RoleId = 2, RoleName = "User" },
            new() { RoleId = 3, RoleName = "Accountant" },
            new() { RoleId = 4, RoleName = "Technician" },
            new() { RoleId = 5, RoleName = "Manager" },
            new() { RoleId = 6, RoleName = "Staff" }
        };
    }

    // ==========================================
    // CRUD: READ - ส่วนแสดงผล (Display) แสดงรายชื่อผู้ใช้ทั้งหมดจาก DB
    // ==========================================
    public IActionResult Users() => UserList();

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

        string userPhone = HttpContext.Session.GetString("UserPhone") ?? "";
        if (string.IsNullOrEmpty(userPhone))
        {
            userPhone = UserProfileHelper.GetPhone(userEmail, userId);
        }
        if (string.IsNullOrEmpty(userPhone))
        {
            userPhone = "ยังไม่ได้ระบุเบอร์โทรศัพท์";
        }

        var vm = new UserProfileViewModel
        {
            UserId = userId,
            Username = userEmail,
            FullName = userName,
            Email = userEmail,
            Phone = userPhone,
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
                ShowtimeStartTime = b.ShowtimeStartTime,
                SeatNumbers = b.SeatNumbers,
                SeatCount = b.SeatCount,
                TotalAmount = b.TotalAmount,
                PaymentMethod = b.PaymentMethod,
                Status = b.Status,
                BarcodeCode = $"*AC{b.BookingId:D6}*",
                CanCancel = b.CanCancel,
                CancelDisallowReason = b.CancelDisallowReason,
                BookedAt = b.BookedAt
            }).ToList();

        // 3. ประวัติภาพยนตร์ที่ดูแล้ว (คำนวณจากตั๋วที่รอบฉายผ่านไปแล้วจริงเท่านั้น ไม่ใช้ข้อมูลจำลอง)
        vm.WatchedHistory = vm.BookingHistory
            .Where(b => b.Status == "Confirmed" && b.ShowtimeStartTime.HasValue && b.ShowtimeStartTime.Value < DateTime.Now)
            .Select(b => new WatchedMovieItemDto
            {
                MovieTitle = b.MovieTitle,
                PosterUrl = b.PosterUrl ?? "",
                WatchedDateText = b.ShowtimeDateText,
                CinemaHall = b.CinemaName,
                Seats = b.SeatNumbers,
                Genre = "ภาพยนตร์",
                Rating = 9.5,
                ReviewComment = "ชมภาพยนตร์ในโรงภาพยนตร์เรียบร้อยแล้ว"
            }).ToList();

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
                ShowtimeStartTime = b.ShowtimeStartTime,
                SeatNumbers = b.SeatNumbers,
                SeatCount = b.SeatCount,
                TotalAmount = b.TotalAmount,
                PaymentMethod = b.PaymentMethod,
                Status = b.Status,
                BarcodeCode = $"*AC{b.BookingId:D6}*",
                CanCancel = b.CanCancel,
                CancelDisallowReason = b.CancelDisallowReason,
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
            int userId = int.TryParse(HttpContext.Session.GetString("UserId"), out int uid) ? uid : 1;
            string userName = HttpContext.Session.GetString("UserName") ?? "ลูกค้า";
            string userEmail = HttpContext.Session.GetString("UserEmail") ?? "";

            // ตรวจสอบข้อมูลการจองของลูกค้ารายนี้
            var bookings = GetUserBookings(userId, userName);
            var target = bookings.FirstOrDefault(b => b.BookingId == bookingId);

            string movieTitle = target?.MovieTitle ?? "ภาพยนตร์";
            decimal amount = target?.TotalAmount ?? 0m;
            string refCode = target?.ReferenceCode ?? $"AC-{bookingId:D6}";
            string cinemaName = target?.CinemaName ?? "Gold Class";
            string showtimeDate = target?.ShowtimeDateText ?? DateTime.Today.ToString("dd/MM/yyyy");
            string showtimeTime = target?.ShowtimeTimeText ?? "15:00 น.";
            string seatNumbers = target?.SeatNumbers ?? "";
            DateTime showtimeStart = target?.ShowtimeStartTime ?? DateTime.Today.AddHours(15);
            string customerPhone = HttpContext.Session.GetString("UserPhone") ?? UserProfileHelper.GetPhone(userEmail, userId);

            // กฎเหล็ก: ต้องยกเลิกก่อนเวลาหนังเริ่ม 5 ชั่วโมงขึ้นไปเท่านั้น
            TimeSpan diff = showtimeStart - DateTime.Now;
            if (diff < TimeSpan.FromHours(5))
            {
                TempData["Error"] = $"ไม่สามารถยกเลิกตั๋วได้ เนื่องจากเหลือน้อยกว่า 5 ชั่วโมงก่อนเวลาเริ่มฉาย (เหลือเวลา {Math.Max(0, (int)diff.TotalHours)} ชั่วโมง {Math.Max(0, diff.Minutes)} นาที - เงื่อนไขต้องยกเลิกก่อนเวลาฉายอย่างน้อย 5 ชั่วโมงขึ้นไป)";
                return RedirectToAction("Tickets");
            }

            // ถ้าทัน (>= 5 ชม.): ปรับสถานะเป็น "รอดำเนินการคืนเงิน" และส่งเรื่องเข้าสู่ระบบพนักงานบัญชี
            // 1. อัปเดตใน MySQL
            try
            {
                if (_db.Database.CanConnect())
                {
                    var booking = _db.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
                    if (booking != null)
                    {
                        booking.Status = "รอดำเนินการคืนเงิน";
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
                            if (dict.TryGetValue("BookingId", out var bIdObj) && int.TryParse(bIdObj.ToString(), out int bId) && bId == bookingId)
                            {
                                dict["Status"] = "รอดำเนินการคืนเงิน";
                            }
                            updatedList.Add(dict);
                        }
                    }
                    System.IO.File.WriteAllText(bookingsPath, JsonSerializer.Serialize(updatedList, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch { }

            // 3. ส่งคำขอเข้าสู่ระบบพนักงานบัญชี (Data/refunds.json) พร้อมข้อมูลครบถ้วน
            try
            {
                var refundsPath = Path.Combine(_env.ContentRootPath, "Data", "refunds.json");
                var refunds = new List<RefundItemDto>();
                if (System.IO.File.Exists(refundsPath))
                {
                    var rJson = System.IO.File.ReadAllText(refundsPath);
                    refunds = JsonSerializer.Deserialize<List<RefundItemDto>>(rJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }

                string reqId = $"REF-{DateTime.Now:yyMMdd}-{new Random().Next(1000, 9999)}";
                refunds.Insert(0, new RefundItemDto
                {
                    RequestId = reqId,
                    CustomerName = userName,
                    CustomerEmail = !string.IsNullOrEmpty(userEmail) ? userEmail : "member@absolutecinema.com",
                    CustomerPhone = !string.IsNullOrEmpty(customerPhone) ? customerPhone : "081-xxx-xxxx",
                    MovieTitle = movieTitle,
                    CinemaName = cinemaName,
                    SeatNumbers = seatNumbers,
                    Amount = amount,
                    Status = "รอดำเนินการ",
                    BookingCode = refCode,
                    ShowtimeText = $"{showtimeDate} {showtimeTime}",
                    CancellationTimeText = $"{DateTime.Now:dd/MM/yyyy HH:mm น.} (ก่อนฉาย {(int)diff.TotalHours} ชั่วโมง)",
                    RefundAmountText = $"{amount:N2} บาท (คืนเต็มจำนวน)",
                    IsConfirmedByCustomer = false
                });

                System.IO.File.WriteAllText(refundsPath, JsonSerializer.Serialize(refunds, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            TempData["Success"] = $"ส่งคำขอยกเลิกตั๋วสำหรับรหัส {refCode} (เรื่อง {movieTitle}) เข้าสู่ระบบพนักงานบัญชีเรียบร้อยแล้ว เมื่อฝ่ายบัญชีตรวจสอบความถูกต้องและกดยืนยันอนุมัติ ระบบจะส่งอีเมลแจ้งผลพร้อมปุ่มยืนยันไปยัง '{userEmail}'";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "เกิดข้อผิดพลาดในการยกเลิกตั๋ว: " + ex.Message;
        }

        return RedirectToAction("Tickets");
    }

    // ==========================================
    // CONFIRM REFUND (/Account/ConfirmRefund)
    // ==========================================
    [HttpGet]
    public IActionResult ConfirmRefund(string? requestId)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            TempData["Error"] = "ไม่พบรหัสคำขอคืนเงิน";
            return RedirectToAction("Tickets");
        }

        RefundItemDto? refundItem = null;
        try
        {
            var refundsPath = Path.Combine(_env.ContentRootPath, "Data", "refunds.json");
            if (System.IO.File.Exists(refundsPath))
            {
                var rJson = System.IO.File.ReadAllText(refundsPath);
                var list = JsonSerializer.Deserialize<List<RefundItemDto>>(rJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                refundItem = list.FirstOrDefault(r => string.Equals(r.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
                if (refundItem != null)
                {
                    refundItem.IsConfirmedByCustomer = true;
                    refundItem.ConfirmedAtText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss น.");
                    System.IO.File.WriteAllText(refundsPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }
        catch { }

        if (refundItem == null)
        {
            refundItem = new RefundItemDto
            {
                RequestId = requestId,
                CustomerName = HttpContext.Session.GetString("UserName") ?? "คุณลูกค้า Absolute Cinema",
                MovieTitle = "ภาพยนตร์ที่ทำรายการคืนเงิน",
                CinemaName = "Gold Class",
                SeatNumbers = "-",
                Amount = 0,
                Status = "อนุมัติแล้ว",
                IsConfirmedByCustomer = true,
                ConfirmedAtText = DateTime.Now.ToString("dd/MM/yyyy HH:mm น.")
            };
        }

        return View(refundItem);
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
        string userEmail = HttpContext.Session.GetString("UserEmail") ?? "";

        bool dbLoaded = false;
        // 1. ดึงจาก MySQL 100%
        try
        {
            if (_db.Database.CanConnect())
            {
                dbLoaded = true;
                var dbBookings = _db.Bookings
                    .Include(b => b.Payments)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Include(b => b.Showtime).ThenInclude(s => s.Cinema)
                    .Where(b => b.UserId == userId)
                    .OrderByDescending(b => b.BookedAt)
                    .ToList();

                foreach (var b in dbBookings)
                {
                    seenIds.Add(b.BookingId);
                    var pay = b.Payments.FirstOrDefault();
                    DateTime stStart = b.Showtime != null ? b.Showtime.StartTime : DateTime.Today.AddHours(15);
                    TimeSpan diff = stStart - DateTime.Now;
                    string status = b.Status ?? "Confirmed";
                    bool canCancel = (diff >= TimeSpan.FromHours(5)) && status != "Cancelled" && status != "รอดำเนินการคืนเงิน";
                    string? disallowReason = null;
                    if (status == "Cancelled")
                    {
                        disallowReason = "ตั๋วนี้ถูกยกเลิกแล้ว";
                    }
                    else if (status == "รอดำเนินการคืนเงิน")
                    {
                        disallowReason = "อยู่ระหว่างการพิจารณาคืนเงินโดยฝ่ายบัญชี";
                    }
                    else if (diff < TimeSpan.FromHours(5))
                    {
                        disallowReason = $"ไม่สามารถยกเลิกได้ (เหลือน้อยกว่า 5 ชม. ก่อนเริ่มฉาย - เหลือ {Math.Max(0, (int)diff.TotalHours)} ชม. {Math.Max(0, diff.Minutes)} นาที)";
                    }

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
                        ShowtimeStartTime = stStart,
                        SeatNumbers = b.SeatNumbers ?? "G7, G8",
                        SeatCount = b.SeatCount,
                        TotalAmount = b.TotalAmount ?? 0m,
                        PaymentMethod = "พร้อมเพย์ (PromptPay)",
                        Status = status,
                        BookedAt = b.BookedAt ?? DateTime.Now,
                        CanCancel = canCancel,
                        CancelDisallowReason = disallowReason
                    });
                }
            }
        }
        catch { }

        // 2. ดึงจาก Data/bookings.json เฉพาะกรณีที่ฐานข้อมูล MySQL ไม่ได้เชื่อมต่อ
        if (!dbLoaded)
        {
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
                        string custEmail = item.TryGetProperty("CustomerEmail", out var ce) ? ce.GetString() ?? "" : "";

                        // จับคู่กับ user ปัจจุบันอย่างแม่นยำ (ไม่เหมาบวม guest/uid=1)
                        bool matchesUser = (bUid > 0 && bUid == userId) ||
                                           (!string.IsNullOrEmpty(custEmail) && custEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) ||
                                           (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(custName) && custName.Contains(userName));

                        if (matchesUser)
                        {
                            string refCode = item.TryGetProperty("ReferenceCode", out var rc) ? rc.GetString() ?? "" : $"AC-{bId:D6}";
                            string invNo = item.TryGetProperty("TaxInvoiceNo", out var inv) ? inv.GetString() ?? "" : $"INV-{bId:D5}";
                            string status = item.TryGetProperty("Status", out var st) ? st.GetString() ?? "Confirmed" : "Confirmed";
                            string movieTitle = item.TryGetProperty("MovieTitle", out var mt) ? mt.GetString() ?? "" : "ภาพยนตร์";
                            string? posterUrl = item.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null;
                            string cinemaName = item.TryGetProperty("CinemaName", out var cnm) ? cnm.GetString() ?? "Gold Class (Cinema 2)" : "Gold Class (Cinema 2)";
                            string showDate = item.TryGetProperty("ShowtimeDateText", out var sdt) ? sdt.GetString() ?? DateTime.Today.ToString("dd/MM/yyyy") : DateTime.Today.ToString("dd/MM/yyyy");
                            string showTime = item.TryGetProperty("ShowtimeTimeText", out var stt) ? stt.GetString() ?? "15:00 น." : "15:00 น.";
                            string seatNums = item.TryGetProperty("SeatNumbers", out var sn) ? sn.GetString() ?? "" : "";
                            int seatCount = item.TryGetProperty("SeatCount", out var sc) ? sc.GetInt32() : 1;
                            decimal totalAmt = item.TryGetProperty("TotalAmount", out var ta) ? ta.GetDecimal() : 0;
                            string payMethod = item.TryGetProperty("PaymentMethod", out var pm) ? pm.GetString() ?? "PromptPay" : "PromptPay";

                            DateTime stStart = DateTime.Today.AddHours(15);
                            if (item.TryGetProperty("ShowtimeStartTime", out var sst) && DateTime.TryParse(sst.GetString(), out var parsedSst))
                            {
                                stStart = parsedSst;
                            }

                            TimeSpan diff = stStart - DateTime.Now;
                            bool canCancel = (diff >= TimeSpan.FromHours(5)) && status != "Cancelled" && status != "รอดำเนินการคืนเงิน";
                            string? disallowReason = null;
                            if (status == "Cancelled")
                            {
                                disallowReason = "ตั๋วนี้ถูกยกเลิกแล้ว";
                            }
                            else if (status == "รอดำเนินการคืนเงิน")
                            {
                                disallowReason = "อยู่ระหว่างการพิจารณาคืนเงินโดยฝ่ายบัญชี";
                            }
                            else if (diff < TimeSpan.FromHours(5))
                            {
                                disallowReason = $"ไม่สามารถยกเลิกได้ (เหลือน้อยกว่า 5 ชม. ก่อนเริ่มฉาย - เหลือ {Math.Max(0, (int)diff.TotalHours)} ชม. {Math.Max(0, diff.Minutes)} นาที)";
                            }

                            DateTime bookedAt = DateTime.Now;
                            if (item.TryGetProperty("BookedAt", out var bat) && DateTime.TryParse(bat.GetString(), out var parsedBat))
                            {
                                bookedAt = parsedBat;
                            }

                            list.Add(new UserBookingHistoryItemDto
                            {
                                BookingId = bId > 0 ? bId : 1001,
                                ReferenceCode = refCode,
                                TaxInvoiceNo = invNo,
                                MovieTitle = movieTitle,
                                PosterUrl = posterUrl,
                                CinemaName = cinemaName,
                                ShowtimeDateText = showDate,
                                ShowtimeTimeText = showTime,
                                ShowtimeStartTime = stStart,
                                SeatNumbers = seatNums,
                                SeatCount = seatCount,
                                TotalAmount = totalAmt,
                                PaymentMethod = payMethod == "CreditCard" ? "บัตรเครดิต/เดบิต" : "พร้อมเพย์ (PromptPay)",
                                Status = status,
                                BookedAt = bookedAt,
                                CanCancel = canCancel,
                                CancelDisallowReason = disallowReason
                            });
                            seenIds.Add(bId);
                        }
                    }
                }
            }
        }
        catch { }
        }

        // คืนค่ารายการจริงของผู้ใช้เท่านั้น (ไม่มีการใส่ข้อมูลหลอก Dune: Part Two เมื่อเป็นไอดีใหม่)
        return list;
    }

    // ==========================================
    // 7. แชทของแอดมิน (Admin Chat: แชทกลุ่มรวมทุกแผนก)
    // ==========================================
    [HttpGet]
    public IActionResult AdminChat(string channel = "group")
    {
        string activeRoom = "group";
        var rooms = ChatHelper.GetAdminRooms(_db, _env, activeRoom, HttpContext);

        HttpContext.Session.SetString("AdminChat_Seen_group", "true");
        var activeRoomDto = rooms.First();

        string adminName = HttpContext.Session.GetString("UserName") ?? "ผู้ดูแลระบบสาขา (Admin)";
        int adminId = ChatHelper.GetCurrentUserId(HttpContext, "Admin");
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, activeRoom, "Admin", adminName, adminId);

        var vm = new ManagerChatViewModel
        {
            ActiveRoomId = activeRoom,
            ActiveRoom = activeRoomDto,
            Rooms = rooms,
            Messages = messages,
            TotalUnreadCount = 0
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendAdminChatMessage(string channel, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            string adminName = HttpContext.Session.GetString("UserName") ?? "ผู้ดูแลระบบสาขา (Admin)";
            ChatHelper.SendMessage(_db, _env, "group", message, adminName, "Admin", HttpContext);
            HttpContext.Session.SetString("AdminChat_Seen_group", "true");
        }
        return RedirectToAction("AdminChat", new { channel = "group" });
    }

    [HttpGet]
    public IActionResult GetAdminMessagesApi(string channel = "group")
    {
        string adminName = HttpContext.Session.GetString("UserName") ?? "ผู้ดูแลระบบสาขา (Admin)";
        int adminId = ChatHelper.GetCurrentUserId(HttpContext, "Admin");
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, "group", "Admin", adminName, adminId);
        return Json(new { success = true, channel = "group", messages = messages });
    }

    [HttpPost]
    public IActionResult SendAdminMessageApi([FromForm] string channel, [FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { success = false, message = "ข้อความว่างเปล่า" });

        string adminName = HttpContext.Session.GetString("UserName") ?? "ผู้ดูแลระบบสาขา (Admin)";
        var newMsg = ChatHelper.SendMessage(_db, _env, "group", message, adminName, "Admin", HttpContext);

        HttpContext.Session.SetString("AdminChat_Seen_group", "true");
        return Json(new { success = true, newMsg = newMsg });
    }

    [HttpGet]
    public IActionResult GetAdminRoomsApi(string currentRoom = "group")
    {
        var rooms = ChatHelper.GetAdminRooms(_db, _env, "group", HttpContext);
        return Json(new { success = true, rooms = rooms });
    }
}
