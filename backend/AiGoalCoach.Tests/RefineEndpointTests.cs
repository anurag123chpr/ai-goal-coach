namespace AiGoalCoach.Tests
{
    using System;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    /// <summary>
    /// Integration tests for the refine endpoint in the API.
    /// </summary>
    public class RefineEndpointTests : IClassFixture<ApiFactory>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RefineEndpointTests"/> class.
        /// </summary>
        /// <param name="factory">The test web application factory.</param>
        public RefineEndpointTests(ApiFactory factory)
        {
            this.Client = factory.CreateClient();
            var configuration = factory.Services.GetRequiredService<IConfiguration>();
            this.Model = configuration["HF_MODEL"] ?? string.Empty;
            this.Token = configuration["HF_API_TOKEN"] ?? string.Empty;
        }

        /// <summary>
        /// Gets the HTTP client used for requests.
        /// </summary>
        public HttpClient Client { get; }

        /// <summary>
        /// Gets the configured HF model id for live tests.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Gets the configured HF API token for live tests.
        /// </summary>
        public string Token { get; }

        /// <summary>
        /// Sends valid inputs to the refine endpoint and asserts a structured goal result.
        /// </summary>
        /// <param name="text">The input text to refine.</param>
        /// <returns>A task that represents the asynchronous test execution.</returns>
        [Theory]
        [InlineData("I want to get better at sales.")]
        [InlineData("Lead the team to reduce defects by 20% in the next quarter.")]
        public async Task Refine_ReturnsGoalForValidInputs(string text)
        {
            this.EnsureCredentials();

            // small delay to avoid sending rapid consecutive requests to the HF router during tests
            await Task.Delay(2000);
            var response = await this.Client.PostAsJsonAsync("/api/goals/refine", new { text });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.True(payload.GetProperty("ok").GetBoolean());
            var data = payload.GetProperty("data");
            var keyResults = data.GetProperty("key_results").EnumerateArray().ToList();
            Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("refined_goal").GetString()));
            Assert.True(keyResults.Count >= 3);
            Assert.InRange(data.GetProperty("confidence_score").GetInt32(), 1, 10);
        }

        /// <summary>
        /// Verifies the refine endpoint rejects an empty text payload.
        /// </summary>
        /// <returns>A task that represents the asynchronous test execution.</returns>
        [Fact]
        public async Task Refine_RejectsEmptyText()
        {
            this.EnsureCredentials();
            var response = await this.Client.PostAsJsonAsync("/api/goals/refine", new { text = string.Empty });

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        private void EnsureCredentials()
        {
            if (string.IsNullOrWhiteSpace(this.Model) || string.IsNullOrWhiteSpace(this.Token))
            {
                throw new InvalidOperationException("HF_MODEL / HF_API_TOKEN must be configured in appsettings or environment for live integration tests.");
            }
        }
    }
    }
