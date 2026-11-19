using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int BookingId { get; set; }

    public decimal Amount { get; set; }

    public string? TaxInvoiceNo { get; set; }

    public bool? IsRefunded { get; set; }

    public DateTime? PaymentDate { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
