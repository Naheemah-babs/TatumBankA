using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.IServices
{
    public interface ITransactionReferenceGenerator
    {
        /// <summary>Returns a unique reference such as DEP-1A2B....</summary>
        string Generate(string prefix);
    }
}
