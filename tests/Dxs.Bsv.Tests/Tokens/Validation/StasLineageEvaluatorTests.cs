using Dxs.Bsv.Script;
using Dxs.Bsv.Tokens.Validation;

namespace Dxs.Bsv.Tests.Tokens.Validation;

public class StasLineageEvaluatorTests
{
    private readonly StasLineageEvaluator _sut = new();

    [Fact]
    public void Evaluate_DetectsFreezeEventAndOptionalDataContinuity()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-freeze",
            [
                new StasLineageInput(
                    "parent-a",
                    0,
                    2,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1ParentAddress",
                                TokenId: "token-1",
                                Hash160: "issuer-1",
                                DstasFrozen: false,
                                DstasActionType: "empty",
                                DstasOptionalDataFingerprint: "opt-1"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1ReceiverAddress",
                    TokenId: "token-1",
                    Hash160: "receiver-1",
                    DstasFrozen: true,
                    DstasActionType: "freeze",
                    DstasOptionalDataFingerprint: "opt-1"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.Equal("freeze", result.DstasEventType);
        Assert.Equal(2, result.DstasSpendingType);
        Assert.False(result.DstasInputFrozen);
        Assert.True(result.DstasOutputFrozen);
        Assert.True(result.DstasOptionalDataContinuity);
        Assert.Equal(["token-1"], result.TokenIds);
    }

    [Fact]
    public void Evaluate_DetectsUnfreezeEventWhenFrozenInputBecomesUnfrozen()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-unfreeze",
            [
                new StasLineageInput(
                    "parent-frozen",
                    0,
                    2,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1FrozenOwner",
                                TokenId: "token-1",
                                Hash160: "issuer-1",
                                DstasFrozen: true,
                                DstasActionType: "freeze",
                                DstasOptionalDataFingerprint: "opt-1"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1RecoveredOwner",
                    TokenId: "token-1",
                    Hash160: "receiver-1",
                    DstasFrozen: false,
                    DstasActionType: "empty",
                    DstasOptionalDataFingerprint: "opt-1"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.Equal("unfreeze", result.DstasEventType);
        Assert.Equal(2, result.DstasSpendingType);
        Assert.True(result.DstasInputFrozen);
        Assert.False(result.DstasOutputFrozen);
        Assert.True(result.DstasOptionalDataContinuity);
    }

    [Fact]
    public void Evaluate_DetectsConfiscationEvent()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-confiscation",
            [
                new StasLineageInput(
                    "parent-frozen",
                    0,
                    3,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1OwnerBeforeConfiscation",
                                TokenId: "token-1",
                                Hash160: "issuer-1",
                                DstasFrozen: true,
                                DstasActionType: "freeze",
                                DstasOptionalDataFingerprint: "opt-1"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1ConfiscationReceiver",
                    TokenId: "token-1",
                    Hash160: "receiver-1",
                    DstasFrozen: false,
                    DstasActionType: "confiscation",
                    DstasOptionalDataFingerprint: "opt-1"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.Equal("confiscation", result.DstasEventType);
        Assert.Equal(3, result.DstasSpendingType);
        Assert.True(result.DstasOptionalDataContinuity);
    }

    [Fact]
    public void Evaluate_BlocksRedeemWhenInputIsFrozen()
    {
        var tokenId = "issuer-token-hash";
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-redeem",
            [
                new StasLineageInput(
                    "parent-frozen",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1FrozenOwner",
                                TokenId: tokenId,
                                Hash160: tokenId,
                                DstasFrozen: true,
                                DstasActionType: "freeze"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.P2PKH,
                    Address: "1IssuerRedeem",
                    Hash160: tokenId
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.False(result.IsRedeem);
        Assert.Equal(1, result.DstasSpendingType);
        Assert.Null(result.DstasEventType);
    }

    [Fact]
    public void Evaluate_MapsSwapEventWhenRegularSpendConsumesSwapMarkedInput()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-swap",
            [
                new StasLineageInput(
                    "parent-swap",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1SwapOwner",
                                TokenId: "token-3",
                                Hash160: "issuer-3",
                                DstasFrozen: false,
                                DstasActionType: "swap",
                                DstasOptionalDataFingerprint: "opt-swap"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1Counterparty",
                    TokenId: "token-3",
                    Hash160: "counterparty-3",
                    DstasFrozen: false,
                    DstasActionType: "empty",
                    DstasOptionalDataFingerprint: "opt-swap"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.Equal(1, result.DstasSpendingType);
        Assert.Equal("swap", result.DstasEventType);
        Assert.True(result.DstasOptionalDataContinuity);
    }

    [Fact]
    public void Evaluate_MapsSwapCancelEventFromSpendingType()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-swap-cancel",
            [
                new StasLineageInput(
                    "parent-swap",
                    0,
                    4,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1SwapOwner",
                                TokenId: "token-3",
                                Hash160: "issuer-3",
                                DstasFrozen: false,
                                DstasActionType: "swap",
                                DstasOptionalDataFingerprint: "opt-swap"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1SwapOwner",
                    TokenId: "token-3",
                    Hash160: "issuer-3",
                    DstasFrozen: false,
                    DstasActionType: "empty",
                    DstasOptionalDataFingerprint: "opt-swap"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.Equal("swap_cancel", result.DstasEventType);
        Assert.Equal(4, result.DstasSpendingType);
        Assert.True(result.DstasOptionalDataContinuity);
    }

    [Fact]
    public void Evaluate_RequiresCurrentOwnerToMatchIssuerForRedeem()
    {
        var tokenId = "issuer-token-hash";
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-redeem-non-issuer",
            [
                new StasLineageInput(
                    "parent-non-issuer",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1DifferentCurrentOwner",
                                TokenId: tokenId,
                                Hash160: tokenId,
                                DstasFrozen: false,
                                DstasActionType: "empty"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.P2PKH,
                    Address: "1IssuerRedeem",
                    Hash160: tokenId
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.False(result.IsRedeem);
        Assert.Equal(1, result.DstasSpendingType);
        Assert.Null(result.DstasEventType);
    }

    [Fact]
    public void Evaluate_TracksMissingDependenciesAndIllegalRootsFromDirectParents()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-child",
            [
                new StasLineageInput(
                    "missing-parent",
                    0,
                    null,
                    null
                ),
                new StasLineageInput(
                    "invalid-issue-parent",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.P2STAS,
                                Address: "1TokenOwner",
                                TokenId: "token-2",
                                Hash160: "issuer-2"
                            )
                        ],
                        IsIssue: true,
                        IsValidIssue: false
                    )
                ),
                new StasLineageInput(
                    "partial-parent",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1PartialOwner",
                                TokenId: "token-2",
                                Hash160: "issuer-2"
                            )
                        ],
                        HasMissingDependencies: true,
                        IllegalRoots: ["root-b"]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1Receiver",
                    TokenId: "token-2",
                    Hash160: "receiver-2"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.False(result.AllInputsKnown);
        Assert.Equal(["invalid-issue-parent", "root-b"], result.IllegalRoots);
        Assert.Equal(["missing-parent", "partial-parent"], result.MissingDependencies);
    }

    [Fact]
    public void Evaluate_FailsOptionalDataContinuityWhenDescendantDropsFingerprint()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-drop-optional-data",
            [
                new StasLineageInput(
                    "parent-with-optional-data",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: "1ParentOwner",
                                TokenId: "token-4",
                                Hash160: "issuer-4",
                                DstasFrozen: false,
                                DstasActionType: "empty",
                                DstasOptionalDataFingerprint: "opt-preserved"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.DSTAS,
                    Address: "1ChildOwner",
                    TokenId: "token-4",
                    Hash160: "receiver-4",
                    DstasFrozen: false,
                    DstasActionType: "empty",
                    DstasOptionalDataFingerprint: null
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.False(result.DstasOptionalDataContinuity);
    }

    [Fact]
    public void Evaluate_AllowsRedeemAfterUnfreezeWhenIssuerIsCurrentOwner()
    {
        const string tokenId = "issuer-token-hash";
        const string issuerAddress = "1IssuerRedeem";

        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-redeem-after-unfreeze",
            [
                new StasLineageInput(
                    "parent-unfrozen",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.DSTAS,
                                Address: issuerAddress,
                                TokenId: tokenId,
                                Hash160: tokenId,
                                DstasFrozen: false,
                                DstasActionType: "empty",
                                DstasOptionalDataFingerprint: "opt-issuer"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.P2MPKH,
                    Address: issuerAddress,
                    Hash160: tokenId
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.True(result.IsRedeem);
        Assert.Equal(1, result.DstasSpendingType);
        Assert.Null(result.DstasEventType);
    }

    [Fact]
    public void Evaluate_DetectsValidIssueWhenOutputTokenMatchesFirstInputHash160()
    {
        var result = _sut.Evaluate(new StasLineageTransaction(
            "tx-issue",
            [
                new StasLineageInput(
                    "funding-parent",
                    0,
                    null,
                    new StasLineageParentTransaction(
                        [
                            new StasLineageOutput(
                                ScriptType.P2PKH,
                                Address: "1Issuer",
                                Hash160: "issuer-hash"
                            )
                        ]
                    )
                )
            ],
            [
                new StasLineageOutput(
                    ScriptType.P2STAS,
                    Address: "1Holder",
                    TokenId: "issuer-hash",
                    Hash160: "issuer-hash"
                )
            ]
        ));

        Assert.True(result.IsStas);
        Assert.True(result.IsIssue);
        Assert.True(result.IsValidIssue);
        Assert.Empty(result.IllegalRoots);
        Assert.Empty(result.MissingDependencies);
    }
}
