
using TatumWelcome2.BLL;
using TatumWelcome2.DTOs;

namespace TatumWelcome2.Models;

public class Account
{
    
    private static readonly object BalanceLock = new();

    public string Name { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    private decimal _balance;

    public decimal Balance
    {
        get
        {
            lock (BalanceLock)
            {
                return _balance;
            }
        }
        private set
        {
            lock (BalanceLock)
            {
                _balance = value;
            }
        }
    }

    private readonly List<Transaction> _transactions = new();

    public List<Transaction> Transactions
    {
        get
        {
            lock (BalanceLock)
            {
                return new List<Transaction>(_transactions);
            }
        }
        private set
        {
            lock (BalanceLock)
            {
                _transactions.Clear();
                _transactions.AddRange(value);
            }
        }
    }

    private bool _isFrozen;

    public bool IsFrozen
    {
        get
        {
            lock (BalanceLock)
            {
                return _isFrozen;
            }
        }
        private set
        {
            lock (BalanceLock)
            {
                _isFrozen = value;
            }
        }
    }

    public bool Deposit(decimal amount, string description)
    {
        lock (BalanceLock)
        {
            if (_isFrozen || amount <= 0)
            {
                return false;
            }

            decimal newBalance;

            try
            {
                newBalance = checked(_balance + amount);
            }
            catch (OverflowException)
            {
                return false;
            }

            Transaction transaction = new Transaction
            {
                Type = TransactionType.Deposit,
                Amount = amount,
                Date = DateTime.Now,
                Description = description
            };

            _transactions.Add(transaction);
            _balance = newBalance;

            return true;
        }
    }

    public TransactionResult CheckWithdraw(decimal amount)
    {
        lock (BalanceLock)
        {
            return CheckWithdrawCore(amount);
        }
    }

    private TransactionResult CheckWithdrawCore(decimal amount)
    {
        if (_isFrozen)
        {
            return TransactionResult.AccountFrozen;
        }

        if (amount <= 0)
        {
            return TransactionResult.InvalidAmount;
        }

        if (amount > _balance)
        {
            return TransactionResult.InsufficientFunds;
        }

        return TransactionResult.Success;
    }

    public TransactionResult Withdraw(
        decimal amount,
        string description)
    {
        lock (BalanceLock)
        {
            TransactionResult check = CheckWithdrawCore(amount);

            if (check != TransactionResult.Success)
            {
                return check;
            }

            Transaction transaction = new Transaction
            {
                Type = TransactionType.Withdrawal,
                Amount = amount,
                Date = DateTime.Now,
                Description = description
            };

            _transactions.Add(transaction);
            _balance -= amount;

            return TransactionResult.Success;
        }
    }





    public TransactionResult ExecuteBillPayment(
        decimal amount,
        string reference,
        string description,
        Func<bool> approvePayment)
    {
        lock (BalanceLock)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return TransactionResult.PaymentFailed;

            bool alreadyDebited = _transactions.Any(
                transaction =>
                    transaction.Type == TransactionType.Withdrawal &&
                    transaction.Reference == reference);

            if (alreadyDebited)
            {
                // Verify the repeated request with the provider.
                // The provider rejects references reused with different details.
                bool originalPaymentApproved = approvePayment();

                return originalPaymentApproved
                    ? TransactionResult.Success
                    : TransactionResult.PaymentFailed;
            }

            TransactionResult check = CheckWithdrawCore(amount);

            if (check != TransactionResult.Success)
                return check;

            if (!approvePayment())
                return TransactionResult.PaymentFailed;

            Transaction transaction = new Transaction
            {
                Type = TransactionType.Withdrawal,
                Amount = amount,
                Date = DateTime.Now,
                Description = description,
                Reference = reference
            };

            _transactions.Add(transaction);
            _balance -= amount;

            return TransactionResult.Success;
        }
    }

    public TransactionResult TransferOut(
        decimal amount,
        string description,
        string? reference = null)
    {
        lock (BalanceLock)
        {
            TransactionResult check = CheckWithdrawCore(amount);

            if (check != TransactionResult.Success)
            {
                return check;
            }

            Transaction transaction = new Transaction
            {
                Type = TransactionType.TransferOut,
                Amount = amount,
                Date = DateTime.Now,
                Description = description,
                Reference = reference
            };

            _transactions.Add(transaction);
            _balance -= amount;

            return TransactionResult.Success;
        }
    }

    public bool TransferIn(
        decimal amount,
        string description,
        string? reference = null)
    {
        lock (BalanceLock)
        {
            if (amount <= 0)
            {
                return false;
            }

            decimal newBalance;

            try
            {
                newBalance = checked(_balance + amount);
            }
            catch (OverflowException)
            {
                return false;
            }

            Transaction transaction = new Transaction
            {
                Type = TransactionType.TransferIn,
                Amount = amount,
                Date = DateTime.Now,
                Description = description,
                Reference = reference
            };

            _transactions.Add(transaction);
            _balance = newBalance;

            return true;
        }
    }

    
    public static TransactionResult TransferBetween(
        Account sender,
        Account recipient,
        decimal amount)
    {
        lock (BalanceLock)
        {
            if (ReferenceEquals(sender, recipient) ||
                sender.AccountNumber == recipient.AccountNumber)
            {
                return TransactionResult.SameAccount;
            }

            TransactionResult check =
                sender.CheckWithdrawCore(amount);

            if (check != TransactionResult.Success)
            {
                return check;
            }

            decimal recipientNewBalance;

            try
            {
                recipientNewBalance =
                    checked(recipient._balance + amount);
            }
            catch (OverflowException)
            {
                return TransactionResult.InvalidAmount;
            }

            DateTime transactionDate = DateTime.Now;
            string reference = Guid.NewGuid().ToString("N");

            Transaction outgoingTransaction = new Transaction
            {
                Type = TransactionType.TransferOut,
                Amount = amount,
                Date = transactionDate,
                Description =
                    $"Transfer to {recipient.Name} " +
                    $"({recipient.AccountNumber})",
                Reference = reference
            };

            Transaction incomingTransaction = new Transaction
            {
                Type = TransactionType.TransferIn,
                Amount = amount,
                Date = transactionDate,
                Description =
                    $"Transfer from {sender.Name} " +
                    $"({sender.AccountNumber})",
                Reference = reference
            };

           
            sender._transactions.Add(outgoingTransaction);

            try
            {
                recipient._transactions.Add(incomingTransaction);
            }
            catch
            {
                sender._transactions.Remove(outgoingTransaction);
                throw;
            }

            
            sender._balance -= amount;
            recipient._balance = recipientNewBalance;

            return TransactionResult.Success;
        }
    }

    public void Freeze()
    {
        lock (BalanceLock)
        {
            _isFrozen = true;
        }
    }

    public void Unfreeze()
    {
        lock (BalanceLock)
        {
            _isFrozen = false;
        }
    }


    public void LoadFromDatabase(
        decimal balance,
        bool isFrozen,
        IEnumerable<Transaction> transactions)
    {
        lock (BalanceLock)
        {
            _balance = balance;
            _isFrozen = isFrozen;

            _transactions.Clear();
            _transactions.AddRange(transactions);
        }
    }

}
