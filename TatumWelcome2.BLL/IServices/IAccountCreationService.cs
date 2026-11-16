using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.BLL.IServices
{
    public interface IAccountCreationService
    {
        CreateAccountResultDto CreateAccount(CreateAccountRequestDto request);
    }
}
