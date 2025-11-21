using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nop.Plugin.DiscountRules.ItemCount.Models;

public class ItemCountRequirementModel
{
    public int RequirementId { get; set; }
    public int DiscountId { get; set; }

    // "12,15,18" formatında ProductId listesi
    public string ProductIdsRaw { get; set; }

    // Miktar aralığı (Min 2, Max 2 => tam 2; Min 3, Max 0 => 3+)
    public int MinQuantity { get; set; }
    public int MaxQuantity { get; set; }

    public List<SelectListItem> AvailableCurrencies { get; set; } = new();
    public List<int> SelectedCurrencyIds { get; set; } = new();
}