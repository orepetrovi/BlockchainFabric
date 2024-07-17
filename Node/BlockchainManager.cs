using System.Fabric;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Data;
using Utils;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using System.Data;
using Utils.Structs;
using System.Fabric.Description;
using System.Collections.ObjectModel;
using static System.Reflection.Metadata.BlobBuilder;

namespace Node
{
    internal sealed class Node : StatefulService, INodeInterface
    {
        public Node(StatefulServiceContext context)
            : base(context)
        { }

        private string? mainChainHash = null;
        private int mainChainBlockNumber = 0;
        static int miningJobsSinceLastRun = 0;
        DateTime? lastRun = null;

        private async Task<BlockchainState> GetBlockchainState()
        {
            string mainChainHash = await GetMainChainHash();

            IReliableDictionary<string, BlockchainState> blockchainStateDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, BlockchainState>>(Config.BLOCKCHAIN_STATE_DICT_NAME);

            using var tx = StateManager.CreateTransaction();
            ConditionalValue<BlockchainState> conditionalValue = await blockchainStateDictionary.TryGetValueAsync(tx, mainChainHash);
            if (!conditionalValue.HasValue)
            {
                throw new Exception("Blockchain state doesn't exist");
            }
            await tx.CommitAsync();
            return conditionalValue.Value.Copy();
        }

        private async Task<string> GetMainChainHash()
        {
            if (mainChainHash != null)
            {
                return mainChainHash;
            }

            IReliableDictionary<string, BlockchainState> blockchainStateDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, BlockchainState>>(Config.BLOCKCHAIN_STATE_DICT_NAME, TimeSpan.FromSeconds(10));

            using (var tx = StateManager.CreateTransaction())
            {
                await blockchainStateDictionary.GetOrAddAsync(tx, "", new BlockchainState());
                await tx.CommitAsync();
            }

            using (var tx = StateManager.CreateTransaction()) {

                var enumerable = await blockchainStateDictionary.CreateEnumerableAsync(tx);
                var enumerator = enumerable.GetAsyncEnumerator();

                mainChainHash = "";
                mainChainBlockNumber = 0;
                while (await enumerator.MoveNextAsync(default))
                {
                    KeyValuePair<string, BlockchainState> current = enumerator.Current;

                    if (current.Value.NumberOfBlocks > mainChainBlockNumber)
                    {
                        mainChainHash = current.Key;
                        mainChainBlockNumber = current.Value.NumberOfBlocks;
                    }
                }

                await tx.CommitAsync();
            }
            return mainChainHash;
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        public async Task<MiningJob> GetMiningJob()
        {
            Interlocked.Increment(ref miningJobsSinceLastRun);
            BlockchainState blockchainState = await GetBlockchainState();

            return new MiningJob(blockchainState.CreateCandidateBlock(), blockchainState.currentDifficulty);
        }

        public async Task<Boolean> SubmitMinedBlock(Block block)
        {
            var blockchainStateDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, BlockchainState>>(Config.BLOCKCHAIN_STATE_DICT_NAME);
            using var tx = StateManager.CreateTransaction();

            return await SubmitMinedBlockHelper(block, blockchainStateDictionary, tx);
        }

        public async Task<Boolean> SubmitMinedBlockHelper(Block block, IReliableDictionary<string, BlockchainState> blockchainStateDictionary, ITransaction tx)
        {
            if (await blockchainStateDictionary.ContainsKeyAsync(tx, block.GetHash()))
            {
                tx.Abort();
                return false;
            }

            ConditionalValue<BlockchainState> parentBlockCond;
            if (block.parentHash == "")
            {
                parentBlockCond = new ConditionalValue<BlockchainState>(true, new BlockchainState());
            } else
            {
                parentBlockCond = await blockchainStateDictionary.TryGetValueAsync(tx, block.parentHash);
            }

            if (!parentBlockCond.HasValue)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Parent state doesn't exist {0}", block.parentHash);
                tx.Abort();
                return false;
            }

            BlockchainState oldblockchainState = parentBlockCond.Value;
            BlockchainState blockchainState = oldblockchainState.Copy();

            if (!blockchainState.TryApply(ref block))
            {
                ServiceEventSource.Current.ServiceMessage(Context, "Can't apply block to current state, block: {0}, state: {1}", HelperMethods.ToString(block), HelperMethods.ToString(blockchainState));
                tx.Abort();
                return false;
            }

            bool success = await blockchainStateDictionary.TryAddAsync(tx, blockchainState.currentBlockHash, blockchainState);

            if (!success) {
                ServiceEventSource.Current.ServiceMessage(Context, "New blockchain state couln't be added to dictionary", HelperMethods.ToString(blockchainState));
                tx.Abort();
                return false;
            }

            if (mainChainBlockNumber < blockchainState.NumberOfBlocks)
            {
                if (mainChainHash != blockchainState.LastBlock.parentHash)
                {
                    ServiceEventSource.Current.ServiceMessage(Context, "Switched main chain!", HelperMethods.ToString(blockchainState));
                }
                mainChainBlockNumber = blockchainState.NumberOfBlocks;
                mainChainHash = blockchainState.currentBlockHash;
            }

            await tx.CommitAsync();
            return true;
        }

        public async Task<Dictionary<long, Block>> Get100Blocks()
        {
            Dictionary<long, Block> retDict = new Dictionary<long, Block>();

            BlockchainState blockchainState = await GetBlockchainState();

            for (int i = blockchainState.NumberOfBlocks - 1; i >= Math.Max(0, blockchainState.NumberOfBlocks - 100); --i)
            {
                retDict[i] = blockchainState.blocks[i];
            }
            return retDict;
        }

        public async Task<Dictionary<long, Block>> GetAllBlocks()
        {
            Dictionary<long, Block> retDict = new Dictionary<long, Block>();

            BlockchainState blockchainState = await GetBlockchainState();

            for (int i = blockchainState.NumberOfBlocks - 1; i >= 0; --i)
            {
                retDict[i] = blockchainState.blocks[i];
            }
            return retDict;
        }


        public async Task<BlockchainStateSummary> GetStateSummary()
        {
            BlockchainState blockchainState = await GetBlockchainState();

            var ret = new BlockchainStateSummary();

            ret.TopAccounts = blockchainState.accountToAmount
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToList();

            var last100Blocks = blockchainState.blocks.Values
                .OrderByDescending(b => b.minedTime)
                .Take(100);

            if (last100Blocks.Count() > 1)
            {
                int elapsedTimeSecs = (int)(last100Blocks.First().minedTime - last100Blocks.Last().minedTime).TotalSeconds;

                if (elapsedTimeSecs > 0)
                {
                    ret.MinerHashRates = last100Blocks
                        .GroupBy(b => b.miner)
                        .Select(g => new KeyValuePair<string, long>(g.Key, g.Sum(b => (long)(1) << HelperMethods.CountLeadingZeros(b.GetHash())) / elapsedTimeSecs))
                        .ToList();
                }
            }

            ret.LastBlocks = blockchainState.blocks.Values
                .OrderByDescending(b => b.minedTime)
                .Take(5)
                .ToList();

            ret.CurrentBlockHash = blockchainState.currentBlockHash;
            ret.CurrentDifficulty = blockchainState.currentDifficulty;
            ret.NumberOfBlocks = blockchainState.NumberOfBlocks;

            return ret;
        }

        public async Task<Boolean> CreateTransaction(string from, string to, long amount)
        {
            string mainChainHash = await GetMainChainHash();

            Tx t = new Tx(from, to, amount);

            if (!t.IsValid())
            {
                return false;
            }

            IReliableDictionary<string, BlockchainState> blockchainStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, BlockchainState>>(Config.BLOCKCHAIN_STATE_DICT_NAME);

            using (var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<BlockchainState> conditionalValue = await blockchainStateDictionary.TryGetValueAsync(tx, mainChainHash, LockMode.Update);

                if (!conditionalValue.HasValue)
                {
                    return false;
                }

                BlockchainState oldBlockchainState = conditionalValue.Value;
                BlockchainState blockchainState = conditionalValue.Value.Copy();

                blockchainState.AddTx(t);
                bool success = await blockchainStateDictionary.TryUpdateAsync(tx, mainChainHash, blockchainState, oldBlockchainState);
                if (!success)
                {
                    return false;
                }

                await tx.CommitAsync();
            }
            return true;
        }

        private void SetMetrics()
        {
            int jobs = Interlocked.Exchange(ref miningJobsSinceLastRun, 0);

            DateTime currentTime = DateTime.UtcNow;

            if (lastRun != null)
            {
                double totalMilliseconds = (currentTime - lastRun.Value).TotalMilliseconds;
                if (totalMilliseconds > Config.SYNC_NODE_PERIOD_SECS * 1000)
                {
                    int jobsPerSecond = (int)Math.Round(1000.0 * jobs / totalMilliseconds);
                    var loadMetrics = new List<LoadMetric>
                    {
                        new LoadMetric("GetMiningJobPerSecond", jobsPerSecond)
                    };
                    Partition.ReportLoad(loadMetrics);
                }
            }

            lastRun = currentTime;
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            StatefulServiceUpdateDescription serviceDescription = new StatefulServiceUpdateDescription();

            {
                StatefulServiceLoadMetricDescription connectionMetric = new StatefulServiceLoadMetricDescription
                {
                    Name = "GetMiningJobPerSecond",
                    PrimaryDefaultLoad = 0,
                    SecondaryDefaultLoad = 0,
                    Weight = ServiceLoadMetricWeight.High
                };

                if (serviceDescription.Metrics == null)
                {
                    serviceDescription.Metrics = new Metrics();
                }

                serviceDescription.Metrics.Add(connectionMetric);
            }

            {
                AverageServiceLoadScalingTrigger trigger = new AverageServiceLoadScalingTrigger();
                AddRemoveIncrementalNamedPartitionScalingMechanism mechanism = new AddRemoveIncrementalNamedPartitionScalingMechanism();
                mechanism.MaxPartitionCount = 5;
                mechanism.MinPartitionCount = 1;
                mechanism.ScaleIncrement = 1;

                trigger.MetricName = "GetMiningJobPerSecond";
                trigger.ScaleInterval = TimeSpan.FromMinutes(10);
                trigger.LowerLoadThreshold = 2;
                trigger.UpperLoadThreshold = 5;
                trigger.UseOnlyPrimaryLoad = true;
                ScalingPolicyDescription policy = new ScalingPolicyDescription(mechanism, trigger);
                serviceDescription.ScalingPolicies = new List<ScalingPolicyDescription>();
                serviceDescription.ScalingPolicies.Add(policy);
            }

            FabricClient fabricClient = new FabricClient();
            await fabricClient.ServiceManager.UpdateServiceAsync(Config.BLOCKCHAIN_URI, serviceDescription);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(Config.SYNC_NODE_PERIOD_SECS * 1000);
                SyncWithRandomNode(await HelperMethods.GetRandomNodeProxy(Context.PartitionId));
                RemoveOldStates();
                SetMetrics();
            }
        }

        private async void SyncWithRandomNode(INodeInterface? proxy)
        {
            try
            {
                if (proxy == null)
                {
                    return;
                }
                bool success = await SyncWithRandomNodeHelper(proxy, await proxy.Get100Blocks());
                if (!success)
                {
                    await SyncWithRandomNodeHelper(proxy, await proxy.GetAllBlocks());
                }
            }
            catch (Exception) { }
        }

        private async Task<bool> SyncWithRandomNodeHelper(INodeInterface? proxy, Dictionary<long, Block> blocks)
        {
            BlockchainState mainChainState = await GetBlockchainState();

            var sortedBlocks = blocks.OrderBy(kvp => kvp.Key);

            bool lastBlockSuccess = true;
            
            {
                var blockchainStateDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, BlockchainState>>(Config.BLOCKCHAIN_STATE_DICT_NAME);

                foreach (var block in sortedBlocks)
                {
                    using (var tx = StateManager.CreateTransaction())
                    {
                        if (!IsOldBlock(block.Value.number - 1, mainChainState) && !await blockchainStateDictionary.ContainsKeyAsync(tx, block.Value.GetHash()))
                        {
                            lastBlockSuccess = await SubmitMinedBlockHelper(block.Value, blockchainStateDictionary, tx);
                            if (!lastBlockSuccess)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return lastBlockSuccess;
        }

        private bool IsOldBlock(long blockNumber, BlockchainState mainChainState)
        {
            return blockNumber < mainChainState.NumberOfBlocks - Config.NUMBER_OF_CONFIRMATIONS - 1;
        }

        private async void RemoveOldStates()
        {
            try
            {
                BlockchainState mainChainState = await GetBlockchainState();

                IReliableDictionary<string, BlockchainState> blockchainStateDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, BlockchainState>>(Config.BLOCKCHAIN_STATE_DICT_NAME);

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var enumerable = await blockchainStateDictionary.CreateEnumerableAsync(tx, EnumerationMode.Unordered);
                    var enumerator = enumerable.GetAsyncEnumerator();

                    while (await enumerator.MoveNextAsync(default))
                    {
                        BlockchainState state = enumerator.Current.Value;

                        if (IsOldBlock(state.LastBlock.number, mainChainState))
                        {
                            await blockchainStateDictionary.TryRemoveAsync(tx, state.currentBlockHash);
                        }
                    }
                    await tx.CommitAsync();
                }
            } catch(Exception) { }
        }

        private class Metrics : KeyedCollection<string, ServiceLoadMetricDescription>
        {
            protected override string GetKeyForItem(ServiceLoadMetricDescription item)
            {
                return item.Name;
            }
        }
    }
}
