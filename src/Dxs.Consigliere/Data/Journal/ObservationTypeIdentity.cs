namespace Dxs.Consigliere.Data.Journal;

internal static class ObservationTypeIdentity
{
    public static string For<TObservation>()
        => typeof(TObservation).AssemblyQualifiedName ?? typeof(TObservation).FullName ?? typeof(TObservation).Name;
}
