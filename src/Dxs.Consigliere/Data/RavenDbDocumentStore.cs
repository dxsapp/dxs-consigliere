using System.Security.Cryptography.X509Certificates;

using Dxs.Common.Extensions;
using Dxs.Common.Utils;
using Dxs.Consigliere.Data.Indexes;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace Dxs.Consigliere.Data;

public class RavenDbDocumentStore(IOptions<RavenDbConfig> config)
{
    public readonly IDocumentStore DocumentStore = CreateStore(config.Value);

    private static IDocumentStore CreateStore(RavenDbConfig config)
    {
        var store = new DocumentStore
        {
            Urls = config.Urls,
            Database = config.DbName
        };

        if (config.ClientCertificate.IsNotNullOrEmpty())
        {
            var path = PathHelper.GetPath(config.ClientCertificate);
            store.Certificate = new X509Certificate2(path, config.CertificatePassword);
        }

        store.Conventions.SaveEnumsAsIntegers = false;
        store.Initialize();

        var result = store.Maintenance.Server.Send(new GetDatabaseRecordOperation(config.DbName));

        if (result == null)
            store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(config.DbName)));

        new AddressHistoryIndex().Execute(store);
        new FoundMissingRootsIndex().Execute(store);
        new StasUtxoSetIndex().Execute(store);

        return store;
    }
}
