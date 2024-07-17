using Dashboard.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System.Diagnostics;
using Utils;

namespace Dashboard.Controllers
{
    public class HomeController : Controller
    {

        private async Task<INodeInterface> GetNode()
        {
            int partition = 0;
            if (Request.Cookies.ContainsKey(Config.COOKIE_PARTITION_NUMBER_KEY))
            {
                int.TryParse(Request.Cookies[Config.COOKIE_PARTITION_NUMBER_KEY], out partition);
            }
            if (partition >= await HelperMethods.GetNodePartitionCount())
            {
                partition = 0;
            }
            return ServiceProxy.Create<INodeInterface>(
                Config.BLOCKCHAIN_URI,
                new ServicePartitionKey(partition.ToString()));
        }

        private async Task<List<INodeInterface>> GetAllNodes()
        {
            List<INodeInterface> result = new List<INodeInterface>();
            for (int i=0; i < await HelperMethods.GetNodePartitionCount(); ++i)
            {
                result.Add(ServiceProxy.Create<INodeInterface>(
                    Config.BLOCKCHAIN_URI,
                    new ServicePartitionKey(i.ToString())));
            }
            return result;
        }

        public async Task<IActionResult> Index()
        {
            // Call the Service Fabric service and get data
            var stateSummary = await (await GetNode()).GetStateSummary();

            // Pass the data to the view
            return View(stateSummary);
        }

        public async Task<IActionResult> Blocks()
        {
            var blocks = await (await GetNode()).Get100Blocks();
            return View(blocks);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public IActionResult Transfer()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Transfer(TransferModel model)
        {
            if (ModelState.IsValid)
            {
                // Call the Service Fabric stateful service and get the result

                bool result = false;
                foreach (var blockchainManager in await GetAllNodes())
                {
                    result |= await blockchainManager.CreateTransaction(model.From, model.To, model.Amount);
                }
                model.Success = result;
            }

            return View(model);
        }
    }
}
