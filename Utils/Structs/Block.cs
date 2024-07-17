using System.Text;
using System.Security.Cryptography;
using System.Numerics;

namespace Utils.Structs
{
    [Serializable]
    public struct Block
    {
        public List<Tx> txs;
        public string parentHash;
        public string miner;
        public long number;
        public BigInteger offset;
        public DateTime minedTime;

        public Block(string parentHash)
        {
            txs = new List<Tx>();
            this.parentHash = parentHash;
            miner = "";
            number = 0;
            offset = BigInteger.Zero;
            minedTime = DateTime.MinValue;
        }

        public Block(Block other)
        {
            txs = new List<Tx>(other.txs);
            parentHash = other.parentHash;
            miner = other.miner;
            number = other.number;
            offset = other.offset;
            minedTime = other.minedTime;
        }

        public string GetHash()
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Combine the properties into a single string
                string rawData = $"{parentHash}{miner}{offset}{minedTime}{string.Join("", txs)}";
                // Compute the hash of the rawData
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public bool IsVerified(int difficulty)
        {
            string hash = GetHash();

            return HelperMethods.CountLeadingZeros(hash) >= difficulty;
        }

        public bool IsValid()
        {
            if (!HelperMethods.IsValidAccountId(miner))
            {
                return false;
            }
            foreach (Tx tx in txs)
            {
                if (!tx.IsValid())
                {
                    return false;
                }
            }

            return true;
        }

        public Block Copy()
        {
            return new Block(this);
        }
    }
}
