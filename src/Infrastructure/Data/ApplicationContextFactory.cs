using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data
{
    internal class ApplicationContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("D:\\QN.Expenditure\\src\\WebAPI\\QN.Expenditure.Credentials\\appsettings.json")
                .Build();

            var connString = config.GetValue<string>("ConnectionStrings:DefaultConnection");
            //connString = "Server=(localdb)\\mssqllocaldb;Database=qnexp;Trusted_Connection=True;MultipleActiveResultSets=true";

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(connString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
