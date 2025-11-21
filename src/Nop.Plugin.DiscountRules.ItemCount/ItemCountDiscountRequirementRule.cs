using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Orders;
using Nop.Services.Plugins;

namespace Nop.Plugin.DiscountRules.ItemCount
{
    public class ItemCountDiscountRequirementRule : BasePlugin, IDiscountRequirementRule
    {
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IWorkContext _workContext;
        private readonly ICurrencyService _currencyService;

        public ItemCountDiscountRequirementRule(
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
            IWorkContext workContext,
            ICurrencyService currencyService)
        {
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _workContext = workContext;
            _currencyService = currencyService;
        }

        // Nop bununla requirement tipini tanıyor
        public string SystemName => DiscountRequirementDefaults.SystemName;

        public string FriendlyName => "Item count discount requirement";

        public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
        {
            var result = new DiscountRequirementValidationResult();

            if (request == null || request.Customer == null)
                return result;

            // requirement id'yi alalım
            // NOT: Sendeki Nop sürümüne göre property adı `DiscountRequirementId` veya `DiscountRequirement` olabilir.
            // En sık kullanılan pattern: request.DiscountRequirementId
            var requirementId = request.DiscountRequirementId;
            if (requirementId <= 0)
                return result;

            // Ayarları çek
            var productIdsRaw = await _settingService.GetSettingByKeyAsync<string>(
                DiscountRequirementDefaults.ProductIdsKey(requirementId));

            var minQty = await _settingService.GetSettingByKeyAsync<int>(
                DiscountRequirementDefaults.MinQtyKey(requirementId));

            var maxQty = await _settingService.GetSettingByKeyAsync<int>(
                DiscountRequirementDefaults.MaxQtyKey(requirementId));

            var currencyIdsRaw = await _settingService.GetSettingByKeyAsync<string>(
                DiscountRequirementDefaults.CurrencyIdsKey(requirementId));

            // Para birimi filtresi
            var workingCurrency = await _workContext.GetWorkingCurrencyAsync();

            List<int> allowedCurrencyIds = new();
            if (!string.IsNullOrWhiteSpace(currencyIdsRaw))
            {
                allowedCurrencyIds = currencyIdsRaw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => int.TryParse(x, out _))
                    .Select(int.Parse)
                    .ToList();
            }

            if (allowedCurrencyIds.Any() && !allowedCurrencyIds.Contains(workingCurrency.Id))
                return result;

            // Sepet
            var cart = await _shoppingCartService.GetShoppingCartAsync(
                request.Customer,
                ShoppingCartType.ShoppingCart,
                request.Store?.Id ?? 0);

            if (!cart.Any())
                return result;

            // Ürün filtresi (boşsa tüm ürünler)
            List<int> productIds = new();
            if (!string.IsNullOrWhiteSpace(productIdsRaw))
            {
                productIds = productIdsRaw
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => int.TryParse(x, out _))
                    .Select(int.Parse)
                    .ToList();
            }

            List<ShoppingCartItem> eligibleItems;
            if (productIds.Any())
                eligibleItems = cart.Where(ci => productIds.Contains(ci.ProductId)).ToList();
            else
                eligibleItems = cart.ToList();

            if (!eligibleItems.Any())
                return result;

            var totalQty = eligibleItems.Sum(ci => ci.Quantity);

            // Min / Max qty koşulu:
            // Min <= 0 ise -> alt sınır yok
            // Max <= 0 ise -> üst sınır yok
            var okMin = minQty <= 0 || totalQty >= minQty;
            var okMax = maxQty <= 0 || totalQty <= maxQty;

            if (okMin && okMax)
                result.IsValid = true;

            return result;
        }

        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            return $"/Admin/ItemCountConfig/Configure?discountId={discountId}&discountRequirementId={discountRequirementId}";
        }

        public override Task InstallAsync()
        {
            // Requirement bazlı ayar kullandığımız için global setting kaydetmeye gerek yok.
            return base.InstallAsync();
        }

        public override Task UninstallAsync()
        {
            // İstersen burada tüm DiscountRequirement.ItemCount.* setting'lerini temizleyebilirsin.
            return base.UninstallAsync();
        }
    }
}