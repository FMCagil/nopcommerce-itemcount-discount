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
    public class ItemCountRequirement : BasePlugin, IDiscountRequirementRule
    {
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IWorkContext _workContext;
        private readonly ICurrencyService _currencyService;

        public ItemCountRequirement(
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

        // Nop plugin sisteminin görmek istediği kimlik
        public string SystemName => DiscountRequirementDefaults.SystemName;

        public string FriendlyName => "Item count discount requirement";

        /// <summary>
        /// İndirim şartı sağlanıyor mu?
        /// - Requirement bazlı ayarlar (ürün Id, min/max qty, para birimi)
        /// - Seçilen para birimi kontrolü
        /// - Sepetteki uygun ürünlerden min/max adet kontrolü
        /// </summary>
        public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
        {
            var result = new DiscountRequirementValidationResult();

            if (request == null || request.Customer == null)
                return result;

            // Requirement id (NOP 4.5'te DiscountRequirement üzerinden geliyor)
            var requirementId = request.DiscountRequirementId;
            if (requirementId <= 0)
                return result;

            // Requirement'a özel ayarları oku
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

            var allowedCurrencyIds = new List<int>();
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

            // Ürün filtresi (boşsa tüm ürünler geçerli)
            var productIds = new List<int>();
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

            // Min / Max qty kuralı:
            // Min <= 0 → alt sınır yok
            // Max <= 0 → üst sınır yok
            var okMin = minQty <= 0 || totalQty >= minQty;
            var okMax = maxQty <= 0 || totalQty <= maxQty;

            if (okMin && okMax)
                result.IsValid = true;

            return result;
        }

        /// <summary>
        /// Admin tarafında bu requirement'ın konfigürasyon URL'i.
        /// Discount ekranındaki "Configure" linki buraya gider.
        /// </summary>
        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            return $"/Admin/ItemCountConfig/Configure?discountId={discountId}&discountRequirementId={discountRequirementId}";
        }

        #region BasePlugin overrides

        public override async Task InstallAsync()
        {
            // Requirement bazlı settings kullandığımız için
            // global bir ayar set etmeye gerek yok.
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            // İstersen burada DiscountRequirement.ItemCount.* settings'lerini temizleyebilirsin.
            await base.UninstallAsync();
        }

        #endregion
    }
}