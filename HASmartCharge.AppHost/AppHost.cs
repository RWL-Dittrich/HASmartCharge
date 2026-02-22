IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> backend = builder.AddProject<Projects.HASmartCharge_Backend>("backend")
    .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=../hasmartcharge.db");

EndpointReference httpEndpoint = backend.GetEndpoint("http");

builder.AddViteApp("frontend", "../HASmartCharge.Frontend")
   .WithEnvironment("VITE_BACKEND_URL", ReferenceExpression.Create($"http://127.0.0.1:{httpEndpoint.Property(EndpointProperty.Port)}"))
   .WithExternalHttpEndpoints();

builder.Build().Run();