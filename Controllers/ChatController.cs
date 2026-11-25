using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.Helpers;

namespace Kawin_67091861_002.Controllers;

[Route("[controller]")]
public class ChatController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public ChatController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Retrieve messages for any channel (role-agnostic)
    [HttpGet("GetMessagesApi")]
    public IActionResult GetMessagesApi(string channelId)
    {
        string role = HttpContext.Session.GetString("UserRole") ?? "Staff";
        string userName = HttpContext.Session.GetString("UserName") ?? "";
        int userId = 0;
        int.TryParse(HttpContext.Session.GetString("UserId"), out userId);
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, channelId ?? "group", role, userName, userId);
        return Json(messages);
    }

    // Send a message to any channel
    [HttpPost("SendMessageApi")]
    public IActionResult SendMessageApi(string channelId, string message)
    {
        var userName = HttpContext.Session.GetString("UserName") ?? "Anonymous";
        var userRole = HttpContext.Session.GetString("UserRole") ?? "Staff";
        if (!string.IsNullOrWhiteSpace(channelId) && !string.IsNullOrWhiteSpace(message))
        {
            ChatHelper.SendMessage(_db, _env, channelId, message, userName, userRole, HttpContext);
        }
        return Json(new { success = true });
    }
}
