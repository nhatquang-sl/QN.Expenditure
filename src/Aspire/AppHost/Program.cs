using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// builder.AddDockerComposePublisher();

var redisCache = builder.AddRedis("redis-cache")
    .WithRedisInsight();

builder.AddProject<WebAPI>("webapi")
    .WithReplicas(2)
    .WithReference(redisCache)
    .WaitFor(redisCache)
    .WithExternalHttpEndpoints();

builder.Build().Run();