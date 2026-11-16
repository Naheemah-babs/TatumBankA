using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.Services
{
    public static class TransactionDescriptions
    {
        public const string OpeningDeposit = "Opening deposit";
        public const string CashDeposit = "Cash deposit";
        public const string CashWithdrawal = "Cash withdrawal";

        public static string TransferTo(string recipientName, string recipientNumber) =>
            $"Transfer to {recipientName} ({recipientNumber})";

        public static string TransferFrom(string senderName, string senderNumber) =>
            $"Transfer from {senderName} ({senderNumber})";

        public static string InterbankTransferTo(string recipientNumber) =>
            $"Interbank transfer to {recipientNumber}";

        public static string InterbankTransferFrom(string senderNumber) =>
            $"Interbank transfer from {senderNumber}";

        public static string InterbankReversal(string reference) =>
            $"Reversal of rejected interbank transfer {reference}";

        public static string Airtime(string network, string phoneNumber, string reference) =>
            $"Airtime {network} {phoneNumber} Ref: {reference}";

        public static string Data(string network, string planName, string phoneNumber, string reference) =>
            $"Data {network} {planName} {phoneNumber} Ref: {reference}";
    }
}
