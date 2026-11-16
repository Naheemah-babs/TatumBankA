using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.BLL.IServices
{
    public interface IAdminService
    {
        AdminAccountResponseDto? GetAccountSummary(string accountNumber);

        AccountStatusChangeResult Freeze(string accountNumber);

        AccountStatusChangeResult Unfreeze(string accountNumber);
    }
}
