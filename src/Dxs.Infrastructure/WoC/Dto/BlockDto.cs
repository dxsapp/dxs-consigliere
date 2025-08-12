using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class BlockDto
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("confirmations")]
        public int Confirmations { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("versionHex")]
        public string VersionHex { get; set; }

        [JsonProperty("merkleroot")]
        public string MerkleRoot { get; set; }

        [JsonProperty("txcount")]
        public int TxCount { get; set; }

        [JsonProperty("nTx")]
        public int NTx { get; set; }

        [JsonProperty("num_tx")]
        public int NumTx { get; set; }

        [JsonProperty("tx")]
        public List<string> Tx { get; set; }

        [JsonProperty("time")]
        public int Time { get; set; }

        [JsonProperty("mediantime")]
        public int MedianTime { get; set; }

        [JsonProperty("nonce")]
        public int Nonce { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty("chainwork")]
        public string ChainWork { get; set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("nextblockhash")]
        public string NextBlockHash { get; set; }

        [JsonProperty("coinbaseTx")]
        public CoinbaseTxDto CoinbaseTx { get; set; }

        [JsonProperty("totalFees")]
        public double TotalFees { get; set; }

        [JsonProperty("miner")]
        public string Miner { get; set; }

        [JsonProperty("pages")]
        public object Pages { get; set; }
    }
}