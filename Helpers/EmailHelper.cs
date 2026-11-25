using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Kawin_67091861_002.Helpers
{
    public class SmtpConfig
    {
        public string Server { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string SenderEmail { get; set; } = "";
        public string SenderName { get; set; } = "Absolute Cinema Customer Service";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
    }

    public class SentEmailRecord
    {
        public string EmailId { get; set; } = "";
        public string RecipientEmail { get; set; } = "";
        public string RecipientName { get; set; } = "";
        public string Subject { get; set; } = "";
        public string HtmlBody { get; set; } = "";
        public string ConfirmationUrl { get; set; } = "";
        public string RequestId { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool SmtpSentSuccessfully { get; set; }
        public string StatusMessage { get; set; } = "";
    }

    public static class EmailHelper
    {
        private static readonly object _lock = new();

        /// <summary>
        /// ดึงการตั้งค่า SMTP จาก Data/smtp_settings.json หรือ appsettings.json
        /// </summary>
        public static SmtpConfig GetSmtpConfig(string contentRootPath, IConfiguration? config)
        {
            try
            {
                var filePath = Path.Combine(contentRootPath, "Data", "smtp_settings.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<SmtpConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (settings != null) return settings;
                }
            }
            catch { }

            // อ่านจาก appsettings.json
            var cfg = new SmtpConfig
            {
                Server = config?["SmtpSettings:Server"] ?? "smtp.gmail.com",
                Port = int.TryParse(config?["SmtpSettings:Port"], out int p) && p > 0 ? p : 587,
                SenderEmail = config?["SmtpSettings:SenderEmail"] ?? "",
                SenderName = config?["SmtpSettings:SenderName"] ?? "Absolute Cinema Customer Service",
                Username = config?["SmtpSettings:Username"] ?? "",
                Password = config?["SmtpSettings:Password"] ?? "",
                EnableSsl = !bool.TryParse(config?["SmtpSettings:EnableSsl"], out bool ssl) || ssl
            };

            return cfg;
        }

        /// <summary>
        /// บันทึกการตั้งค่า SMTP ลง Data/smtp_settings.json
        /// </summary>
        public static void SaveSmtpConfig(string contentRootPath, SmtpConfig settings)
        {
            lock (_lock)
            {
                try
                {
                    var dataDir = Path.Combine(contentRootPath, "Data");
                    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                    var filePath = Path.Combine(dataDir, "smtp_settings.json");
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                }
                catch { }
            }
        }

        /// <summary>
        /// ดึงประวัติอีเมลที่ส่งล่าสุดของ RequestId นั้นๆ
        /// </summary>
        public static SentEmailRecord? GetLatestEmailForRequest(string contentRootPath, string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            try
            {
                var filePath = Path.Combine(contentRootPath, "Data", "sent_emails.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var list = JsonSerializer.Deserialize<List<SentEmailRecord>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return list?.Find(e => string.Equals(e.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// สร้าง HTML Email เทมเพลตมาตรฐาน
        /// </summary>
        public static string GenerateRefundEmailHtml(
            string baseUrl,
            string requestId,
            string bookingCode,
            string customerName,
            string movieTitle,
            string cinemaName,
            string showtimeText,
            string seatNumbers,
            decimal refundAmount)
        {
            string confirmUrl = $"{baseUrl.TrimEnd('/')}/Account/ConfirmRefund?requestId={requestId}";
            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <style>
        body {{ font-family: 'Prompt', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #0b0b12; color: #e5e5ec; margin: 0; padding: 20px; }}
        .card {{ max-width: 580px; margin: 0 auto; background: #121218; border: 1px solid rgba(198,156,67,0.3); border-radius: 12px; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.8); }}
        .header {{ background: #08080c; border-bottom: 2px solid #dfb76c; padding: 24px; text-align: center; }}
        .brand {{ color: #dfb76c; font-size: 20px; font-weight: 700; letter-spacing: 2px; text-transform: uppercase; margin-bottom: 4px; }}
        .title {{ color: #ffffff; font-size: 16px; margin-top: 6px; font-weight: 600; }}
        .content {{ padding: 28px 26px; font-size: 14px; line-height: 1.7; color: #cfcfdc; }}
        .box {{ background: #181822; border: 1px solid rgba(255,255,255,0.08); border-radius: 8px; padding: 18px 20px; margin: 20px 0; }}
        .row {{ display: flex; justify-content: space-between; margin-bottom: 10px; font-size: 13.5px; }}
        .label {{ color: #8e8e9e; }}
        .val {{ color: #ffffff; font-weight: 600; }}
        .val-gold {{ color: #dfb76c; font-weight: 700; font-size: 16px; }}
        .cta {{ text-align: center; margin: 30px 0 15px; }}
        .btn {{ display: inline-block; background: linear-gradient(135deg, #eed08d 0%, #c69c43 100%); color: #0b0b12 !important; text-decoration: none; padding: 14px 34px; border-radius: 8px; font-weight: 700; font-size: 15px; letter-spacing: 0.5px; box-shadow: 0 4px 15px rgba(223,183,108,0.4); }}
        .footer {{ padding: 18px; text-align: center; font-size: 11.5px; color: #777788; border-top: 1px solid rgba(255,255,255,0.05); }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <div class='brand'>👑 ABSOLUTE CINEMA</div>
            <div class='title'>แจ้งผลการอนุมัติการคืนเงิน (Refund Approved)</div>
        </div>
        <div class='content'>
            <p>เรียน คุณ<strong>{customerName}</strong>,</p>
            <p>คำขอยกเลิกตั๋วภาพยนตร์และขอคืนเงินสำหรับรหัสการจอง <strong>{bookingCode}</strong> ของท่านได้รับการตรวจสอบความถูกต้องและ<strong>อนุมัติโดยฝ่ายบัญชี</strong>เรียบร้อยแล้ว ยอดเงินจำนวน <span style='color:#4ade80; font-weight:bold;'>฿{refundAmount:N2}</span> ได้รับการดำเนินการคืนสู่ช่องทางการชำระเงินของท่าน</p>
            
            <div class='box'>
                <div class='row'>
                    <span class='label'>รหัสคำขอ:</span>
                    <span class='val'>{requestId}</span>
                </div>
                <div class='row'>
                    <span class='label'>รหัสการจองตั๋ว:</span>
                    <span class='val'>{bookingCode}</span>
                </div>
                <div class='row'>
                    <span class='label'>ภาพยนตร์:</span>
                    <span class='val'>{movieTitle}</span>
                </div>
                <div class='row'>
                    <span class='label'>โรงภาพยนตร์:</span>
                    <span class='val'>{cinemaName}</span>
                </div>
                <div class='row'>
                    <span class='label'>รอบเวลาฉาย:</span>
                    <span class='val'>{showtimeText}</span>
                </div>
                <div class='row'>
                    <span class='label'>ที่นั่ง:</span>
                    <span class='val'>{seatNumbers}</span>
                </div>
                <div class='row' style='margin-bottom:0;'>
                    <span class='label'>ยอดเงินที่คืน:</span>
                    <span class='val-gold'>฿{refundAmount:N2} (คืนเต็มจำนวน)</span>
                </div>
            </div>

            <p style='text-align: center; color: #dfb76c;'>กรุณากดปุ่มด้านล่างนี้เพื่อยืนยันการรับทราบและเสร็จสิ้นขั้นตอนการคืนเงิน:</p>
            
            <div class='cta'>
                <a href='{confirmUrl}' class='btn' target='_blank'>ยืนยันรับทราบการคืนเงิน</a>
            </div>

            <p style='font-size: 12px; color: #888898; text-align: center; margin-top: 25px;'>
                หากปุ่มกดไม่ทำงาน สามารถคัดลอกลิงก์นี้ไปวางในเบราว์เซอร์ได้โดยตรง:<br />
                <a href='{confirmUrl}' style='color: #dfb76c;'>{confirmUrl}</a>
            </p>
        </div>
        <div class='footer'>
            &copy; {DateTime.Now.Year} ABSOLUTE CINEMA LUXURY EXPERIENCE. All rights reserved.<br />
            อีเมลฉบับนี้ส่งอัตโนมัติจากระบบ กรุณาอย่าตอบกลับ
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// ทดสอบส่งอีเมลผ่าน SMTP
        /// </summary>
        public static bool SendTestEmail(string contentRootPath, IConfiguration? config, string toEmail, out string statusMessage)
        {
            var smtp = GetSmtpConfig(contentRootPath, config);
            if (string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
            {
                statusMessage = "ยังไม่ได้ระบุ Username หรือ Password (รหัสผ่านแอป Gmail 16 หลัก) ในการตั้งค่า SMTP";
                return false;
            }

            try
            {
                string fromEmail = !string.IsNullOrWhiteSpace(smtp.SenderEmail) ? smtp.SenderEmail : smtp.Username;
                using var client = new SmtpClient(smtp.Server, smtp.Port)
                {
                    Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                    EnableSsl = smtp.EnableSsl,
                    Timeout = 15000
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(fromEmail, smtp.SenderName),
                    Subject = "[ABSOLUTE CINEMA] ทดสอบการเชื่อมต่อระบบส่งอีเมลจริง",
                    Body = $"<div style='font-family:sans-serif; padding:20px;'><h2 style='color:#dfb76c;'>Absolute Cinema Email Test</h2><p>ยินดีด้วย! ระบบเชื่อมต่อ SMTP ส่งอีเมลสำเร็จแล้วในเวลา {DateTime.Now:dd/MM/yyyy HH:mm:ss น.}</p></div>",
                    IsBodyHtml = true
                };
                mail.To.Add(new MailAddress(toEmail));

                client.Send(mail);
                statusMessage = "ส่งอีเมลทดสอบสำเร็จแล้ว";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return false;
            }
        }

        /// <summary>
        /// ส่งอีเมลอนุมัติการคืนเงินหาลูกค้าจริง พร้อมบันทึกประวัติ
        /// </summary>
        public static (bool success, string statusMessage, string emailId) SendRefundApprovalEmail(
            string contentRootPath,
            IConfiguration? config,
            string toEmail,
            string customerName,
            string bookingCode,
            string movieTitle,
            string cinemaName,
            string showtimeText,
            string seatNumbers,
            decimal refundAmount,
            string requestId,
            string baseUrl = "http://localhost:5000")
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return (false, "ไม่มีที่อยู่อีเมลผู้รับ", "");
            }

            string confirmUrl = $"{baseUrl.TrimEnd('/')}/Account/ConfirmRefund?requestId={requestId}";
            string emailId = $"EML-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            string subject = $"[ABSOLUTE CINEMA] อนุมัติการคืนเงินสำหรับรหัสจอง {bookingCode} เรียบร้อยแล้ว";
            string bodyHtml = GenerateRefundEmailHtml(baseUrl, requestId, bookingCode, customerName, movieTitle, cinemaName, showtimeText, seatNumbers, refundAmount);

            bool smtpOk = false;
            string statusMsg = "";

            var smtp = GetSmtpConfig(contentRootPath, config);
            if (!string.IsNullOrWhiteSpace(smtp.Username) && !string.IsNullOrWhiteSpace(smtp.Password))
            {
                try
                {
                    string fromEmail = !string.IsNullOrWhiteSpace(smtp.SenderEmail) ? smtp.SenderEmail : smtp.Username;
                    using var client = new SmtpClient(smtp.Server, smtp.Port)
                    {
                        Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                        EnableSsl = smtp.EnableSsl,
                        Timeout = 15000
                    };

                    using var mail = new MailMessage
                    {
                        From = new MailAddress(fromEmail, smtp.SenderName),
                        Subject = subject,
                        Body = bodyHtml,
                        IsBodyHtml = true
                    };
                    mail.To.Add(new MailAddress(toEmail, customerName));

                    client.Send(mail);
                    smtpOk = true;
                    statusMsg = $"ส่งอีเมลจริงผ่าน SMTP สำเร็จแล้ว (ไปยัง {toEmail})";
                }
                catch (Exception ex)
                {
                    statusMsg = "SMTP Error: " + (ex.InnerException?.Message ?? ex.Message);
                }
            }
            else
            {
                statusMsg = "ยังไม่ได้ระบุ SMTP Username/Password (Google App Password 16 หลัก) ในระบบ";
            }

            // บันทึกลง Data/sent_emails.json เสมอ เพื่อให้ตรวจสอบ/เปิดดูตัวอย่างได้ตลอดเวลา
            lock (_lock)
            {
                try
                {
                    var dataDir = Path.Combine(contentRootPath, "Data");
                    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                    var filePath = Path.Combine(dataDir, "sent_emails.json");

                    var list = new List<SentEmailRecord>();
                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        list = JsonSerializer.Deserialize<List<SentEmailRecord>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }

                    // เอาอันเก่าของ requestId นี้ออกเพื่ออัปเดตสถานะล่าสุด
                    list.RemoveAll(x => string.Equals(x.RequestId, requestId, StringComparison.OrdinalIgnoreCase));

                    list.Insert(0, new SentEmailRecord
                    {
                        EmailId = emailId,
                        RecipientEmail = toEmail,
                        RecipientName = customerName,
                        Subject = subject,
                        HtmlBody = bodyHtml,
                        ConfirmationUrl = confirmUrl,
                        RequestId = requestId,
                        SentAt = DateTime.Now,
                        SmtpSentSuccessfully = smtpOk,
                        StatusMessage = statusMsg
                    });

                    File.WriteAllText(filePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }
            }

            return (smtpOk, statusMsg, emailId);
        }
    }
}
