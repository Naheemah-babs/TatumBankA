using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.IServices
{
    public interface IBillPaymentProvider
    {
        BillProviderDecision Authorize(BillPaymentRequest request);
    }
}
