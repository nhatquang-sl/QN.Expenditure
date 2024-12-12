using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Auth.Infrastructure.Data
{
    internal class AuthContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
    {
        public AuthDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("D:\\QN.Expenditure\\src\\WebAPI\\QN.Expenditure.Credentials\\appsettings.json")
                .Build();

            var connString = config.GetValue<string>("ConnectionStrings:AuthConnection");
            //connString = "Server=(localdb)\\mssqllocaldb;Database=qnexp;Trusted_Connection=True;MultipleActiveResultSets=true";

            var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
            optionsBuilder.UseSqlServer(connString);

            return new AuthDbContext(optionsBuilder.Options);
        }
    }
}