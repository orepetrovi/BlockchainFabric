using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using Utils;
using Utils.Structs;

namespace Miner
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class Miner : StatelessService
    {
        public Miner(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or Node requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            ServiceEventSource.Current.ServiceMessage(this.Context, "Starting worker");

            string myAccountId = HelperMethods.GenerateAccountId();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    INodeInterface? proxy = await HelperMethods.GetRandomNodeProxy();

                    if (proxy == null)
                    {
                        throw new Exception("No valid blockchain manager");
                    }

                    MiningJob currentJob = await proxy.GetMiningJob();

                    bool success;

                    currentJob.candidateBlock.miner = myAccountId;

                    success = DoMining(ref currentJob);

                    if (success)
                    {
                        SubmitMinedBlock(currentJob.candidateBlock);
                    }
                }
                catch (Exception e)
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Miner got exception {0}", e.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        private static bool DoMining(ref MiningJob job)
        {
            DateTime jobBegin = DateTime.UtcNow;

            while (true)
            {
                bool time_exceeded = (DateTime.UtcNow - jobBegin).TotalSeconds > Config.MINING_JOB_TIMEOUT_SECS;
                if (time_exceeded) { break; }

                job.candidateBlock.minedTime = DateTime.UtcNow;
                for (int i = 0; i< Config.HASH_ATTEMPTS_PER_CYCLE; ++i)
                {
                    Random random = new Random();
                    job.candidateBlock.offset = GenerateRandom256BitInteger();
                    if (job.candidateBlock.IsVerified(job.currentDifficulty))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static BigInteger GenerateRandom256BitInteger()
        {
            Random random = new Random();

            byte[] bytes = new byte[Config.HASH_BYTES];

            random.NextBytes(bytes);

            BigInteger bigInteger = new BigInteger(bytes);
            if (bigInteger.Sign < 0)
            {
                bigInteger = BigInteger.Negate(bigInteger);
            }
            return bigInteger;
        }

        private static async void SubmitMinedBlock(Block block)
        {
            List<Task<Boolean>> tasks = new List<Task<bool>>();
            for (int i=0; i < await HelperMethods.GetNodePartitionCount(); ++i)
            {
                INodeInterface proxy = ServiceProxy.Create<INodeInterface>(
                    Config.BLOCKCHAIN_URI,
                    new ServicePartitionKey(i.ToString()));

                tasks.Add(proxy.SubmitMinedBlock(block));
            }
        }
    }
}
