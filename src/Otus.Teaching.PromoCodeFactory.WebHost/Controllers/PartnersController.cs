using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Otus.Teaching.PromoCodeFactory.Core.Abstractions.Repositories;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;

namespace Otus.Teaching.PromoCodeFactory.WebHost.Controllers
{
    /// <summary>
    /// Партнеры
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    public class PartnersController : ControllerBase
    {
        private readonly IRepository<Partner> _partnersRepository;

        public PartnersController(IRepository<Partner> partnersRepository)
        {
            _partnersRepository = partnersRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PartnerResponse>>> GetPartnersAsync()
        {
            var partners = await _partnersRepository.GetAllAsync();
            return Ok(partners.Select(MapToPartnerResponse).AsEnumerable());
        }

        [HttpGet("{id}/limits/{limitId}")]
        public async Task<ActionResult<PartnerPromoCodeLimit>> GetPartnerLimitAsync(Guid id, Guid limitId)
        {
            var partner = await _partnersRepository.GetByIdAsync(id);
            if (partner == null) return NotFound();

            var limit = partner.PartnerLimits.FirstOrDefault(x => x.Id == limitId);
            if (limit == null) return NotFound();

            return Ok(MapToPartnerPromoCodeLimitResponse(limit));
        }

        [HttpPost("{id}/limits")]
        public async Task<IActionResult> SetPartnerPromoCodeLimitAsync(Guid id, SetPartnerPromoCodeLimitRequest request)
        {
            var partner = await GetPartnerByIdAsync(id);
            if (partner == null) return NotFound();

            if (!partner.IsActive) return BadRequest("Данный партнер не активен");

            var activeLimit = partner.PartnerLimits.FirstOrDefault(x => !x.CancelDate.HasValue);
            if (activeLimit != null)
            {
                partner.NumberIssuedPromoCodes = 0;
                activeLimit.CancelDate = DateTime.Now;
            }

            if (request.Limit <= 0) return BadRequest("Лимит должен быть больше 0");

            var newLimit = new PartnerPromoCodeLimit
            {
                Limit = request.Limit,
                PartnerId = partner.Id,
                CreateDate = DateTime.Now,
                EndDate = request.EndDate
            };
            partner.PartnerLimits.Add(newLimit);
            await _partnersRepository.UpdateAsync(partner);

            return CreatedAtAction(nameof(GetPartnerLimitAsync), new { id = partner.Id, limitId = newLimit.Id }, MapToPartnerPromoCodeLimitResponse(newLimit));
        }

        [HttpPost("{id}/canceledLimits")]
        public async Task<IActionResult> CancelPartnerPromoCodeLimitAsync(Guid id)
        {
            var partner = await GetPartnerByIdAsync(id);
            if (partner == null) return NotFound();

            if (!partner.IsActive) return BadRequest("Данный партнер не активен");

            var activeLimit = partner.PartnerLimits.FirstOrDefault(x => !x.CancelDate.HasValue);
            if (activeLimit != null) activeLimit.CancelDate = DateTime.Now;

            await _partnersRepository.UpdateAsync(partner);

            return NoContent();
        }

        private async Task<Partner> GetPartnerByIdAsync(Guid id)
        {
            return await _partnersRepository.GetByIdAsync(id);
        }

        private static PartnerResponse MapToPartnerResponse(Partner partner)
        {
            return new PartnerResponse
            {
                Id = partner.Id,
                Name = partner.Name,
                NumberIssuedPromoCodes = partner.NumberIssuedPromoCodes,
                IsActive = true,
                PartnerLimits = partner.PartnerLimits.Select(MapToPartnerPromoCodeLimitResponse).ToList()
            };
        }

        private static PartnerPromoCodeLimitResponse MapToPartnerPromoCodeLimitResponse(PartnerPromoCodeLimit limit)
        {
            return new PartnerPromoCodeLimitResponse
            {
                Id = limit.Id,
                PartnerId = limit.PartnerId,
                Limit = limit.Limit,
                CreateDate = FormatDate(limit.CreateDate),
                EndDate = FormatDate(limit.EndDate),
                CancelDate = FormatDate(limit.CancelDate)
            };
        }

        private static string FormatDate(DateTime? date)
        {
            return date?.ToString("dd.MM.yyyy hh:mm:ss");
        }
    }
}