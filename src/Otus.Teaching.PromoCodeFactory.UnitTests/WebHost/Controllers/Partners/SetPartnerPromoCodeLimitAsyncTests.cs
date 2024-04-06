using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Otus.Teaching.PromoCodeFactory.Core.Abstractions.Repositories;
using Otus.Teaching.PromoCodeFactory.Core.Domain.PromoCodeManagement;
using Otus.Teaching.PromoCodeFactory.WebHost.Controllers;
using Otus.Teaching.PromoCodeFactory.WebHost.Models;
using Xunit;

namespace Otus.Teaching.PromoCodeFactory.UnitTests.WebHost.Controllers.Partners
{
    public class SetPartnerPromoCodeLimitAsyncTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<IRepository<Partner>> _partnersRepositoryMock;
        private readonly PartnersController _partnersController;

        public SetPartnerPromoCodeLimitAsyncTests()
        {
            _fixture = (Fixture) new Fixture().Customize(new AutoMoqCustomization());
            _partnersRepositoryMock = _fixture.Freeze<Mock<IRepository<Partner>>>();
            _partnersController = _fixture.Build<PartnersController>().OmitAutoProperties().Create();
        }

        public Partner CreateBasePartner()
        {
            return new Partner()
            {
                Id = Guid.Parse("7d994823-8226-4273-b063-1a95f3cc1df8"),
                Name = "Суперигрушки",
                IsActive = true,
                PartnerLimits = new List<PartnerPromoCodeLimit>()
                {
                    new PartnerPromoCodeLimit()
                    {
                        Id = Guid.Parse("e00633a5-978a-420e-a7d6-3e1dab116393"),
                        CreateDate = new DateTime(2020, 07, 9),
                        EndDate = new DateTime(2020, 10, 9),
                        Limit = 100
                    }
                }
            };
        }

        [Fact]
        public async void SetPartnerPromoCodeLimitAsync_WhenPartnerNotFound_ReturnsNotFound()
        {
            // Arrange
            var partnerId = Guid.Parse("def47943-7aaf-44a1-ae21-05aa4948b165");
            Partner partner = null;

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partnerId))
                .ReturnsAsync((Partner)partner);

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partnerId, 
                _fixture.Create<SetPartnerPromoCodeLimitRequest>());

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async void SetPartnerPromoCodeLimitAsync_WhenPartnerIsNotActive_ReturnsBadRequest()
        {
            // Arrange
            var partnerId = Guid.Parse("def47943-7aaf-44a1-ae21-05aa4948b165");
            var partner = CreateBasePartner();
            partner.IsActive = false;

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partner.Id, 
                _fixture.Create<SetPartnerPromoCodeLimitRequest>());

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>()
                .Which.Value.Should().Be("Данный партнер не активен");
        }


        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_WhenSettingNewLimit_ResetNumberIssuedPromoCodes()
        {
            // Arrange
            var partner = CreateBasePartner();            
            partner.NumberIssuedPromoCodes = 8;

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partner.Id)).ReturnsAsync(partner);
            var request = _fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .With(r => r.Limit, 10).Create();

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partner.Id, request);

            // Assert
            // Если партнеру выставляется лимит, то мы должны обнулить количество промокодов, которые партнер выдал (NumberIssuedPromoCodes)
            partner.NumberIssuedPromoCodes.Should().Be(0); 
        }

        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_WhenSettingNewLimit_ResetNumberIssuedPromoCodes_WhenNoActiveLimit()
        {
            // Arrange
            var partner = CreateBasePartner();
            partner.NumberIssuedPromoCodes = 8; // количество выданных промокодов меньше предыдущего лимита
            var previousLimit = partner.PartnerLimits.FirstOrDefault(x => !x.CancelDate.HasValue);
            previousLimit.CancelDate = DateTime.UtcNow;

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partner.Id)).ReturnsAsync(partner);
            var request = _fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .With(r => r.Limit, 10).Create();

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partner.Id, request);

            // Assert
            // Если партнеру выставляется лимит, то мы должны обнулить количество промокодов, которые партнер выдал (NumberIssuedPromoCodes)
            // но если лимит закончился, то количество не обнуляется
            partner.NumberIssuedPromoCodes.Should().Be(8);
        }

        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_WhenAddNewLimit_SetPreviousLimitCanceled()
        {
            // Arrange
            var partner = CreateBasePartner();
            var previousLimit = partner.PartnerLimits.FirstOrDefault(x => !x.CancelDate.HasValue);

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partner.Id)).ReturnsAsync(partner);
            var request = _fixture.Create<SetPartnerPromoCodeLimitRequest>();

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partner.Id, request);

            // Assert
            previousLimit.CancelDate.Should().NotBeNull();
        }

        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_WhenLimitIsZero_ReturnsBadRequest()
        {
            // Arrange
            var partner = CreateBasePartner();

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partner.Id)).ReturnsAsync(partner);
            var request = _fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .With(r => r.Limit, 0).Create();

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partner.Id, request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>()
                .Which.Value.Should().Be("Лимит должен быть больше 0");
        }

        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_WhenSettingNewLimit_SuccessfullyUpdatesDatabase()
        {
            // Arrange
            var partner = CreateBasePartner();
            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partner.Id)).ReturnsAsync(partner);
            var request = _fixture.Create<SetPartnerPromoCodeLimitRequest>();

            // Act
            await _partnersController.SetPartnerPromoCodeLimitAsync(partner.Id, request);

            // Assert
            _partnersRepositoryMock.Verify(repo => repo.UpdateAsync(It.IsAny<Partner>()), Times.Once);
        }
    }
}