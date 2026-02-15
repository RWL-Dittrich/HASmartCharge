var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.HASmartChargeBackend>("backend")
    .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=../hasmartcharge.db");

builder.Build().Run();