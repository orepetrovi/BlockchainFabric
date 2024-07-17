using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Remoting;
using System;
using Utils.Structs;

namespace Utils
{
    public interface INodeInterface : IService
    {
        Task<MiningJob> GetMiningJob();

        Task<Boolean> SubmitMinedBlock(Block block);

        Task<Dictionary<long, Block>> Get100Blocks();

        Task<Dictionary<long, Block>> GetAllBlocks();

        Task<BlockchainStateSummary> GetStateSummary();

        Task<Boolean> CreateTransaction(string from, string to, long amount);
    }
}
