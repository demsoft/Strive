using System;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Strive.Core.Interfaces.Gateways;
using Strive.Infrastructure.Utilities;
using Xunit;

namespace Strive.IntegrationTests.Infrastructure
{
    public class MongoConcurrencyExtensionsTests : IClassFixture<MongoDbFixture>
    {
        private readonly IMongoCollection<VersionedDocument> _collection;

        public MongoConcurrencyExtensionsTests(MongoDbFixture fixture)
        {
            var database = new MongoClient(fixture.Runner.ConnectionString).GetDatabase("ConcurrencyTests");
            _collection = database.GetCollection<VersionedDocument>(Guid.NewGuid().ToString("N"));
        }

        public class VersionedDocument
        {
            [BsonId]
            public string Id { get; set; } = Guid.NewGuid().ToString("N");

            public string Value { get; set; } = "initial";
            public int Version { get; set; }
        }

        private async Task<VersionedDocument> Insert()
        {
            var document = new VersionedDocument();
            await _collection.InsertOneAsync(document);
            return document;
        }

        private Task<VersionedDocument> Load(string id)
        {
            return _collection.Find(x => x.Id == id).SingleAsync();
        }

        [Fact]
        public async Task Update_CurrentVersion_ReplaceAndIncrementVersion()
        {
            var document = await Insert();
            document.Value = "updated";

            var result = await _collection.OptimisticUpdateAsync(document, x => x.Version);

            Assert.Equal(OptimisticUpdateResult.Ok, result);
            Assert.Equal(1, document.Version);

            var stored = await Load(document.Id);
            Assert.Equal("updated", stored.Value);
            Assert.Equal(1, stored.Version);
        }

        [Fact]
        public async Task Update_StaleVersion_ReturnConcurrencyExceptionAndRestoreVersion()
        {
            var document = await Insert();
            var otherCopy = await Load(document.Id);
            await _collection.OptimisticUpdateAsync(otherCopy, x => x.Version);

            document.Value = "lost update";
            var result = await _collection.OptimisticUpdateAsync(document, x => x.Version);

            Assert.Equal(OptimisticUpdateResult.ConcurrencyException, result);
            Assert.Equal(0, document.Version);
            Assert.Equal("initial", (await Load(document.Id)).Value);
        }

        [Fact]
        public async Task Update_Deleted_ReturnDeletedException()
        {
            var document = await Insert();
            await _collection.DeleteOneAsync(x => x.Id == document.Id);

            var result = await _collection.OptimisticUpdateAsync(document, x => x.Version);

            Assert.Equal(OptimisticUpdateResult.DeletedException, result);
            Assert.Equal(0, document.Version);
        }

        [Fact]
        public async Task Upsert_NewDocument_Insert()
        {
            var document = new VersionedDocument {Value = "new"};

            var result = await _collection.OptimisticUpdateAsync(document, x => x.Version,
                new ReplaceOptions {IsUpsert = true});

            Assert.Equal(OptimisticUpdateResult.Ok, result);
            var stored = await Load(document.Id);
            Assert.Equal("new", stored.Value);
            Assert.Equal(1, stored.Version);
        }

        [Fact]
        public async Task Upsert_StaleVersion_ReturnConcurrencyException()
        {
            var document = await Insert();
            var otherCopy = await Load(document.Id);
            await _collection.OptimisticUpdateAsync(otherCopy, x => x.Version);

            var result = await _collection.OptimisticUpdateAsync(document, x => x.Version,
                new ReplaceOptions {IsUpsert = true});

            Assert.Equal(OptimisticUpdateResult.ConcurrencyException, result);
            Assert.Equal(0, document.Version);
            Assert.Equal(1, (await Load(document.Id)).Version);
        }
    }
}
