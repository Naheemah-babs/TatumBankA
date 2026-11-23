namespace TatumWelcome2.Domain.Models
{
    public static class AccountLimits
    {
        /// <summary>Largest balance an account may hold (DECIMAL(18, 2)).</summary>
        public const decimal MaximumBalance = 9999999999999999.99m;

        /// <summary>Smallest amount for a deposit, withdrawal, transfer or interbank send.</summary>
        public const decimal MinimumTransactionAmount = 500m;

        /// <summary>Account numbers are stored as NVARCHAR(10).</summary>
        public const int MaximumAccountNumberLength = 10;

        public const string NotWholeNumberMessage =
            "Amount must be a whole number, with no decimal places.";

        public static string OutOfRangeMessage =>
            $"Amount must be at least {MinimumTransactionAmount:N0} and not more than {MaximumBalance:N0}.";

        /// <summary>
        /// Returns why the amount is rejected, or null when it is acceptable.
        /// The whole-number check runs first so 0.01 gets the "whole number" message.
        /// </summary>
        public static TransactionResult? CheckAmount(decimal amount)
        {
            if (decimal.Truncate(amount) != amount)
                return TransactionResult.AmountNotWholeNumber;

            if (amount < MinimumTransactionAmount || amount > MaximumBalance)
                return TransactionResult.AmountOutOfRange;

            return null;
        }
    }
}