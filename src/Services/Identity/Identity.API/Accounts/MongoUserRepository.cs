#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Identity.API.Accounts
{
    public class MongoUserRepository : IUserRepository
    {
        public const string UsersCollection = "Users";
        public const string LoginsCollection = "ExternalLogins";
        public const string TokensCollection = "UserTokens";

        private readonly IMongoCollection<ExternalLogin> _logins;
        private readonly IMongoCollection<UserToken> _tokens;
        private readonly IMongoCollection<StriveUser> _users;

        static MongoUserRepository()
        {
            // dates are queried against ($lt, TTL index), so they need to be real dates
            BsonClassMap.TryRegisterClassMap<StriveUser>(map =>
            {
                map.AutoMap();
                map.MapProperty(x => x.CreatedAt).SetSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
                map.MapProperty(x => x.LockoutEnd).SetSerializer(
                    new NullableSerializer<DateTimeOffset>(new DateTimeOffsetSerializer(BsonType.DateTime)));
            });
            BsonClassMap.TryRegisterClassMap<UserToken>(map =>
            {
                map.AutoMap();
                map.MapProperty(x => x.Purpose).SetSerializer(new EnumSerializer<TokenPurpose>(BsonType.String));
            });
            BsonClassMap.TryRegisterClassMap<ExternalLogin>(map => map.AutoMap());
        }

        public MongoUserRepository(IOptions<AccountsOptions> options)
        {
            var client = new MongoClient(options.Value.MongoDb.ConnectionString);
            var database = client.GetDatabase(options.Value.MongoDb.DatabaseName);
            Database = database;
            _users = database.GetCollection<StriveUser>(UsersCollection);
            _logins = database.GetCollection<ExternalLogin>(LoginsCollection);
            _tokens = database.GetCollection<UserToken>(TokensCollection);
        }

        public IMongoDatabase Database { get; }

        /// <summary>Creates the indexes. Safe to run at every start.</summary>
        public async Task EnsureIndexesAsync()
        {
            await _users.Indexes.CreateOneAsync(new CreateIndexModel<StriveUser>(
                Builders<StriveUser>.IndexKeys.Ascending(x => x.NormalizedEmail),
                new CreateIndexOptions {Unique = true, Name = "unique_email"}));
            await _logins.Indexes.CreateOneAsync(new CreateIndexModel<ExternalLogin>(
                Builders<ExternalLogin>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions {Name = "by_user"}));
            // expired tokens are removed by MongoDB
            await _tokens.Indexes.CreateOneAsync(new CreateIndexModel<UserToken>(
                Builders<UserToken>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions {ExpireAfter = TimeSpan.Zero, Name = "expire_tokens"}));
            await _tokens.Indexes.CreateOneAsync(new CreateIndexModel<UserToken>(
                Builders<UserToken>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions {Name = "by_user"}));
        }

        public async Task<StriveUser?> FindByIdAsync(string id)
        {
            return await _users.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<StriveUser?> FindByEmailAsync(string normalizedEmail)
        {
            return await _users.Find(x => x.NormalizedEmail == normalizedEmail).FirstOrDefaultAsync();
        }

        public async Task<StriveUser?> FindByLoginAsync(string provider, string key)
        {
            var login = await _logins.Find(x => x.Id == ExternalLogin.BuildId(provider, key)).FirstOrDefaultAsync();
            return login == null ? null : await FindByIdAsync(login.UserId);
        }

        public async Task<IReadOnlyList<StriveUser>> FindByIdsAsync(IEnumerable<string> ids)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0) return Array.Empty<StriveUser>();

            return await _users.Find(Builders<StriveUser>.Filter.In(x => x.Id, list)).ToListAsync();
        }

        public async Task<bool> TryInsertAsync(StriveUser user)
        {
            try
            {
                await _users.InsertOneAsync(user);
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task UpdateAsync(StriveUser user)
        {
            await _users.ReplaceOneAsync(x => x.Id == user.Id, user);
        }

        public async Task<bool> TryAddLoginAsync(string provider, string key, string userId)
        {
            try
            {
                await _logins.InsertOneAsync(new ExternalLogin {Id = ExternalLogin.BuildId(provider, key), UserId = userId});
                return true;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task SaveTokenAsync(UserToken token)
        {
            await _tokens.InsertOneAsync(token);
        }

        public async Task<UserToken?> PeekTokenAsync(string tokenHash, TokenPurpose purpose)
        {
            return await _tokens.Find(x => x.Id == tokenHash && x.Purpose == purpose && x.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();
        }

        public async Task<UserToken?> ConsumeTokenAsync(string tokenHash, TokenPurpose purpose)
        {
            // the TTL monitor runs only once a minute, so the expiry is checked here as well
            var filter = Builders<UserToken>.Filter.Where(x =>
                x.Id == tokenHash && x.Purpose == purpose && x.ExpiresAt > DateTime.UtcNow);
            return await _tokens.FindOneAndDeleteAsync(filter);
        }

        public async Task DeleteTokensAsync(string userId, TokenPurpose purpose)
        {
            await _tokens.DeleteManyAsync(x => x.UserId == userId && x.Purpose == purpose);
        }
    }
}
