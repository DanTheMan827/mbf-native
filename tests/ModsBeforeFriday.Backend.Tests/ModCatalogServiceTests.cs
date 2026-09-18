using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ModsBeforeFriday.Backend.Configuration;
using ModsBeforeFriday.Backend.Mods;
using ModsBeforeFriday.Core.Models;
using Xunit;

namespace ModsBeforeFriday.Backend.Tests;

public sealed class ModCatalogServiceTests
{
    [Fact]
    public async Task CatalogPreservesCoversSortsUpdatesFirstAndSuppressesCoreUpdates()
    {
        const string gameJson = """
            {
              "regular.update": {
                "1.0.0": {
                  "name": "Regular Update",
                  "id": "regular.update",
                  "version": "1.0.0",
                  "download": "https://example.test/regular-1.qmod",
                  "source": "https://github.com/example/regular",
                  "author": "Author",
                  "cover": "https://example.test/regular-1.png",
                  "modloader": "Scotland2",
                  "description": "Old"
                },
                "2.0.0": {
                  "name": "Regular Update",
                  "id": "regular.update",
                  "version": "2.0.0",
                  "download": "https://example.test/regular-2.qmod",
                  "source": "https://github.com/example/regular",
                  "author": "Author",
                  "cover": "https://example.test/regular-2.png",
                  "modloader": "Scotland2",
                  "description": "New"
                }
              },
              "fresh.mod": {
                "1.10.0": {
                  "name": "Fresh Mod",
                  "id": "fresh.mod",
                  "version": "1.10.0",
                  "download": "https://example.test/fresh.qmod",
                  "source": "https://example.test/fresh",
                  "author": "Author",
                  "cover": "https://example.test/fresh.png",
                  "modloader": "Scotland2",
                  "description": "Fresh"
                }
              },
              "core.mod": {
                "2.0.0": {
                  "name": "Core Mod",
                  "id": "core.mod",
                  "version": "2.0.0",
                  "download": "https://example.test/core.qmod",
                  "source": "https://example.test/core",
                  "author": "Author",
                  "cover": null,
                  "modloader": "Scotland2",
                  "description": "Core"
                }
              }
            }
            """;

        const string globalJson = """
            {
              "global.only": {
                "1.0.0": {
                  "name": "Global Only",
                  "id": "global.only",
                  "version": "1.0.0",
                  "download": "https://example.test/global.qmod",
                  "source": "https://example.test/global",
                  "author": "Author",
                  "cover": "https://example.test/global.png",
                  "modloader": "Scotland2",
                  "description": "Global"
                }
              }
            }
            """;

        using var http = new HttpClient(new RepositoryHandler(gameJson, globalJson));
        var service = new ModCatalogService(
            http,
            new BackendOptions
            {
                AgentBinaryPath = "unused",
                ModRepositoryBaseUri = new Uri("https://example.test/"),
            },
            NullLogger<ModCatalogService>.Instance);

        var installed = new[]
        {
            new ModInfo("regular.update", "Regular Update", "1.0.0", "1.40.0", null, true, false),
            new ModInfo("core.mod", "Core Mod", "1.0.0", "1.40.0", null, true, true),
        };

        var result = await service.GetAvailableModsAsync("1.40.0", installed, TestContext.Current.CancellationToken);

        Assert.Collection(
            result,
            update =>
            {
                Assert.Equal("regular.update", update.Mod.Id);
                Assert.Equal("2.0.0", update.Mod.Version);
                Assert.Equal("https://example.test/regular-2.png", update.Mod.Cover);
                Assert.True(update.AlreadyInstalled);
                Assert.True(update.NeedsUpdate);
            },
            install =>
            {
                Assert.Equal("fresh.mod", install.Mod.Id);
                Assert.Equal("1.10.0", install.Mod.Version);
                Assert.False(install.AlreadyInstalled);
                Assert.False(install.NeedsUpdate);
            });
    }

    private sealed class RepositoryHandler(string gameJson, string globalJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = request.RequestUri?.AbsolutePath.EndsWith("/global.json", StringComparison.Ordinal) == true
                ? globalJson
                : gameJson;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }
}
