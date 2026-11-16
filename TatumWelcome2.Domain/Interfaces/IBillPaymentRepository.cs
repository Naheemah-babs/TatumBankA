using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.Domain.Interfaces
{
    public interface IBillPaymentRepository
    {
        string GetPaymentMode();

        void SetPaymentMode(string mode);

        /// <summary>
        /// Provider side: reserves the reference and records the decision.
        /// Returns AlreadyExists (with the stored row) for a known reference.
        /// </summary>
        BillPaymentWrite ProcessPayment(
            BillPaymentRequest request,
            BillProviderDecision decision);

        /// <summary>
        /// Customer purchase in one transaction. Under the account lock: account
        /// exists, PIN, reference unused, not frozen, sufficient funds. Debits
        /// only when <paramref name="decision"/> is approved, then records it.
        /// </summary>
        CustomerPurchaseWrite PurchaseForCustomer(
            BillPaymentRequest request,
            string pin,
            TransactionType transactionType,
            string description,
            BillProviderDecision decision);
    }
}
