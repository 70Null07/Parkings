var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Parkings_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Parkings_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.Parkings_Worker>("worker")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.Parkings_Worker>("parkings-worker");

builder.Build().Run();
