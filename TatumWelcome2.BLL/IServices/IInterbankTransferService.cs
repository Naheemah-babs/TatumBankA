using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.IServices
{
    public interface IInterbankTransferService
    {
        IncomingTransferResult ReceiveTransfer(
        InterbankTransferRequestDto request);
        IncomingTransferOutcome? GetOutcome(string reference);
        InterbankTransferRequestDto? FindProcessedTransfer(string reference);
    }
}
