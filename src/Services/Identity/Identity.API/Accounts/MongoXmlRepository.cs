#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Identity.API.Accounts
{
    /// <summary>
    ///     Keeps the data protection keys in MongoDB. They protect the session cookies, the anti-forgery tokens and
    ///     the sign in state of Google, which must survive a restart and must be shared by all instances.
    /// </summary>
    public class MongoXmlRepository : IXmlRepository
    {
        private readonly IMongoCollection<KeyDocument> _keys;

        public MongoXmlRepository(IMongoDatabase database)
        {
            _keys = database.GetCollection<KeyDocument>("DataProtectionKeys");
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            return _keys.Find(FilterDefinition<KeyDocument>.Empty).ToList().Select(x => XElement.Parse(x.Xml)).ToList();
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            _keys.InsertOne(new KeyDocument {Id = friendlyName, Xml = element.ToString(SaveOptions.DisableFormatting)});
        }

        private class KeyDocument
        {
            [BsonId]
            public string Id { get; set; } = null!;

            public string Xml { get; set; } = null!;
        }
    }
}
