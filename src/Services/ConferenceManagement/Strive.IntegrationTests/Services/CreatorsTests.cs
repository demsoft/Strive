using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Strive.Infrastructure.Serialization;
using Strive.IntegrationTests._Helpers;
using Strive.Models.Response;
using Xunit;
using Xunit.Abstractions;

namespace Strive.IntegrationTests.Services
{
    [Collection(IntegrationTestCollection.Definition)]
    public class CreatorsTests : IntegrationTestBase
    {
        public CreatorsTests(ITestOutputHelper testOutputHelper, MongoDbFixture mongoDb) : base(testOutputHelper,
            mongoDb)
        {
        }

        private UserAccount CreateUser() => Factory.CreateUser("user" + System.Guid.NewGuid().ToString("N")[..6], false);

        private FakePaymentProvider Provider => Factory.Services.GetRequiredService<FakePaymentProvider>();

        private HttpClient Client(UserAccount? user)
        {
            var client = Factory.CreateClient();
            if (user != null)
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, user.Token);
            return client;
        }

        private static async Task<T> Read<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(), JsonConfig.Default)!;
        }

        private static JsonNetContent Save(string name = "Ada Lovelace", string? bio = "Piano teacher",
            string? bank = "058", string? number = "0123456789") =>
            new(new SaveCreatorDto(name, bio, bank, number));

        [Fact]
        public async Task Config_TellsWhatThePageNeeds()
        {
            var config = await Read<PaymentsConfigDto>(await Client(CreateUser()).GetAsync("/v1/creators/config"));

            Assert.True(config.Enabled);
            Assert.Equal("NGN", config.Currency);
            Assert.Equal(10, config.PlatformCommissionPercent);
        }

        [Fact]
        public async Task Banks_AreListedByName()
        {
            var banks = await Read<BankDto[]>(await Client(CreateUser()).GetAsync("/v1/creators/banks"));

            Assert.Equal(new[] {"Access Bank", "Guaranty Trust Bank"}, banks.Select(x => x.Name));
        }

        [Fact]
        public async Task Banks_NotSignedIn_Unauthorized()
        {
            var response = await Client(null).GetAsync("/v1/creators/banks");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ResolveAccount_ReturnsTheNameOfTheHolder()
        {
            var response = await Client(CreateUser()).PostAsync("/v1/creators/resolve-account",
                new JsonNetContent(new ResolveAccountDto("058", "0123456789")));

            Assert.Equal("ADA LOVELACE", (await Read<ResolvedAccountDto>(response)).AccountName);
        }

        [Fact]
        public async Task ResolveAccount_UnknownAccount_BadRequest()
        {
            var response = await Client(CreateUser()).PostAsync("/v1/creators/resolve-account",
                new JsonNetContent(new ResolveAccountDto("058", FakePaymentProvider.UnknownAccount)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Payments_InvalidAccount", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Me_NoProfileYet_NotFound()
        {
            var response = await Client(CreateUser()).GetAsync("/v1/creators/me");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Save_FirstTime_CreatesTheProfileAndTheSubaccount()
        {
            var user = CreateUser();

            var profile = await Read<CreatorProfileDto>(await Client(user).PutAsync("/v1/creators/me", Save()));

            Assert.Equal("Ada Lovelace", profile.DisplayName);
            Assert.Equal("ADA LOVELACE", profile.AccountName);
            Assert.Equal("6789", profile.AccountLast4);
            Assert.Equal("Guaranty Trust Bank", profile.BankName);
            Assert.True(profile.CanSell);
            var subaccount = Provider.CreatedSubaccounts.Last();
            Assert.Equal("0123456789", subaccount.AccountNumber);
            // the platform keeps its commission of every payment
            Assert.Equal(10, subaccount.PlatformPercentage);

            // and the profile is there next time, without the full account number
            var again = await Read<CreatorProfileDto>(await Client(user).GetAsync("/v1/creators/me"));
            Assert.Equal(profile.CreatorId, again.CreatorId);
            Assert.DoesNotContain("0123456789", JsonConvert.SerializeObject(again));
        }

        [Fact]
        public async Task Save_AgainWithoutBankDetails_ChangesOnlyTheProfile()
        {
            var user = CreateUser();
            await Client(user).PutAsync("/v1/creators/me", Save());
            var subaccounts = Provider.CreatedSubaccounts.Count;

            var profile = await Read<CreatorProfileDto>(await Client(user).PutAsync("/v1/creators/me",
                Save("Ada L.", "New text", null, null)));

            Assert.Equal("Ada L.", profile.DisplayName);
            Assert.Equal("6789", profile.AccountLast4);
            Assert.Equal(subaccounts, Provider.CreatedSubaccounts.Count);
        }

        [Fact]
        public async Task Save_NewBankDetails_UpdatesTheExistingSubaccount()
        {
            var user = CreateUser();
            await Client(user).PutAsync("/v1/creators/me", Save());

            var profile = await Read<CreatorProfileDto>(await Client(user).PutAsync("/v1/creators/me",
                Save(bank: "044", number: "9876543210")));

            Assert.Equal("3210", profile.AccountLast4);
            Assert.Equal("Access Bank", profile.BankName);
            Assert.Contains(Provider.UpdatedSubaccounts, x => x.Request.AccountNumber == "9876543210");
        }

        [Fact]
        public async Task Save_UnknownAccount_BadRequestAndNothingIsStored()
        {
            var user = CreateUser();

            var response = await Client(user).PutAsync("/v1/creators/me", Save(number: FakePaymentProvider.UnknownAccount));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client(user).GetAsync("/v1/creators/me")).StatusCode);
        }

        [Fact]
        public async Task Save_FirstTimeWithoutBank_BadRequest()
        {
            var response = await Client(CreateUser()).PutAsync("/v1/creators/me", Save(bank: null, number: null));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("")]
        public async Task Save_NameTooShort_BadRequest(string name)
        {
            var response = await Client(CreateUser()).PutAsync("/v1/creators/me", Save(name));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("12345678ab")]
        public async Task Save_AccountNumberNotTenDigits_BadRequest(string number)
        {
            var response = await Client(CreateUser()).PutAsync("/v1/creators/me", Save(number: number));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Save_ProviderDown_BadRequestWithAClearCode()
        {
            Provider.Unavailable = true;
            try
            {
                var response = await Client(CreateUser()).PutAsync("/v1/creators/me", Save());

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Payments_ProviderUnavailable", await response.Content.ReadAsStringAsync());
            }
            finally
            {
                Provider.Unavailable = false;
            }
        }

        [Fact]
        public async Task Public_ShowsNameAndTextButNoBankDetails()
        {
            var user = CreateUser();
            var mine = await Read<CreatorProfileDto>(await Client(user).PutAsync("/v1/creators/me", Save()));

            var response = await Client(null).GetAsync($"/v1/creators/{mine.CreatorId}");
            var text = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Ada Lovelace", text);
            Assert.DoesNotContain("6789", text);
            Assert.DoesNotContain("Guaranty", text);
        }

        [Fact]
        public async Task Public_UnknownCreator_NotFound()
        {
            var response = await Client(null).GetAsync("/v1/creators/doesnotexist");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
