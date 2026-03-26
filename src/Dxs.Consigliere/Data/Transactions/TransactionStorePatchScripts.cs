using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

internal static class TransactionStorePatchScripts
{
    public static readonly string UpdateStasAttributesQuery = $@"
this.{nameof(MetaTransaction.IsStas)} = args.{nameof(MetaTransaction.IsStas)};
this.{nameof(MetaTransaction.IsIssue)} = args.{nameof(MetaTransaction.IsIssue)};
this.{nameof(MetaTransaction.IsValidIssue)} = args.{nameof(MetaTransaction.IsValidIssue)};
this.{nameof(MetaTransaction.IsRedeem)} = args.{nameof(MetaTransaction.IsRedeem)};
this.{nameof(MetaTransaction.IsWithFee)} = args.{nameof(MetaTransaction.IsWithFee)};
this.{nameof(MetaTransaction.IsWithNote)} = args.{nameof(MetaTransaction.IsWithNote)};
this.{nameof(MetaTransaction.AllStasInputsKnown)} = args.{nameof(MetaTransaction.AllStasInputsKnown)};
this.{nameof(MetaTransaction.RedeemAddress)} = args.{nameof(MetaTransaction.RedeemAddress)};
this.{nameof(MetaTransaction.StasFrom)} = args.{nameof(MetaTransaction.StasFrom)};
this.{nameof(MetaTransaction.DstasEventType)} = args.{nameof(MetaTransaction.DstasEventType)};
this.{nameof(MetaTransaction.DstasSpendingType)} = args.{nameof(MetaTransaction.DstasSpendingType)};
this.{nameof(MetaTransaction.DstasInputFrozen)} = args.{nameof(MetaTransaction.DstasInputFrozen)};
this.{nameof(MetaTransaction.DstasOutputFrozen)} = args.{nameof(MetaTransaction.DstasOutputFrozen)};
this.{nameof(MetaTransaction.DstasOptionalDataContinuity)} = args.{nameof(MetaTransaction.DstasOptionalDataContinuity)};
this.{nameof(MetaTransaction.StasProtocolType)} = args.{nameof(MetaTransaction.StasProtocolType)};
this.{nameof(MetaTransaction.StasValidationStatus)} = args.{nameof(MetaTransaction.StasValidationStatus)};
this.{nameof(MetaTransaction.CanProjectTokenOutputs)} = args.{nameof(MetaTransaction.CanProjectTokenOutputs)};
this.{nameof(MetaTransaction.TokenIds)} = args.{nameof(MetaTransaction.TokenIds)};
this.{nameof(MetaTransaction.IllegalRoots)} = args.{nameof(MetaTransaction.IllegalRoots)};
this.{nameof(MetaTransaction.MissingTransactions)} = args.{nameof(MetaTransaction.MissingTransactions)};
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
this.{nameof(MetaTransaction.StasProtocolType)} = null;
this.{nameof(MetaTransaction.StasValidationStatus)} = null;
this.{nameof(MetaTransaction.CanProjectTokenOutputs)} = null;

this.{nameof(MetaTransaction.TokenIds)} = [];
this.{nameof(MetaTransaction.IllegalRoots)} = [];
this.{nameof(MetaTransaction.MissingTransactions)} = [];

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
}
