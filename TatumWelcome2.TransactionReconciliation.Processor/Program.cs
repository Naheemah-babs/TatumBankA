using Microsoft.Extensions.Hosting;
using TatumWelcome2.BLL;
using TatumWelcome2.BLL.Options;
using TatumWelcome2.Infrastructure;
using TatumWelcome2.TransactionReconciliation.Processor;
using Microsoft.Extensions.DependencyInjection;

// Builds a "generic host": configuration + logging + dependency injection + background services.
// In Development it also loads user secrets (the API's UserSecretsId is shared in the .csproj).
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Same registrations as the API, so IInterbankOutgoingTransferService can be built
// with all its dependencies (repositories, BankApiClient, logger, options).
builder.Services.AddTatumInfrastructure();
builder.Services.AddTatumBusinessLogic();

// Bank B's URL 
builder.Services.Configure<InterBankOptions>(
    builder.Configuration.GetSection(InterBankOptions.SectionName));

// Loop settings.
builder.Services.Configure<ReconciliationOptions>(
    builder.Configuration.GetSection(ReconciliationOptions.SectionName));

// Register the loop itself. The host starts it and stops it on Ctrl+C.
builder.Services.AddHostedService<ReconciliationWorker>();

await builder.Build().RunAsync();