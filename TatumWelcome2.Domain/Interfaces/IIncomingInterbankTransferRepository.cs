using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.Domain.Interfaces
{
    public interface IIncomingInterbankTransferRepository
    {
        IncomingTransferWrite ReceiveTransfer(InterbankTransferRequestDto request, string description);

        IncomingTransferOutcome? GetOutcome(string reference);
    }
}
