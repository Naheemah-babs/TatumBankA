using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public enum IncomingTransferResult
    {
        Success,
        AlreadyProcessed,
        Rejected,
        Pending,
        AccountNotFound,
        ReferenceConflict,
        InvalidRequest
    }
}
