namespace Dxs.Consigliere.Dto.Responses;

public record ValidateStasResponse(
    bool AskLater,
    string Id,
    bool IsLegal,
    bool IsIssue,
    bool IsRedeem,
    string EventType,
    int? SpendingType,
    bool? OptionalDataContinuity,
    string TokenId,
    string[] Roots,
    string[] IllegalRoots
);
