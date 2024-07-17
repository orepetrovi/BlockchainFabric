namespace Dashboard.Models
{
    public class TransferModel
    {
        public string From { get; set; }
        public string To { get; set; }
        public long Amount { get; set; }
        public bool? Success { get; set; }
    }
}
