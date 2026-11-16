using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.BLL.Services;

namespace TatumWelcome2.BLL
{
    public static class BusinessLogicServiceCollectionExtensions
    {
        /// Registers business services. Helpers are stateless singletons;
        /// services are scoped (one per request / reconciliation run) so the
        /// transient typed HttpClient is never captured by a singleton.
        public static IServiceCollection AddTatumBusinessLogic(
            this IServiceCollection services)
        {
            services.AddSingleton<IPinHasher, PinHasher>();
            services.AddSingleton<
                ITransactionReferenceGenerator,
                TransactionReferenceGenerator>();

            // Simulated biller (reads the admin payment mode).
            services.AddSingleton<IBillPaymentProvider, BillPaymentProvider>();

            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IPinMigrationService, PinMigrationService>();
            services.AddSingleton<IPinHasher, PinHasher>();
            services.AddScoped<IAccountCreationService, AccountCreationService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IBillPaymentService, BillPaymentService>();
            services.AddScoped<ICustomerBillPaymentService, CustomerBillPaymentService>();
            services.AddScoped<IInterbankTransferService, InterbankTransferService>();
            services.AddSingleton<ITransactionReferenceGenerator, TransactionReferenceGenerator>();
            services.AddScoped<
                IInterbankOutgoingTransferService,
                InterbankOutgoingTransferService>();

            return services;
        }
    }

}
