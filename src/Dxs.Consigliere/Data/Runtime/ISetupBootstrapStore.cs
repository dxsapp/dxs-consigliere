using Dxs.Consigliere.Data.Models.Runtime;

namespace Dxs.Consigliere.Data.Runtime;

public interface ISetupBootstrapStore
{
    SetupBootstrapDocument Get();
    Task<SetupBootstrapDocument> SaveAsync(SetupBootstrapDocument document, CancellationToken cancellationToken = default);
}
