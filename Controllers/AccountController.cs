using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;

namespace Kawin_67091861_002.Controllers;

public class AccountController : Controller
{
    private readonly AbsoluteCinemaContext _db;

    public AccountController(AbsoluteCinemaContext db)
    {
        _db = db;
    }

    // ==========================================
    // 1. ส่วนแสดงผล (Display) - หน้า Login
    // ==========================================
    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("UserLoggedIn") == "true")
        {
            return RedirectToAction("Index", "Home");
        }
        return View(new LoginViewModel());
    }

    // ==========================================
    // 2. ส่วนประมวลผล (Processing) - ตรวจสอบการ Login จาก DB
    // ==========================================
    [HttpPost]
    public IActionResult Login(LoginViewModel model)
    {
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
}
