using Dxs.Consigliere.Data.Models;

using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.Client.Documents.Session;

using SessionOptions = Raven.Client.Documents.Session.SessionOptions;

namespace Dxs.Consigliere.Extensions;

public enum OperationResult
{
    Created,
    Updated,
    NotModified
}

public static class DocumentStoreExtensions
{
    public static IAsyncDocumentSession GetSession(
        this IDocumentStore store,
        bool noCache = false,
        bool noTracking = false,
        TransactionMode transactionMode = TransactionMode.SingleNode
    )
        => store.OpenAsyncSession(
            new SessionOptions
            {
                NoCaching = noCache,
                NoTracking = noTracking,
                TransactionMode = transactionMode
            }
        );

    public static IAsyncDocumentSession GetNoCacheSession(this IDocumentStore store) => store.GetSession(noCache: true);
    public static IAsyncDocumentSession GetNoTrackingSession(this IDocumentStore store) => store.GetSession(noTracking: true);
    public static IAsyncDocumentSession GetNoCacheNoTrackingSession(this IDocumentStore store) => store.GetSession(true, true);

    public static IAsyncDocumentSession GetClusterSession(this IDocumentStore store) =>
        store.GetSession(transactionMode: TransactionMode.ClusterWide);

    /// <summary>
    /// returns true if entity was created
    /// </summary>
    public static async Task<OperationResult> AddOrUpdateEntity<T>(
        this IDocumentStore store,
        T entity
    ) where T : Entity
    {
        var values = GetValues(entity);

        return await AddOrUpdateEntity(store, entity, values);
    }

    public static Task<OperationResult> AddOrUpdateEntity<T>(
        this IDocumentStore store,
        T entity,
        Dictionary<string, object> values
    ) where T : Entity
    {
        return AddOrUpdateEntity(store, entity, values, values);
    }

    public static async Task<OperationResult> AddOrUpdateEntity<T>(
        this IDocumentStore store,
        T entity,
        Dictionary<string, object> insertValues,
        Dictionary<string, object> updateValues
    ) where T : Entity
    {
        var insertRequest = BuildInsertRequest(store, entity, insertValues);
        var updateRequest = BuildUpdateRequest(entity, updateValues);
        var operation = new PatchOperation
        (
            entity.GetId(),
            null,
            updateRequest,
            insertRequest
        );
        var result = await store.Operations.SendAsync(operation);

        return result switch
        {
            PatchStatus.Created => OperationResult.Created,
            PatchStatus.Patched => OperationResult.Updated,
            _ => OperationResult.NotModified
        };
    }

    /// <summary>
    /// returns true if entity was created
    /// </summary>
    public static async Task<bool> AddEntity<T>(
        this IDocumentStore store,
        T entity
    ) where T : Entity
    {
        var values = GetValues(entity);
        var insertRequest = BuildInsertRequest(store, entity, values);
        var updateRequest = BuildEmptyRequest();
        var operation = new PatchOperation
        (
            entity.GetId(),
            null,
            updateRequest,
            insertRequest
        );

        var result = await store.Operations.SendAsync(operation);

        return result == PatchStatus.Created;
    }

    /// <summary>
    /// returns true if entity was created
    /// </summary>
    public static async Task<bool> UpdateEntity<T>(
        this IDocumentStore store,
        T entity
    ) where T : Entity
    {
        var values = GetValues(entity);
        var updateRequest = BuildUpdateRequest(entity, values);
        var operation = new PatchOperation
        (
            entity.GetId(),
            null,
            updateRequest
        );

        var result = await store.Operations.SendAsync(operation);

        return result == PatchStatus.Patched;
    }

    /// <summary>
    /// returns true if entity was created
    /// </summary>
    public static async Task<bool> UpsertEntity<TEntity>(
        this IDocumentStore store,
        string entityId,
        Dictionary<string, object> values
    )
    {
        var updateScript = """

                           for(var key of Object.keys(args)) {
                               this[key] = args[key];
                           })

                           """;
        var insertScript = BuildInsertScript<TEntity>(store, updateScript);
        var updateRequest = new PatchRequest
        {
            Script = updateScript,
            Values = values
        };
        var insertRequest = new PatchRequest
        {
            Script = insertScript,
            Values = values
        };
        var operation = new PatchOperation
        (
            entityId,
            null,
            updateRequest,
            insertRequest
        );

        var result = await store.Operations.SendAsync(operation);

        return result == PatchStatus.Patched;
    }

    private static Dictionary<string, object> GetValues<T>(T entity) where T : Entity
    {
        var values = new Dictionary<string, object>
        {
            { nameof(entity.AllKeys), entity.AllKeys() },
            { nameof(entity.UpdateableKeys), entity.UpdateableKeys() }
        };
        foreach (var entry in entity.ToEntries())
            values.Add(entry.Key, entry.Value);

        return values;
    }

    private static PatchRequest BuildInsertRequest<T>(
        this IDocumentStore store,
        T entity,
        Dictionary<string, object> values
    ) where T : Entity
        => new()
        {
            Script = $@"
for(var i = 0; i < ${nameof(entity.AllKeys)}.length; i++) {{
    var key = ${nameof(entity.AllKeys)}[i];

    this[key] = args[key];
}}

this['@metadata'] = {{ 
    '@collection': '{store.Conventions.FindCollectionName(typeof(T))}', 
    'Raven-Clr-Type': '{typeof(T).FullName}, {typeof(T).Assembly.GetName().Name}' 
}};
",
            Values = values
        };

    private static PatchRequest BuildUpdateRequest<T>(
        T entity,
        Dictionary<string, object> values
    ) where T : Entity
        => new()
        {
            Script = $@"
for(var i = 0; i < ${nameof(entity.UpdateableKeys)}.length; i++) {{
    var key = ${nameof(entity.UpdateableKeys)}[i];

    this[key] = args[key];
}}
",
            Values = values
        };

    private static PatchRequest BuildEmptyRequest()
        => new()
        {
            Script = "{}"
        };

    private static string BuildInsertScript<T>(
        this IDocumentStore store,
        string updateScript
    )
        => $$"""
             ${{updateScript}}

             this['@metadata'] = { 
                 '@collection': '{{store.Conventions.FindCollectionName(typeof(T))}}', 
                 'Raven-Clr-Type': '{{typeof(T).FullName}}, {{typeof(T).Assembly.GetName().Name}}' 
             };

             """;

    public static Task<CompareExchangeResult<TValue>> PutCompareExchange<TValue>(this IDocumentStore store, string key, TValue value)
        => store
            .Operations.SendAsync(new PutCompareExchangeValueOperation<TValue>(
                    key,
                    value,
                    0
                )
            );
}
