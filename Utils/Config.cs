
using Microsoft.ServiceFabric.Data.Collections;
using System.Fabric;

namespace Utils
{
    public static class Config
    {
        public const int BLOCKS_UNTIL_DIFFICULTY_SHIFT = 10;
        public const int TARGET_BLOCK_TIME_SECS = 5;
        public const int MINING_JOB_TIMEOUT_SECS = 1;
        public const int HASH_ATTEMPTS_PER_CYCLE = 1000;
        public const int HASH_BYTES = 32; // 256 bits;
        public const int MAX_TX_PER_BLOCK = 10;
        public const int INITIAL_DIFFICULTY = 20;
        public static Uri BLOCKCHAIN_URI = new Uri("fabric:/BlockchainFabric/Node");
        public const string BLOCKCHAIN_STATE_DICT_NAME = "BlockchainState";
        public const string COOKIE_PARTITION_NUMBER_KEY = "partition";
        public const long INITIAL_REWARD = 50;
        public const long HALVING_BLOCKS_NUM = 210000;
        public const long NUMBER_OF_CONFIRMATIONS = 50;
        public const int SYNC_NODE_PERIOD_SECS = 5;
    }
}