namespace ByteBill_BS.Models.Enums;

public enum DiscountType
{
    /// <summary>RA 9994 — 20% discount, VAT-exempt on discounted amount.</summary>
    SeniorCitizen,

    /// <summary>RA 10754 — 20% discount, VAT-exempt on discounted amount.</summary>
    PWD,

    /// <summary>Admin-defined percentage or fixed-amount discount.</summary>
    Promo
}
