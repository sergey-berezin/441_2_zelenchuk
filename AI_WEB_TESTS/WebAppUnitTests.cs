using AI_WEB_APP.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net.Http.Json;

namespace AI_WEB_TESTS {
    public class WebTests : IClassFixture<WebApplicationFactory<Program>> {

        private readonly WebApplicationFactory<Program> factory;

        public WebTests(WebApplicationFactory<Program> _factory) {
            factory = _factory;
        }

        [Fact]
        public async Task TestPostMethodWithRealPhoto() {
            var client = factory.CreateClient();
            byte[] bytes = File.ReadAllBytes("..\\..\\..\\..\\in_photo\\cats.jpg");
            string base64str = Convert.ToBase64String(bytes);

            var task = await client.PostAsJsonAsync("http://localhost:5257/Photo", new ReciveData() { id = 0, img = base64str });

            Assert.Equal(System.Net.HttpStatusCode.OK, task.StatusCode);
        }

        [Fact]
        public async Task TestPostMethodWithEmptyPhoto() {
            var client = factory.CreateClient();
            string base64str = Convert.ToBase64String(new byte[0]);

            var task = await client.PostAsJsonAsync("http://localhost:5257/Photo", new ReciveData() { id = 0, img = base64str });

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, task.StatusCode);
        }

    }
}