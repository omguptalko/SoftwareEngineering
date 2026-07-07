using HIS.Application.Abstractions;
using HIS.Infrastructure.Persistence;
using HIS.Infrastructure.Platform;
using HIS.Infrastructure.Security;
using HIS.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace HIS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers Dapper-backed persistence. Connection string resolved from config.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // L1.8.5: the single-DB SqlConnectionFactory is retired — all data access now flows
        // through ITenantConnectionFactory (per-tenant master / current-FY DB).
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuditQueryRepository, AuditQueryRepository>();

        // L1 control plane (HIS_Platform): identity + auth.
        services.AddSingleton<IPlatformConnectionFactory, PlatformConnectionFactory>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IModuleAdminRepository, ModuleAdminRepository>();
        services.AddScoped<ITenantAdminRepository, TenantAdminRepository>();
        services.AddSingleton<IProvisioningEngine, SqlProvisioningEngine>();
        services.AddScoped<ITenantConnectionFactory, TenantConnectionFactory>();
        services.AddScoped<ITenantScopedRepository, TenantScopedRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IFieldProtector, AesGcmFieldProtector>();
        services.AddScoped<IModuleRegistryRepository, ModuleRegistryRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<IAdmissionRepository, AdmissionRepository>();
        services.AddScoped<IEmergencyRepository, EmergencyRepository>();
        services.AddScoped<IIcuRepository, IcuRepository>();
        services.AddScoped<INursingRepository, NursingRepository>();
        services.AddScoped<IOtRepository, OtRepository>();
        services.AddScoped<ILisRepository, LisRepository>();
        services.AddScoped<IRadiologyRepository, RadiologyRepository>();
        services.AddScoped<IBloodBankRepository, BloodBankRepository>();
        services.AddScoped<IPharmacyRepository, PharmacyRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<IClaimsRepository, ClaimsRepository>();
        services.AddScoped<IPmjayRepository, PmjayRepository>();
        services.AddScoped<ISchemeRepository, SchemeRepository>();
        services.AddScoped<IHrRepository, HrRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<IOccHealthRepository, OccHealthRepository>();
        services.AddScoped<ITelemedicineRepository, TelemedicineRepository>();
        services.AddScoped<IAmbulanceRepository, AmbulanceRepository>();
        services.AddScoped<IStatutoryRepository, StatutoryRepository>();
        services.AddScoped<IExperienceRepository, ExperienceRepository>();
        return services;
    }
}
