using Parkings.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Aspire service discovery требует https+http:// схему
builder.Services.AddHttpClient("api")
    .AddServiceDiscovery()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https+http://apiservice");
        client.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);  // AttemptTimeout
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);  // минимум AttemptTimeout * 2
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(3);
    });

builder.Services.AddHttpClient("camera", client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int?>("Worker:CameraTimeoutSeconds") ?? 30;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
})
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);  // минимум AttemptTimeout * 2
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
});

builder.Services.AddSingleton<YoloDetector>(sp =>
    new YoloDetector(builder.Configuration));

builder.Services.AddHostedService<ParkingWorker>();

var host = builder.Build();
host.Run();
