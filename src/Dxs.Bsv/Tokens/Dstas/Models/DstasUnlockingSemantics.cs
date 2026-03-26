#nullable enable
namespace Dxs.Bsv.Tokens.Dstas.Models;

public sealed record DstasUnlockingSemantics(
    int SpendingType,
    bool UsesSimpleTail,
    bool UsesAuthorityMultisigTail
);
