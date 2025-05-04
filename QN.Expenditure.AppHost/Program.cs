var builder = DistributedApplication.CreateBuilder(args);

var redisCache = builder.AddRedis("redis-cache")
    .WithRedisInsight();

builder.AddProject<Projects.WebAPI>("webapi")
    .WithReference(redisCache)
    .WithExternalHttpEndpoints();

builder.Build().Run();
