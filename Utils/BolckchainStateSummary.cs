using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Structs;

namespace Utils
{
    public struct BlockchainStateSummary
    {
        public List<KeyValuePair<string, long>> TopAccounts { get; set; }
        public List<KeyValuePair<string, long>> MinerHashRates { get; set; }
        public List<Block> LastBlocks { get; set; }
        public string CurrentBlockHash { get; set; }
        public int CurrentDifficulty { get; set; }
        public int NumberOfBlocks { get; set; }
    }
}
