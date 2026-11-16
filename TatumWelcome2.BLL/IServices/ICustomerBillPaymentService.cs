using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.BLL.IServices
{
    public interface ICustomerBillPaymentService
    {
        public BillPurchaseResult BuyAirtime(
        string accountNumber,
        AirtimePurchaseRequestDto request);
        public BillPurchaseResult BuyData(
        string accountNumber,
        DataPurchaseRequestDto request);
    }
}
