using HIS.Application.Abstractions;
using HIS.Infrastructure.Persistence;
using HIS.Infrastructure.Platform;
using HIS.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace HIS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers Dapper-backed persistence. Connection string resolved from config.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        // L1 control plane (HIS_Platform): identity + auth.
        services.AddSingleton<IPlatformConnectionFactory, PlatformConnectionFactory>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IModuleAdminRepository, ModuleAdminRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IModuleRegistryRepository, ModuleRegistryRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<IAdmissionRepository, AdmissionRepository>();
        services.AddScoped<IEmergencyRepository, EmergencyRepository>();
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
