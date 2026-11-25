using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace Kawin_67091861_002.Helpers
{
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

        public static bool SendRefundApprovalEmail(
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
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            string confirmUrl = $"{baseUrl.TrimEnd('/')}/Account/ConfirmRefund?requestId={requestId}";
            string emailId = $"EML-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            string subject = $"[ABSOLUTE CINEMA] อนุมัติการคืนเงินสำหรับรหัสจอง {bookingCode} เรียบร้อยแล้ว";

            string bodyHtml = $@"
<!DOCTYPE html>
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

            bool smtpOk = false;
            string statusMsg = "Saved to outbox";

            // ลองส่งผ่าน SMTP ถ้ามีการตั้งค่า
            try
            {
                var smtpHost = config?["SmtpSettings:Server"] ?? "smtp.gmail.com";
                var smtpPortStr = config?["SmtpSettings:Port"] ?? "587";
                int.TryParse(smtpPortStr, out int smtpPort);
                if (smtpPort <= 0) smtpPort = 587;

                var senderEmail = config?["SmtpSettings:SenderEmail"] ?? "no-reply@absolutecinema.com";
                var senderName = config?["SmtpSettings:SenderName"] ?? "Absolute Cinema Customer Service";
                var username = config?["SmtpSettings:Username"];
                var password = config?["SmtpSettings:Password"];

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    using var client = new SmtpClient(smtpHost, smtpPort)
                    {
                        Credentials = new NetworkCredential(username, password),
                        EnableSsl = true,
                        Timeout = 10000
                    };

                    using var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail, senderName),
                        Subject = subject,
                        Body = bodyHtml,
                        IsBodyHtml = true
                    };
                    mail.To.Add(new MailAddress(toEmail, customerName));

                    client.Send(mail);
                    smtpOk = true;
                    statusMsg = "Sent via SMTP successfully";
                }
                else
                {
                    statusMsg = "SMTP credentials not provided; logged to Data/sent_emails.json";
                }
            }
            catch (Exception ex)
            {
                statusMsg = "SMTP Error: " + ex.Message;
            }

            // บันทึกลง Data/sent_emails.json เสมอ
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

            return true;
        }
    }
}
