#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasIssueSemantics(
    bool IsStas,
    bool IsIssue,
    bool IsValidIssue,
    IReadOnlyList<string> TokenIds
);
