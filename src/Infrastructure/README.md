# EF Core Migrations
## Installing the tools
- `dotnet tool install --global dotnet-ef`
- `dotnet add package Microsoft.EntityFrameworkCore.Design`: add this package to specific project that contains `DBContext`

## Create your first migration
```
dotnet ef migrations add InitialCreate
```

## Create your database and schema
```bash
dotnet ef database update
```