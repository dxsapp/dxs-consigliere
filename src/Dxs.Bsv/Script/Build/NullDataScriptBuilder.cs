namespace Dxs.Bsv.Script.Build;

public class NullDataScriptBuilder: ScriptBuilder
{
    public NullDataScriptBuilder(params byte[][] data): base(ScriptType.NullData, null)
    {
        foreach (var token in ScriptSamples.NullDataTokens)
        {
            Tokens.Add(token.Clone());
        }
            
        AddOpCode(OpCode.OP_RETURN);

        foreach (var segment in data)
        {
            AddData(segment);
        }
    }
}