using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public sealed class PinMigrationService : IPinMigrationService
{
    private readonly IAccountRepository _repository;
    private readonly IPinHasher _pinHasher;

    public PinMigrationService(
        IAccountRepository repository,
        IPinHasher pinHasher)
    {
        _repository = repository;
        _pinHasher = pinHasher;
    }

    public int HashLegacyPins()
    {
        int updated = 0;

        foreach (LegacyPin legacy in _repository.GetLegacyPins())
        {
            // Guarded on the old value, so a concurrent change is never overwritten.
            bool replaced = _repository.ReplacePinHash(
                legacy.AccountNumber,
                legacy.StoredValue,
                _pinHasher.Hash(legacy.StoredValue));

            if (replaced)
                updated++;
        }

        return updated;
    }
}
