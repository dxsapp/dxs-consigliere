using System.ComponentModel.DataAnnotations;

namespace Dxs.Bsv.Zmq.Configs;

public class ZmqClientConfig
{
    [Required]
    public string RawTx2Address { get; init; }

    [Required]
    public string RemovedFromMempoolBlockAddress { get; init; }

    [Required]
    public string DiscardedFromMempoolAddress { get; init; }

    [Required]
    public string HashBlock2Address { get; init; }
}