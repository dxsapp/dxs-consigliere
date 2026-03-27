#nullable enable

namespace Dxs.Bsv.ScriptEvaluation;

public sealed record BsvScriptTraceStep(
    string Phase,
    uint ProgramCounter,
    string Opcode,
    int StackDepth,
    string? StackTopHex,
    int AltStackDepth
);
