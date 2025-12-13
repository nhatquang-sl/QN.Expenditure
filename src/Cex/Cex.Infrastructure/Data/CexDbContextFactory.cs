using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Cex.Infrastructure.Data
{
    internal class CexDbContextFactory : IDesignTimeDbContextFactory<CexDbContext>
    {
        public CexDbContext CreateDbContext(string[] args)
        {
            var path = "/Users/quang/workspace/QN.Expenditure/src/WebAPI/QN.Expenditure.Credentials";
            var config = new ConfigurationBuilder()
                .AddJsonFile($"{path}/appsettings.json")
                // .AddJsonFile($"{path}/appsettings.Production.json", optional: true, reloadOnChange: true)
                .Build();

            var connString = config.GetValue<string>("ConnectionStrings:CexConnection");
            //connString = "Server=.;Database=BoDb;Trusted_Connection=True;MultipleActiveResultSets=true";

            var optionsBuilder = new DbContextOptionsBuilder<CexDbContext>();
            optionsBuilder.UseSqlServer(connString);

            return new CexDbContext(optionsBuilder.Options);
        }
    }
}