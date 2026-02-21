IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> backend = builder.AddProject<Projects.HASmartCharge_Backend>("backend")
    .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=../hasmartcharge.db");

builder.AddViteApp("frontend", "../HASmartCharge.Frontend")
   .WithEnvironment("VITE_BACKEND_URL", backend.GetEndpoint("http"))
   .WithExternalHttpEndpoints();

builder.Build().Run();