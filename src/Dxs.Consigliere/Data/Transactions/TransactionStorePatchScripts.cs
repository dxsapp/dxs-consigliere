using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

internal static class TransactionStorePatchScripts
{
    public static readonly string UpdateStasAttributesQuery = $@"
var stasInputsCount = 0;
var inputsCount = this.{nameof(MetaTransaction.Inputs)}.length;
var outputsCount = this.{nameof(MetaTransaction.Outputs)}.length;
var stasType1 = '{ScriptType.P2STAS:G}';
var stasType2 = '{ScriptType.DSTAS:G}';
var p2pkhType = '{ScriptType.P2PKH:G}';
var p2mpkhType = '{ScriptType.P2MPKH:G}';

var withNote = outputsCount > 0 &&
    this.{nameof(MetaTransaction.Outputs)}[outputsCount - 1].{nameof(MetaTransaction.Output.Type)} === '{ScriptType.NullData:G}';
var withFee = false;
var allInputsKnown = true;
var stasFrom = null;
var firstInputHash160 = null;
var firstInputTokenId = null;
var inputTokens = new Set();
var illegalRoots = new Set();
var missingTxs = new Set();
var redeemAddress = null;
var outputTokens = new Set();
var firstInputFrozen = null;
var firstOutputFrozen = null;
var firstInputActionType = null;
var dstasSpendingType = null;
var inputOptionalDataFingerprints = new Set();
var outputOptionalDataFingerprints = new Set();
var firstStasInputSeen = false;

for (var i = 0; i < outputsCount; i++) {{
    var output = this.{nameof(MetaTransaction.Outputs)}[i];
    var outputType = output.{nameof(MetaTransaction.Output.Type)};
    var outputIsStas = outputType === stasType1 || outputType === stasType2;

    if (outputIsStas) {{
        outputTokens.add(output.{nameof(MetaTransaction.Output.TokenId)});

        if (firstOutputFrozen === null &&
            output.{nameof(MetaTransaction.Output.DstasFrozen)} !== undefined &&
            output.{nameof(MetaTransaction.Output.DstasFrozen)} !== null) {{
            firstOutputFrozen = output.{nameof(MetaTransaction.Output.DstasFrozen)} === true;
        }}

        if (output.{nameof(MetaTransaction.Output.DstasOptionalDataFingerprint)}) {{
            outputOptionalDataFingerprints.add(output.{nameof(MetaTransaction.Output.DstasOptionalDataFingerprint)});
        }}
    }}

    if (i === 0 && (outputType === p2pkhType || outputType === p2mpkhType)) {{
        redeemAddress = output.{nameof(MetaTransaction.Output.Address)};
    }}
}}

outputTokens = [...outputTokens];

for (var i = 0; i < inputsCount; i++) {{
    var slimInput = this.{nameof(MetaTransaction.Inputs)}[i];
    var inputTxId = slimInput.{nameof(MetaTransaction.Input.TxId)};
    var inputTx = load(inputTxId);

    if (!inputTx) {{
        allInputsKnown = false;
        missingTxs.add(inputTxId);
        continue;
    }}

    var vout = slimInput.{nameof(MetaTransaction.Input.Vout)};
    var inputOutput = inputTx.{nameof(MetaTransaction.Outputs)}[vout];
    var inputType = inputOutput.{nameof(MetaTransaction.Output.Type)};
    var isInputStas = inputType === stasType1 || inputType === stasType2;

    if (i === 0) {{
        firstInputHash160 = inputOutput.{nameof(MetaTransaction.Output.Hash160)};
    }} else if (i === inputsCount - 1) {{
        withFee = inputType === p2pkhType || inputType === p2mpkhType;
    }}

    if (isInputStas) {{
        stasInputsCount++;

        if (!firstStasInputSeen) {{
            firstStasInputSeen = true;
            stasFrom = inputOutput.{nameof(MetaTransaction.Output.Address)};
            firstInputTokenId = inputOutput.{nameof(MetaTransaction.Output.TokenId)};

            if (inputOutput.{nameof(MetaTransaction.Output.DstasFrozen)} !== undefined &&
                inputOutput.{nameof(MetaTransaction.Output.DstasFrozen)} !== null) {{
                firstInputFrozen = inputOutput.{nameof(MetaTransaction.Output.DstasFrozen)} === true;
            }}

            if (inputOutput.{nameof(MetaTransaction.Output.DstasActionType)} !== undefined &&
                inputOutput.{nameof(MetaTransaction.Output.DstasActionType)} !== null) {{
                firstInputActionType = inputOutput.{nameof(MetaTransaction.Output.DstasActionType)};
            }}
        }}

        if (dstasSpendingType === null &&
            slimInput.{nameof(MetaTransaction.Input.DstasSpendingType)} !== undefined &&
            slimInput.{nameof(MetaTransaction.Input.DstasSpendingType)} !== null) {{
            dstasSpendingType = slimInput.{nameof(MetaTransaction.Input.DstasSpendingType)};
        }}

        if (inputOutput.{nameof(MetaTransaction.Output.DstasOptionalDataFingerprint)}) {{
            inputOptionalDataFingerprints.add(inputOutput.{nameof(MetaTransaction.Output.DstasOptionalDataFingerprint)});
        }}

        if (inputTx.{nameof(MetaTransaction.MissingTransactions)}.length > 0) {{
            missingTxs.add(inputTxId);
        }}

        inputTokens.add(inputOutput.{nameof(MetaTransaction.Output.TokenId)});

        if (inputTx.{nameof(MetaTransaction.IsIssue)} === true) {{
            if (inputTx.{nameof(MetaTransaction.IsValidIssue)} !== true) {{
                illegalRoots.add(inputTxId);
            }}
        }} else {{
            for (var j = 0; j < inputTx.{nameof(MetaTransaction.IllegalRoots)}.length; j++) {{
                var illegalRoot = inputTx.{nameof(MetaTransaction.IllegalRoots)}[j];
                illegalRoots.add(illegalRoot);
            }}
        }}
    }}
}}

var hasStasOutputs = outputTokens.length > 0;
var isStas = hasStasOutputs || stasInputsCount > 0;
var isIssue = isStas && hasStasOutputs && stasInputsCount === 0;
var isValidIssue = isIssue &&
    allInputsKnown &&
    outputTokens.length === 1 &&
    outputTokens[0] === firstInputHash160;

var firstOutput = outputsCount > 0 ? this.{nameof(MetaTransaction.Outputs)}[0] : null;
var firstOutputIsRedeemType = firstOutput &&
    (firstOutput.{nameof(MetaTransaction.Output.Type)} === p2pkhType ||
     firstOutput.{nameof(MetaTransaction.Output.Type)} === p2mpkhType);
var redeemBlockedByState = firstInputFrozen === true || firstInputActionType === 'confiscation';
var redeemUsesRegularSpending = dstasSpendingType === null || dstasSpendingType === 1;
var isRedeem = allInputsKnown &&
    stasInputsCount === 1 &&
    firstOutputIsRedeemType &&
    redeemUsesRegularSpending &&
    !redeemBlockedByState &&
    firstInputTokenId === firstOutput.{nameof(MetaTransaction.Output.Hash160)};

var dstasEventType = null;
if (isStas && dstasSpendingType !== null) {{
    if (dstasSpendingType === 4) {{
        dstasEventType = 'swap_cancel';
    }} else if (dstasSpendingType === 3) {{
        dstasEventType = 'confiscation';
    }} else if (dstasSpendingType === 2) {{
        if (firstInputFrozen === true && firstOutputFrozen === false) {{
            dstasEventType = 'unfreeze';
        }} else if (firstOutputFrozen === true) {{
            dstasEventType = 'freeze';
        }} else {{
            dstasEventType = 'freeze';
        }}
    }}
}}

var optionalDataContinuity = null;
if (isStas) {{
    optionalDataContinuity = true;
    if (inputOptionalDataFingerprints.size > 0 && outputOptionalDataFingerprints.size === 0) {{
        optionalDataContinuity = false;
    }} else {{
        var outputOptionalDataFingerprintsArr = [...outputOptionalDataFingerprints];
        for (var i = 0; i < outputOptionalDataFingerprintsArr.length; i++) {{
            var fingerprint = outputOptionalDataFingerprintsArr[i];
            if (!inputOptionalDataFingerprints.has(fingerprint)) {{
                optionalDataContinuity = false;
                break;
            }}
        }}
    }}
}}

this.{nameof(MetaTransaction.IsStas)} = isStas;
this.{nameof(MetaTransaction.IsIssue)} = isIssue;
this.{nameof(MetaTransaction.IsValidIssue)} = isValidIssue;
this.{nameof(MetaTransaction.IsRedeem)} = isRedeem;
this.{nameof(MetaTransaction.IsWithFee)} = isStas && withFee;
this.{nameof(MetaTransaction.IsWithNote)} = isStas && withNote;
this.{nameof(MetaTransaction.AllStasInputsKnown)} = isStas && allInputsKnown;
this.{nameof(MetaTransaction.RedeemAddress)} = isRedeem ? redeemAddress : null;
this.{nameof(MetaTransaction.StasFrom)} = isStas ? stasFrom : null;
this.{nameof(MetaTransaction.DstasEventType)} = dstasEventType;
this.{nameof(MetaTransaction.DstasSpendingType)} = dstasSpendingType;
this.{nameof(MetaTransaction.DstasInputFrozen)} = firstInputFrozen;
this.{nameof(MetaTransaction.DstasOutputFrozen)} = firstOutputFrozen;
this.{nameof(MetaTransaction.DstasOptionalDataContinuity)} = optionalDataContinuity;

this.{nameof(MetaTransaction.TokenIds)} = [...new Set([...outputTokens, ...inputTokens])];
this.{nameof(MetaTransaction.IllegalRoots)} = [...illegalRoots];
this.{nameof(MetaTransaction.MissingTransactions)} = [...missingTxs];
";

    public static readonly string InsertMetaTransactionQuery = $@"
this.{nameof(MetaTransaction.Block)} = ${nameof(MetaTransaction.Block)};
this.{nameof(MetaTransaction.Height)} = ${nameof(MetaTransaction.Height)};
this.{nameof(MetaTransaction.Index)} = ${nameof(MetaTransaction.Index)};
this.{nameof(MetaTransaction.Timestamp)} = ${nameof(MetaTransaction.Timestamp)};

this.{nameof(MetaTransaction.Inputs)} = ${nameof(MetaTransaction.Inputs)};
this.{nameof(MetaTransaction.Outputs)} = ${nameof(MetaTransaction.Outputs)};
this.{nameof(MetaTransaction.Addresses)} = ${nameof(MetaTransaction.Addresses)};

this.{nameof(MetaTransaction.Note)} = ${nameof(MetaTransaction.Note)};

this.{nameof(MetaTransaction.IsStas)} = false;
this.{nameof(MetaTransaction.IsIssue)} = false;
this.{nameof(MetaTransaction.IsValidIssue)} = false;
this.{nameof(MetaTransaction.IsRedeem)} = false;
this.{nameof(MetaTransaction.AllStasInputsKnown)} = false;
this.{nameof(MetaTransaction.RedeemAddress)} = null;
this.{nameof(MetaTransaction.StasFrom)} = null;
this.{nameof(MetaTransaction.DstasEventType)} = null;
this.{nameof(MetaTransaction.DstasSpendingType)} = null;
this.{nameof(MetaTransaction.DstasInputFrozen)} = null;
this.{nameof(MetaTransaction.DstasOutputFrozen)} = null;
this.{nameof(MetaTransaction.DstasOptionalDataContinuity)} = null;

this.{nameof(MetaTransaction.TokenIds)} = [];
this.{nameof(MetaTransaction.IllegalRoots)} = [];
this.{nameof(MetaTransaction.MissingTransactions)} = [];

if (${nameof(MetaTransaction.IsStas)}) {{
    {UpdateStasAttributesQuery}
}}

this['@metadata'] = {{ 
    '@collection': 'MetaTransactions', 
    'Raven-Clr-Type': '{typeof(MetaTransaction).FullName}, {typeof(MetaTransaction).Assembly.GetName().Name}' 
}};
";

    public static readonly string UpdateMetaTransactionQuery = $@"
this.{nameof(MetaTransaction.Block)} = ${nameof(MetaTransaction.Block)};
this.{nameof(MetaTransaction.Index)} = ${nameof(MetaTransaction.Index)};
this.{nameof(MetaTransaction.Height)} = ${nameof(MetaTransaction.Height)};

if (!this.{nameof(MetaTransaction.Timestamp)}) {{
    this.{nameof(MetaTransaction.Timestamp)} = ${nameof(MetaTransaction.Timestamp)};
}}

if (${nameof(MetaTransaction.IsStas)}) {{
    {UpdateStasAttributesQuery}
}}
";

    public static readonly string InsertTransactionHexData = $@"
this.{nameof(TransactionHexData.TxId)} = ${nameof(TransactionHexData.TxId)};
this.{nameof(TransactionHexData.Hex)} = ${nameof(TransactionHexData.Hex)};

this['@metadata'] = {{ 
    '@collection': 'TransactionHexDatas', 
    'Raven-Clr-Type': '{typeof(TransactionHexData).FullName}, {typeof(TransactionHexData).Assembly.GetName().Name}' 
}};
";

    public static readonly string InsertOutputQuery = $@"
this.{nameof(MetaOutput.TxId)} = ${nameof(MetaOutput.TxId)};
this.{nameof(MetaOutput.Vout)} = ${nameof(MetaOutput.Vout)};

this.{nameof(MetaOutput.Type)} = ${nameof(MetaOutput.Type)};
this.{nameof(MetaOutput.Satoshis)} = 0;
this.{nameof(MetaOutput.Address)} = null;
this.{nameof(MetaOutput.TokenId)} = null;
this.{nameof(MetaOutput.Hash160)} = null;
this.{nameof(MetaOutput.ScriptPubKey)} = null;
this.{nameof(MetaOutput.Symbol)} = null;
this.{nameof(MetaOutput.DstasFlags)} = null;
this.{nameof(MetaOutput.DstasFreezeEnabled)} = null;
this.{nameof(MetaOutput.DstasConfiscationEnabled)} = null;
this.{nameof(MetaOutput.DstasFrozen)} = null;
this.{nameof(MetaOutput.DstasFreezeAuthority)} = null;
this.{nameof(MetaOutput.DstasConfiscationAuthority)} = null;
this.{nameof(MetaOutput.DstasServiceFields)} = null;
this.{nameof(MetaOutput.DstasActionType)} = null;
this.{nameof(MetaOutput.DstasActionData)} = null;
this.{nameof(MetaOutput.DstasRequestedScriptHash)} = null;
this.{nameof(MetaOutput.DstasOptionalData)} = null;
this.{nameof(MetaOutput.DstasOptionalDataFingerprint)} = null;

this.{nameof(MetaOutput.InputIdx)} = 0;
this.{nameof(MetaOutput.SpendTxId)} = null;
this.{nameof(MetaOutput.Spent)} = false;

this['@metadata'] = {{ 
    '@collection': 'MetaOutputs', 
    'Raven-Clr-Type': '{typeof(MetaOutput).FullName}, {typeof(MetaOutput).Assembly.GetName().Name}' 
}};
";

    public static readonly string UpdateMetaOutputQuery = $@"
this.{nameof(MetaOutput.Type)} = ${nameof(MetaOutput.Type)};
this.{nameof(MetaOutput.Satoshis)} = ${nameof(MetaOutput.Satoshis)};
this.{nameof(MetaOutput.Address)} = ${nameof(MetaOutput.Address)};
this.{nameof(MetaOutput.TokenId)} = ${nameof(MetaOutput.TokenId)};
this.{nameof(MetaOutput.Hash160)} = ${nameof(MetaOutput.Hash160)};
this.{nameof(MetaOutput.ScriptPubKey)} = ${nameof(MetaOutput.ScriptPubKey)};
this.{nameof(MetaOutput.Symbol)} = ${nameof(MetaOutput.Symbol)};
this.{nameof(MetaOutput.DstasFlags)} = ${nameof(MetaOutput.DstasFlags)};
this.{nameof(MetaOutput.DstasFreezeEnabled)} = ${nameof(MetaOutput.DstasFreezeEnabled)};
this.{nameof(MetaOutput.DstasConfiscationEnabled)} = ${nameof(MetaOutput.DstasConfiscationEnabled)};
this.{nameof(MetaOutput.DstasFrozen)} = ${nameof(MetaOutput.DstasFrozen)};
this.{nameof(MetaOutput.DstasFreezeAuthority)} = ${nameof(MetaOutput.DstasFreezeAuthority)};
this.{nameof(MetaOutput.DstasConfiscationAuthority)} = ${nameof(MetaOutput.DstasConfiscationAuthority)};
this.{nameof(MetaOutput.DstasServiceFields)} = ${nameof(MetaOutput.DstasServiceFields)};
this.{nameof(MetaOutput.DstasActionType)} = ${nameof(MetaOutput.DstasActionType)};
this.{nameof(MetaOutput.DstasActionData)} = ${nameof(MetaOutput.DstasActionData)};
this.{nameof(MetaOutput.DstasRequestedScriptHash)} = ${nameof(MetaOutput.DstasRequestedScriptHash)};
this.{nameof(MetaOutput.DstasOptionalData)} = ${nameof(MetaOutput.DstasOptionalData)};
this.{nameof(MetaOutput.DstasOptionalDataFingerprint)} = ${nameof(MetaOutput.DstasOptionalDataFingerprint)};
";

    public static readonly string UpdateMetaInputQuery = $@"
this.{nameof(MetaOutput.InputIdx)} = ${nameof(MetaOutput.InputIdx)};
this.{nameof(MetaOutput.SpendTxId)} = ${nameof(MetaOutput.SpendTxId)};
this.{nameof(MetaOutput.Spent)} = true;
";

    public static readonly string InsertMetaOutputQuery = $@"
{InsertOutputQuery}

{UpdateMetaOutputQuery}
";

    public static readonly string InsertMetaInputQuery = $@"
{InsertOutputQuery}
    
{UpdateMetaInputQuery}
";

    public const string FreeMetaOutputQuery = $@"
if (this.{nameof(MetaOutput.SpendTxId)} == ${nameof(MetaOutput.SpendTxId)}) {{
    this.{nameof(MetaOutput.InputIdx)} = 0;
    this.{nameof(MetaOutput.SpendTxId)} = null;
    this.{nameof(MetaOutput.Spent)} = false;
}}
";
}
