using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Strive.Infrastructure.Data;

namespace Strive.Admin
{
    public interface IAdminMetricsStore
    {
        Task AddAsync(AdminSample sample);
        Task<IReadOnlyList<AdminSample>> GetAsync(DateTime since, int maxPoints);
    }

    /// <summary>The numbers of the last days, one point per minute, in MongoDB. MongoDB removes old points itself.</summary>
    public class MongoAdminMetricsStore : MongoRepo<AdminSample>, IAdminMetricsStore, IMongoIndexBuilder
    {
        private readonly AdminOptions _admin;

        public MongoAdminMetricsStore(IOptions<MongoDbOptions> options, IOptions<AdminOptions> admin) : base(options)
        {
            _admin = admin.Value;
        }

        public async Task CreateIndexes()
        {
            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<AdminSample>(
                Builders<AdminSample>.IndexKeys.Ascending(x => x.T),
                new CreateIndexOptions {ExpireAfter = TimeSpan.FromDays(Math.Max(1, _admin.HistoryDays)), Name = "expire_samples"}));
        }

        public async Task AddAsync(AdminSample sample)
        {
            await Collection.InsertOneAsync(sample);
        }

        public async Task<IReadOnlyList<AdminSample>> GetAsync(DateTime since, int maxPoints)
        {
            var all = await Collection.Find(x => x.T >= since).SortBy(x => x.T).ToListAsync();
            return Thin(all, maxPoints);
        }

        /// <summary>Keeps at most maxPoints points, evenly spread: a chart does not need more.</summary>
        public static IReadOnlyList<AdminSample> Thin(IReadOnlyList<AdminSample> samples, int maxPoints)
        {
            if (maxPoints <= 0 || samples.Count <= maxPoints) return samples;

            var result = new List<AdminSample>(maxPoints);
            var step = (double) samples.Count / maxPoints;
            for (var i = 0; i < maxPoints; i++) result.Add(samples[(int) Math.Floor(i * step)]);
            return result;
        }
    }
}
