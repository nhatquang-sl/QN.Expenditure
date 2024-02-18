# EF Core Migrations
## Installing the tools
- `dotnet tool install --global dotnet-ef`
- `dotnet tool update --global dotnet-ef` update version
- `dotnet add package Microsoft.EntityFrameworkCore.Design`: add this package to specific project that contains `DBContext`

## Create your first migration
```bash
dotnet ef migrations add InitialCreate
```

## Create your database and schema
```bash
dotnet ef database update
```

## Remote
``` bash
dotnet ef migrations remove -f
```