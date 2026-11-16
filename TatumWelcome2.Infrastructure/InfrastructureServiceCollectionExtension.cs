using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.Services;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Infrastructure.Database;
using TatumWelcome2.Infrastructure.Http;
using TatumWelcome2.Infrastructure.Repositories;

namespace TatumWelcome2.Infrastructure
{
    public static class InfrastructureServiceCollectionExtensions
    {
        /// <summary>
        /// Registers MySQL repositories and the bank-to-bank HTTP client.
        /// Requires ConnectionStrings:DefaultConnection and Interbank:ApiKey.
        /// </summary>
        public static IServiceCollection AddTatumInfrastructure(
            this IServiceCollection services)
        {
            // Stateless: each call opens its own connection.
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            services.AddSingleton<IAccountRepository, AccountRepository>();
            services.AddSingleton<IBillPaymentRepository, BillPaymentRepository>();
            services.AddSingleton<IIncomingInterbankTransferRepository, IncomingInterbankTransferRepository>();
            services.AddSingleton<IInterbankOutgoingTransferRepository, OutgoingInterbankTransferRepository>();
            
            // Typed client: transient by design.
            services.AddHttpClient<IBankApiClient, BankApiClient>();

            return services;
        }
    }

}
