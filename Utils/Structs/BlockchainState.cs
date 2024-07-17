using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Utils.Structs
{
    [Serializable]
    public struct BlockchainState
    {
        public Dictionary<string, long> accountToAmount;
        public Dictionary<string, Tx> txMempool;
        public Dictionary<long, Block> blocks;
        public string currentBlockHash;
        public int currentDifficulty;

        public BlockchainState()
        {
            accountToAmount = new Dictionary<string, long>();
            txMempool = new Dictionary<string, Tx>();
            blocks = new Dictionary<long, Block>();
            currentBlockHash = "";
            currentDifficulty = Config.INITIAL_DIFFICULTY;
        }

        public int NumberOfBlocks { get { return blocks.Count; } }

        public Block LastBlock { get { return NumberOfBlocks > 0 ? blocks[NumberOfBlocks - 1] : new Block(""); } }

        public bool TryApply(ref Block b)
        {
            if (!b.IsValid())
            {
                return false;
            }

            if (!b.IsVerified(currentDifficulty))
            {
                return false;
            }
            if (!IsValidTransactionList(b.txs))
            {
                return false;
            }

            if (currentBlockHash != b.parentHash)
            {
                return false;
            }

            if (b.minedTime <= LastBlock.minedTime)
            {
                return false;
            }

            b.number = NumberOfBlocks;

            foreach (Tx t in b.txs)
            {
                AddToAccount(t.to, t.amount);
                accountToAmount[t.from] -= t.amount;
                txMempool.Remove(t.id);
            }

            AddToAccount(b.miner, GetMiningReward());

            currentBlockHash = b.GetHash();

            blocks[NumberOfBlocks] = b;

            if (NumberOfBlocks % Config.BLOCKS_UNTIL_DIFFICULTY_SHIFT == 0)
            {
                TimeSpan timeDifference = LastBlock.minedTime - blocks[NumberOfBlocks-Config.BLOCKS_UNTIL_DIFFICULTY_SHIFT].minedTime;
                if (timeDifference.TotalSeconds > Config.BLOCKS_UNTIL_DIFFICULTY_SHIFT * Config.TARGET_BLOCK_TIME_SECS * 125 / 100)
                {
                    --currentDifficulty;
                }
                else if (timeDifference.TotalSeconds < Config.BLOCKS_UNTIL_DIFFICULTY_SHIFT * Config.TARGET_BLOCK_TIME_SECS * 75 / 100)
                {
                    ++currentDifficulty;
                }
            }

            return true;
        }

        public void AddTx(Tx tx)
        {
            txMempool[tx.id] = tx;
        }

        private void AddToAccount(string account, long amount)
        {
            if (!accountToAmount.ContainsKey(account))
            {
                accountToAmount[account] = 0;
            }
            accountToAmount[account] += amount;
        }

        private long GetMiningReward()
        {
            long reward = Config.INITIAL_REWARD;
            long halvingInterval = Config.HALVING_BLOCKS_NUM;

            // Calculate the number of halvings
            long numHalvings = NumberOfBlocks / halvingInterval;

            // Halve the reward for each halving interval that has passed
            for (long i = 0; i < numHalvings; i++)
            {
                reward /= 2;
            }

            // Ensure the reward does not go below 1
            if (reward < 1)
            {
                reward = 1;
            }

            return reward;
        }

        public bool IsValidTransactionList(List<Tx> txs)
        {
            foreach (Tx t in txs)
            {
                if (!accountToAmount.ContainsKey(t.from) || accountToAmount[t.from] < t.amount)
                {
                    return false;
                }
            }
            return true;
        }

        public Block CreateCandidateBlock()
        {
            Block candidateBlock = new Block(currentBlockHash);

            candidateBlock.txs = txMempool.Take(10).Select(pair => pair.Value).ToList();

            return candidateBlock;
        }

        public BlockchainState Copy()
        {
            var copy = new BlockchainState
            {
                accountToAmount = new Dictionary<string, long>(accountToAmount),
                txMempool = new Dictionary<string, Tx>(txMempool),
                blocks = new Dictionary<long, Block>(),
                currentBlockHash = currentBlockHash,
                currentDifficulty = currentDifficulty,
            };

            // Deep copy the blocks
            foreach (var kvp in blocks)
            {
                copy.blocks[kvp.Key] = kvp.Value.Copy();
            }

            return copy;
        }
    }
}
