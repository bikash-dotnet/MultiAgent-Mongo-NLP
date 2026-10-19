using Gateway.Audit;
using Gateway.Configuration;
using Gateway.Conversations;
using Gateway.Mcp;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;
using Gateway.Rules;
using Gateway.Schemas;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Gateway.Persistence;

internal static class MongoBsonConfiguration
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            var conventions = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true)
            };
            ConventionRegistry.Register("Gateway", conventions, _ => true);

            BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.String));

            RegisterDocument<BusinessRule>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<ColumnDefinition>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<SchemaDefinition>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<SettingsDocument>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<McpServerDefinition>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<AuditLogDocument>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<ConversationState>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<AccessRequest>(map => map.MapIdMember(document => document.Id));
            RegisterDocument<AgentState>(map => map.MapIdMember(document => document.SessionId));
            RegisterDocument<ActivationLock>(map => map.MapIdMember(document => document.Id));

            _registered = true;
        }
    }

    private static void RegisterDocument<T>(Action<BsonClassMap<T>> mapId)
    {
        BsonClassMap.RegisterClassMap<T>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
            mapId(map);
        });
    }
}
