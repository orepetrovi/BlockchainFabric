using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Fabric.Query;
using System.Fabric;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utils.Structs;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using System.ComponentModel;
using Microsoft.VisualBasic;

namespace Utils
{
    public class HelperMethods
    {

        public static string GenerateAccountId()
        {
            return GenerateRandomString(Config.HASH_BYTES);
        }

        public static bool IsValidAccountId(string accountId)
        {
            return accountId.Length == Config.HASH_BYTES;
        }

        public static string GenerateTxId()
        {
            return GenerateRandomString(Config.HASH_BYTES);
        }

        public static bool IsValidTxId(string id)
        {
            return id.Length == Config.HASH_BYTES;
        }
        
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static int CountLeadingZeros(string hex)
        {
            int count = 0;
            foreach (char c in hex)
            {
                string binary = Convert.ToString(Convert.ToInt32(c.ToString(), 16), 2).PadLeft(4, '0');
                foreach (char b in binary)
                {
                    if (b == '0')
                    {
                        count++;
                    }
                    else
                    {
                        return count;
                    }
                }
            }
            return count;
        }

        public static string ToString<T>(T structInstance)
        {
            Type type = typeof(T);
            StringBuilder result = new StringBuilder();

            // Get all fields
            FieldInfo[] fields = type.GetFields();
            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(structInstance);
                string valueStr = "";
                if (value is Block)
                {
                    valueStr = ToString((Block)value);
                } else if (value is Block)
                {
                    valueStr = ToString((Block)value);
                } else if (value is Tx)
                {
                    valueStr = ToString((Tx)value);
                } else if (value is IDictionary)
                {
                    foreach (var key in ((IDictionary)value).Keys)
                    {
                        valueStr += $"{key}: {((IDictionary)value)[key]}, ";
                    }
                    if (valueStr.Length > 0)
                    {
                        valueStr = valueStr.Substring(0, valueStr.Length - 2); // Remove the last comma and space
                    }
                } else
                {
                        valueStr = value.ToString();
                }
                result.Append($"{field.Name} (Field): {valueStr}, ");
            }

            // Get all properties
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object value = property.GetValue(structInstance);
                result.Append($"{property.Name} (Property): {value}, ");
            }

            // Remove the last comma and space
            if (result.Length > 0)
            {
                result.Length -= 2;
            }

            return result.ToString();
        }

        public static async Task<INodeInterface?> GetRandomNodeProxy(Guid? currentPartitionId = null)
        {
            // Create a fabric client to query the partitions
            var fabricClient = new FabricClient();

            // Retrieve all partitions for this service
            ServicePartitionList partitionList = await fabricClient.QueryManager.GetPartitionListAsync(Config.BLOCKCHAIN_URI);

            // Filter out the current partition
            var otherPartitions = partitionList
                .Where(p => p.PartitionInformation.Id != currentPartitionId)
                .ToList();

            if (!otherPartitions.Any())
            {
                return null;
            }

            // Select a random partition from the remaining ones
            Random random = new Random();
            int randomIndex = random.Next(otherPartitions.Count);
            var randomPartition = otherPartitions[randomIndex];

            ServicePartitionKey partitionKey = new ServicePartitionKey(((NamedPartitionInformation)randomPartition.PartitionInformation).Name);
            
            return ServiceProxy.Create<INodeInterface>(Config.BLOCKCHAIN_URI, partitionKey);
        }

        public static async Task<int> GetNodePartitionCount()
        {
            return (await new FabricClient().QueryManager.GetPartitionListAsync(Config.BLOCKCHAIN_URI)).Count;
        }
    }
}

