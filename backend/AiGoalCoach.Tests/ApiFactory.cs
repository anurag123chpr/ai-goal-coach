namespace AiGoalCoach.Tests
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;

    /// <summary>
    /// Test web application factory for integration tests.
    /// </summary>
    public class ApiFactory : WebApplicationFactory<Program>
    {
        /// <summary>
        /// Configure the test host environment.
        /// </summary>
        /// <param name="builder">The web host builder.</param>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
