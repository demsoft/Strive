using System;
using System.Linq;
using System.Threading.Tasks;
using Identity.API.Accounts;
using Microsoft.Extensions.Options;
using Mongo2Go;
using Xunit;

namespace Identity.API.Tests
{
    public class MongoFixture : IDisposable
    {
        public MongoFixture()
        {
            Runner = MongoDbRunner.Start();
        }

        public MongoDbRunner Runner { get; }

        public void Dispose() => Runner.Dispose();

        public MongoUserRepository CreateRepository()
        {
            var options = new AccountsOptions
            {
                MongoDb = {ConnectionString = Runner.ConnectionString, DatabaseName = "t" + Guid.NewGuid().ToString("N")},
            };
            var repo = new MongoUserRepository(Options.Create(options));
            repo.EnsureIndexesAsync().GetAwaiter().GetResult();
            return repo;
        }
    }

    public class MongoUserRepositoryTests : IClassFixture<MongoFixture>
    {
        private readonly MongoUserRepository _repo;

        public MongoUserRepositoryTests(MongoFixture fixture)
        {
            _repo = fixture.CreateRepository();
        }

        private static StriveUser NewUser(string email, string? id = null) => new()
        {
            Id = id ?? Guid.NewGuid().ToString("N")[..24],
            Email = email,
            NormalizedEmail = AccountService.NormalizeEmail(email),
            DisplayName = "Name",
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        };

        [Fact]
        public async Task A_user_round_trips()
        {
            var user = NewUser("ann@example.com");
            user.LockoutEnd = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
            user.PasswordHash = "hash";
            user.AccessFailedCount = 3;
            Assert.True(await _repo.TryInsertAsync(user));

            var loaded = (await _repo.FindByEmailAsync("ANN@EXAMPLE.COM"))!;

            Assert.Equal(user.Id, loaded.Id);
            Assert.Equal(user.CreatedAt, loaded.CreatedAt);
            Assert.Equal(user.LockoutEnd, loaded.LockoutEnd);
            Assert.Equal("hash", loaded.PasswordHash);
            Assert.Equal(3, loaded.AccessFailedCount);
            Assert.Equal(user.Id, (await _repo.FindByIdAsync(user.Id))!.Id);
        }

        [Fact]
        public async Task The_email_address_is_unique_regardless_of_case()
        {
            Assert.True(await _repo.TryInsertAsync(NewUser("dup@example.com")));

            Assert.False(await _repo.TryInsertAsync(NewUser("DUP@example.com")));
        }

        [Fact]
        public async Task An_external_account_belongs_to_one_user()
        {
            var a = NewUser("a1@example.com");
            var b = NewUser("b1@example.com");
            await _repo.TryInsertAsync(a);
            await _repo.TryInsertAsync(b);

            Assert.True(await _repo.TryAddLoginAsync("Google", "42", a.Id));
            Assert.False(await _repo.TryAddLoginAsync("google", "42", b.Id));

            Assert.Equal(a.Id, (await _repo.FindByLoginAsync("Google", "42"))!.Id);
            Assert.Null(await _repo.FindByLoginAsync("Google", "43"));
        }

        [Fact]
        public async Task Users_are_found_by_their_ids()
        {
            var a = NewUser("a2@example.com");
            var b = NewUser("b2@example.com");
            await _repo.TryInsertAsync(a);
            await _repo.TryInsertAsync(b);

            var found = await _repo.FindByIdsAsync(new[] {a.Id, b.Id, a.Id, "unknown"});

            Assert.Equal(new[] {a.Id, b.Id}.OrderBy(x => x), found.Select(x => x.Id).OrderBy(x => x));
        }

        [Fact]
        public async Task A_token_can_be_consumed_only_once_and_only_for_its_purpose()
        {
            await _repo.SaveTokenAsync(new UserToken
            {
                Id = "t1", UserId = "u", Purpose = TokenPurpose.ResetPassword, ExpiresAt = DateTime.UtcNow.AddHours(1),
            });

            Assert.Null(await _repo.ConsumeTokenAsync("t1", TokenPurpose.ConfirmEmail));
            Assert.NotNull(await _repo.PeekTokenAsync("t1", TokenPurpose.ResetPassword));
            Assert.NotNull(await _repo.ConsumeTokenAsync("t1", TokenPurpose.ResetPassword));
            Assert.Null(await _repo.ConsumeTokenAsync("t1", TokenPurpose.ResetPassword));
        }

        [Fact]
        public async Task An_expired_token_cannot_be_used_even_before_mongodb_removes_it()
        {
            await _repo.SaveTokenAsync(new UserToken
            {
                Id = "t2", UserId = "u", Purpose = TokenPurpose.ConfirmEmail, ExpiresAt = DateTime.UtcNow.AddSeconds(-5),
            });

            Assert.Null(await _repo.PeekTokenAsync("t2", TokenPurpose.ConfirmEmail));
            Assert.Null(await _repo.ConsumeTokenAsync("t2", TokenPurpose.ConfirmEmail));
        }

        [Fact]
        public async Task Tokens_of_a_user_can_be_deleted_by_purpose()
        {
            await _repo.SaveTokenAsync(new UserToken
            {
                Id = "t3", UserId = "u3", Purpose = TokenPurpose.ConfirmEmail, ExpiresAt = DateTime.UtcNow.AddHours(1),
            });
            await _repo.SaveTokenAsync(new UserToken
            {
                Id = "t4", UserId = "u3", Purpose = TokenPurpose.ResetPassword, ExpiresAt = DateTime.UtcNow.AddHours(1),
            });

            await _repo.DeleteTokensAsync("u3", TokenPurpose.ConfirmEmail);

            Assert.Null(await _repo.PeekTokenAsync("t3", TokenPurpose.ConfirmEmail));
            Assert.NotNull(await _repo.PeekTokenAsync("t4", TokenPurpose.ResetPassword));
        }

        [Fact]
        public async Task The_service_works_on_the_real_store()
        {
            var email = new RecordingEmailSender();
            var service = new AccountService(_repo, email,
                Options.Create(new AccountsOptions {Mode = IdentityMode.Accounts}),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);

            await service.RegisterAsync("real@example.com", "Real", "a long enough password",
                t => $"x?token={t}", () => "l", () => "f");
            var token = System.Text.RegularExpressions.Regex.Match(email.Last.TextBody, @"token=([\w-]+)").Groups[1].Value;
            await service.ConfirmEmailAsync(token);

            var outcome = await service.PasswordSignInAsync("real@example.com", "a long enough password");
            Assert.Equal(SignInStatus.Succeeded, outcome.Status);
        }
    }
}
