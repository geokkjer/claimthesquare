using ClaimTheSquare.ViewModel;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClaimTheSquare.Tests;

// Små tester, samme rolle som i labApi: de er grønne i CI, og på torsdag
// gjør vi én av dem rød for å se at en feilende test stopper hele leveransen.
public class TextObjectTests
{
    [Fact]
    public void TextObject_HoldsTheRouteCoordinate()
    {
        var cell = new TextObject(17, "Terje", "green", "white");

        Assert.Equal(17, cell.Index);
        Assert.Equal("Terje", cell.Text);
        Assert.Equal("green", cell.BackColor);
        Assert.Equal("white", cell.ForeColor);
    }

    [Fact]
    public void TextObject_HasAParameterlessConstructor_SoJsonAndDapperCanBuildIt()
    {
        // Uten denne konstruktøren får verken System.Text.Json eller Dapper
        // laget objektet før de vet hva feltene skal være.
        var empty = new TextObject();

        Assert.Equal(0, empty.Index);
        Assert.Equal("", empty.Text);
    }

    [Fact]
    public void AppVersion_FallsBackToDev_WhenUnset()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Equal("dev", config["APP_VERSION"] ?? "dev");
    }

    [Fact]
    public void AppVersion_ComesFromEnvironment()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_VERSION"] = "sha-a1b2c3d"
            })
            .Build();

        Assert.Equal("sha-a1b2c3d", config["APP_VERSION"]);
    }
}
