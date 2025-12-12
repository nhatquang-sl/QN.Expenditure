using Projects;

var builder = DistributedApplication.CreateBuilder(args);

//builder.AddDockerComposePublisher();

var redisCache = builder.AddRedis("Redis")
    .WithRedisInsight();

builder.AddProject<WebAPI>("webapi")
    .WithReference(redisCache)
    .WithExternalHttpEndpoints();

builder.Build().Run();