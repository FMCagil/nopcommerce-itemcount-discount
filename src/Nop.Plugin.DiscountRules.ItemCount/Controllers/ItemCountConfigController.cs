using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Discounts;
using Nop.Plugin.DiscountRules.ItemCount.Models;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.DiscountRules.ItemCount.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class ItemCountConfigController : BasePluginController
    {
        private readonly IDiscountService _discountService;
        private readonly ISettingService _settingService;
        private readonly IPermissionService _permissionService;
        private readonly ICurrencyService _currencyService;

        public ItemCountConfigController(
            IDiscountService discountService,
            ISettingService settingService,
            IPermissionService permissionService,
            ICurrencyService currencyService)
        {
            _discountService = discountService;
            _settingService = settingService;
            _permissionService = permissionService;
            _currencyService = currencyService;
        }

        public async Task<IActionResult> Configure(int discountId, int? discountRequirementId)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageDiscounts))
                return Content("Access denied");

            var discount = await _discountService.GetDiscountByIdAsync(discountId);
            if (discount == null)
                throw new ArgumentException("Discount could not be loaded");

            //check whether the discount requirement exists
            if (discountRequirementId.HasValue && await _discountService.GetDiscountRequirementByIdAsync(discountRequirementId.Value) is null)
                return Content("Failed to load requirement.");

        
            var productIdsRaw = await _settingService
                .GetSettingByKeyAsync<string>(DiscountRequirementDefaults.ProductIdsKey(discountRequirementId.GetValueOrDefault()));
            var minQty = await _settingService
                .GetSettingByKeyAsync<int>(DiscountRequirementDefaults.MinQtyKey(discountRequirementId.GetValueOrDefault()));
            var maxQty = await _settingService
                .GetSettingByKeyAsync<int>(DiscountRequirementDefaults.MaxQtyKey(discountRequirementId.GetValueOrDefault()));
            var currencyIdsRaw = await _settingService
                .GetSettingByKeyAsync<string>(DiscountRequirementDefaults.CurrencyIdsKey(discountRequirementId.GetValueOrDefault()));

            var model = new ItemCountRequirementModel
            {
                RequirementId = discountRequirementId ?? 0,
                DiscountId = discountId,
                ProductIdsRaw = productIdsRaw,
                MinQuantity = minQty,
                MaxQuantity = maxQty,
                AvailableCurrencies = (await _currencyService.GetAllCurrenciesAsync())
                    .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() })
                    .ToList(),
                SelectedCurrencyIds = !string.IsNullOrWhiteSpace(currencyIdsRaw)
                    ? currencyIdsRaw.Split(',').Select(int.Parse).ToList()
                    : new List<int>()
            };

            ViewData.TemplateInfo.HtmlFieldPrefix =
                $"DiscountRequirement.ItemCount{discountRequirementId?.ToString() ?? "0"}";

            return View("~/Plugins/DiscountRules.ItemCount/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ItemCountRequirementModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageDiscounts))
                return Content("Access denied");

            if (!ModelState.IsValid)
                return Ok(new { Errors = GetErrorsFromModelState(ModelState) });

            var discount = await _discountService.GetDiscountByIdAsync(model.DiscountId);
            if (discount == null)
                return NotFound(new { Errors = new[] { "Discount could not be loaded" } });

            var discountRequirement = await _discountService.GetDiscountRequirementByIdAsync(model.RequirementId);

            if (discountRequirement != null)
            {
                // Save requirement-level settings
                await _settingService.SetSettingAsync(
                    $"DiscountRequirement.ItemCount.ProductIds-{discountRequirement.Id}",
                    model.ProductIdsRaw ?? "");
                await _settingService.SetSettingAsync($"DiscountRequirement.ItemCount.MinQty-{discountRequirement.Id}",
                    model.MinQuantity);
                await _settingService.SetSettingAsync($"DiscountRequirement.ItemCount.MaxQty-{discountRequirement.Id}",
                    model.MaxQuantity);
                await _settingService.SetSettingAsync(
                    $"DiscountRequirement.ItemCount.CurrencyIds-{discountRequirement.Id}",
                    string.Join(",", model.SelectedCurrencyIds ?? new List<int>())
                );
            }
            else
            {
                if (discountRequirement == null)
                {
                    discountRequirement = new DiscountRequirement
                    {
                        DiscountId = discount.Id,
                        DiscountRequirementRuleSystemName = DiscountRequirementDefaults.SystemName
                    };
                    await _discountService.InsertDiscountRequirementAsync(discountRequirement);
                }

                // Save requirement-level settings
                await _settingService.SetSettingAsync($"DiscountRequirement.ItemCount.ProductIds-{discountRequirement.Id}",
                    model.ProductIdsRaw ?? "");
                await _settingService.SetSettingAsync($"DiscountRequirement.ItemCount.MinQty-{discountRequirement.Id}",
                    model.MinQuantity);
                await _settingService.SetSettingAsync($"DiscountRequirement.ItemCount.MaxQty-{discountRequirement.Id}",
                    model.MaxQuantity);
                await _settingService.SetSettingAsync(
                    $"DiscountRequirement.ItemCount.CurrencyIds-{discountRequirement.Id}",
                    string.Join(",", model.SelectedCurrencyIds ?? new List<int>())
                );
            }

            return Ok(new { NewRequirementId = discountRequirement.Id });
        }
        
        #region Utilities

        private IEnumerable<string> GetErrorsFromModelState(ModelStateDictionary modelState)
        {
            return ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
        }

        #endregion
    }
}