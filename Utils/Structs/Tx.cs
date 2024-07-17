using System.Security.Cryptography;
using System.Text;

namespace Utils.Structs
{
    [Serializable]
    public struct Tx
    {
        public string from;
        public string to;
        public long amount;
        public string id;

        public Tx(string from, string to, long amount)
        {
            this.from = from;
            this.to = to;
            this.amount = amount;
            id = HelperMethods.GenerateTxId();
        }

        public string GetHash()
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes($"{from}{to}{amount}"));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public bool IsValid()
        {
            return HelperMethods.IsValidAccountId(from) && HelperMethods.IsValidAccountId(to) && HelperMethods.IsValidTxId(id);
        }
    }
}
