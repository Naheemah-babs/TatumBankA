namespace TatumWelcome2.Domain.Models;

    public class Transaction
    {
        public TransactionType Type { get; set; }

        public decimal Amount { get; set; }

        public DateTime Date { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? Reference { get; set; }
    }

