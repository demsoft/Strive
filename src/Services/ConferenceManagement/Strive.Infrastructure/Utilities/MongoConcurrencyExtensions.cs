using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Infrastructure.Utilities
{
    /// <summary>
    ///     Optimistic concurrency for documents with an integer version property, replaces MongoDB.Concurrency with the
    ///     same semantics.
    /// </summary>
    public static class MongoConcurrencyExtensions
    {
        /// <summary>
        ///     Replace the document if its stored version still equals the version of <paramref name="obj" />. The
        ///     version of <paramref name="obj" /> is incremented and stays incremented only if the update succeeded.
        /// </summary>
        public static async Task<OptimisticUpdateResult> OptimisticUpdateAsync<T>(this IMongoCollection<T> collection,
            T obj, Expression<Func<T, int>> versionSelector, ReplaceOptions? options = null,
            CancellationToken token = default) where T : class
        {
            var versionProperty = (PropertyInfo) ((MemberExpression) versionSelector.Body).Member;
            var requestedVersion = (int) versionProperty.GetValue(obj)!;

            var id = obj.ToBsonDocument()["_id"];
            if (id.IsBsonNull || id.IsString && id.AsString.Length == 0)
                throw new InvalidOperationException("Optimistic updates require documents with an id.");

            var queryById = Builders<T>.Filter.Eq("_id", id);
            var queryWithVersion =
                Builders<T>.Filter.And(queryById, Builders<T>.Filter.Eq(versionSelector, requestedVersion));

            versionProperty.SetValue(obj, requestedVersion + 1);

            try
            {
                var result = await collection.ReplaceOneAsync(queryWithVersion, obj, options, token);

                if (options?.IsUpsert == true && result.UpsertedId != null) return OptimisticUpdateResult.Ok;
                if (result.ModifiedCount == 1) return OptimisticUpdateResult.Ok;
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey &&
                                                options?.IsUpsert == true)
            {
                // upsert with a different version tries to insert a document with an existing id
            }
            catch
            {
                versionProperty.SetValue(obj, requestedVersion);
                throw;
            }

            versionProperty.SetValue(obj, requestedVersion);

            var exists = await collection.Find(queryById).Limit(1).CountDocumentsAsync(token) > 0;
            return exists ? OptimisticUpdateResult.ConcurrencyException : OptimisticUpdateResult.DeletedException;
        }
    }
}
