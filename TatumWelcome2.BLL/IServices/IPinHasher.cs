using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.IServices
{
    public interface IPinHasher
    {
        /// <summary>Returns a PBKDF2 hash in the form PBKDF2$iterations$salt$hash.</summary>
        string Hash(string pin);

        /// <summary>Verifies a PIN against a PBKDF2 hash. Any other format fails.</summary>
        bool Verify(string pin, string storedHash);
    }
}
