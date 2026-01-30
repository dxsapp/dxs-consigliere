namespace Dxs.Consigliere.Data;

public class RavenDbConfig
{
    public string[] Urls { get; init; }
    public string DbName { get; init; }
    public string ClientCertificate { get; init; }
    public string CertificatePassword { get; init; }
}
