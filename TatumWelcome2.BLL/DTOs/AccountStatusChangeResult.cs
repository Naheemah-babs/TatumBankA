using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public enum AccountStatusChangeResult
    {
        Changed,
        AlreadyInState,
        AccountNotFound,

        ///<summary> The row changed concurrently; nothing was updated.</summary>
        UpdateFailed
    }
}
