namespace Nop.Plugin.DiscountRules.ItemCount;

public class DiscountRequirementDefaults
{
    public const string SystemName = "DiscountRequirement.ItemCount";

    public static string ProductIdsKey(int requirementId) =>
        $"DiscountRequirement.ItemCount.ProductIds-{requirementId}";

    public static string MinQtyKey(int requirementId) =>
        $"DiscountRequirement.ItemCount.MinQty-{requirementId}";

    public static string MaxQtyKey(int requirementId) =>
        $"DiscountRequirement.ItemCount.MaxQty-{requirementId}";

    public static string CurrencyIdsKey(int requirementId) =>
        $"DiscountRequirement.ItemCount.CurrencyIds-{requirementId}";
}