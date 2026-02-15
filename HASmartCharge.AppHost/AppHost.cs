var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.HASmartCharge_Backend>("backend")
    .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=../hasmartcharge.db");

builder.Build().Run();