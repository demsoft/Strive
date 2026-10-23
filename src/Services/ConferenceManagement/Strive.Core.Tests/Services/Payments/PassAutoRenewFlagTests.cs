using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Payments.Requests;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Payments.UseCases;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class PassAutoRenewFlagTests
    {
        private static CreatorProfile Creator() =>
            new("c1", "host", "Ada", DateTimeOffset.UtcNow)
            {
                ProviderSubaccountCode = "ACCT_1",
                PassPriceMinor = 2_000_000,
            };

        private static (StartPassCheckoutUseCase useCase, Mock<IOrderRepo> orders, Mock<IPaymentProvider> provider)
            Build(bool autoRenewEnabled)
        {
            var creators = new Mock<ICreatorRepo>();
            creators.Setup(x => x.FindById("c1")).ReturnsAsync(Creator());
            var orders = new Mock<IOrderRepo>();
            var provider = new Mock<IPaymentProvider>();
            provider.Setup(x => x.InitializeCheckout(It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSession("https://pay.test/x", "code"));
            var options = Options.Create(new PaymentsOptions {Enabled = true, AutoRenewEnabled = autoRenewEnabled});
            return (new StartPassCheckoutUseCase(provider.Object, creators.Object, orders.Object, options,
                TimeProvider.System, NullLogger<StartPassCheckoutUseCase>.Instance), orders, provider);
        }

        [Fact]
        public async Task Checkout_AutoRenewWhileSwitchedOff_IsRefusedAndNothingIsStarted()
        {
            var (useCase, orders, provider) = Build(autoRenewEnabled: false);

            var result = await useCase.Handle(
                new StartPassCheckoutRequest("fan", "c1", "fan@example.com", "https://x", AutoRenew: true),
                CancellationToken.None);

            Assert.False(result.Success);
            orders.Verify(x => x.Create(It.IsAny<TicketOrder>()), Times.Never);
            provider.Verify(x => x.InitializeCheckout(It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Checkout_WithoutAutoRenewWhileSwitchedOff_WorksAsBefore()
        {
            var (useCase, _, _) = Build(autoRenewEnabled: false);

            var result = await useCase.Handle(
                new StartPassCheckoutRequest("fan", "c1", "fan@example.com", "https://x"), CancellationToken.None);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task Checkout_AutoRenewSwitchedOn_RemembersTheChoiceOnTheOrder()
        {
            var (useCase, orders, _) = Build(autoRenewEnabled: true);
            TicketOrder? created = null;
            orders.Setup(x => x.Create(It.IsAny<TicketOrder>())).Callback<TicketOrder>(o => created = o)
                .Returns(Task.CompletedTask);

            var result = await useCase.Handle(
                new StartPassCheckoutRequest("fan", "c1", "fan@example.com", "https://x", AutoRenew: true),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(created!.AutoRenew);
            Assert.False(created.IsRenewal);
        }
    }
}
