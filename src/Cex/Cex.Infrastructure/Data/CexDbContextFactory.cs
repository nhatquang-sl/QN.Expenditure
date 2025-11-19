using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Cex.Infrastructure.Data
{
    internal class CexDbContextFactory : IDesignTimeDbContextFactory<CexDbContext>
    {
        public CexDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("/Users/quang/workspace/QN.Expenditure/src/WebAPI/QN.Expenditure.Credentials/appsettings.json")
                .Build();

            var connString = config.GetValue<string>("ConnectionStrings:CexConnection");
            //connString = "Server=.;Database=BoDb;Trusted_Connection=True;MultipleActiveResultSets=true";

            var optionsBuilder = new DbContextOptionsBuilder<CexDbContext>();
            optionsBuilder.UseSqlServer(connString);

            return new CexDbContext(optionsBuilder.Options);
        }
    }
}
