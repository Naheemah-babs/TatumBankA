using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.Domain.Models;


namespace TatumWelcome2
{
    public static class AccountHistory
    {
        public static void PrintHistory(Account account)
        {
            if (account.Transactions.Count == 0)
            {
                Console.WriteLine("No transactions yet.");
                return;
            }
            Console.WriteLine($"{"Date",-18}{"Type",-14}{"Amount",12}  Description");
            Console.WriteLine(new string('-', 60));
            foreach (Transaction t in account.Transactions)
            {
                Console.WriteLine($"{t.Date,-18:dd MMM yyyy HH:mm}{t.Type,-14}{t.Amount,12:N2}  {t.Description}");
            }
            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"{"Balance",-32}{account.Balance,12:N2}");
        }
    }
    
}
