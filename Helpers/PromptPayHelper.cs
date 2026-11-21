using System;
using System.Text;

namespace Kawin_67091861_002.Helpers;

public static class PromptPayHelper
{
    public const string TARGET_PHONE = "0933632239";
    public const string TARGET_PHONE_DISPLAY = "093-363-2239";
    public const string ACCOUNT_NAME = "บจก. แอบโซลูท ซินีม่า (ประเทศไทย) / คุณกวิน";

    public static string GeneratePromptPayPayload(string phone, decimal amount)
    {
        string cleanPhone = phone.Replace("-", "").Trim();
        string formattedPhone = cleanPhone.StartsWith("0") ? "0066" + cleanPhone.Substring(1) : cleanPhone;

        string aid = "0016A000000677010111";
        string mobileTag = "01" + formattedPhone.Length.ToString("D2") + formattedPhone;
        string tag29Value = aid + mobileTag;
        string tag29 = "29" + tag29Value.Length.ToString("D2") + tag29Value;

        string amtStr = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        string tag54 = "54" + amtStr.Length.ToString("D2") + amtStr;

        var sb = new StringBuilder();
        sb.Append("000201");     // Payload Format Indicator
        sb.Append("010212");     // Point of Initiation Method: Dynamic QR
        sb.Append(tag29);        // Merchant Account Info
        sb.Append("5303764");    // Currency: THB (764)
        sb.Append(tag54);        // Amount
        sb.Append("5802TH");     // Country: TH
        sb.Append("6304");       // CRC placeholder

        string raw = sb.ToString();
        string crc = ComputeCrc16(raw);
        return raw + crc;
    }

    private static string ComputeCrc16(string input)
    {
        ushort crc = 0xFFFF;
        byte[] bytes = Encoding.ASCII.GetBytes(input);
        foreach (byte b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc <<= 1;
            }
        }
        return crc.ToString("X4");
    }
}
