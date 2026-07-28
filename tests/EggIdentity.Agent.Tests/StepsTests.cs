using EggIdentity.Agent;

namespace EggIdentity.Agent.Tests;

public class StepsTests {
    [Fact]
    public void DockerPull_TwoContainers_OneStale_ShortCircuitEndsFalse() {
        var containerImage = new Dictionary<string, string> {
            ["a"] = "sha256:new",
            ["b"] = "sha256:old-b",
        };

        (string, bool) Run(string name, string[] args) {
            if (name != "docker") return ("", false);
            if (args[0] == "inspect" && args[2] == "{{.Image}}")
                return (containerImage[args[3]], true);
            if (args[0] == "pull")
                return ("Image is up to date for img:latest\n", true);
            if (args[0] == "image" && args[1] == "inspect" && args[3] == "{{.Id}}")
                return ("sha256:new", true);
            if (args[0] == "image" && args[1] == "inspect")
                return ("sha256:new <no value>", true);
            return ("", false);
        }

        var c = new RunContext { Repo = "", RepoUrl = "", Run = Run };
        var a = new DockerPull { Ref = "img:latest", Container = "a" };
        var b = new DockerPull { Ref = "img:latest", Container = "b" };

        Assert.Null(a.Exec(c));
        Assert.True(c.ShortCircuit);

        Assert.Null(b.Exec(c));
        Assert.False(c.ShortCircuit);
    }

    [Fact]
    public void DockerPull_TwoContainers_BothCurrent_ShortCircuitStaysTrue() {
        var containerImage = new Dictionary<string, string> {
            ["a"] = "sha256:new",
            ["b"] = "sha256:new",
        };

        (string, bool) Run(string name, string[] args) {
            if (name != "docker") return ("", false);
            if (args[0] == "inspect" && args[2] == "{{.Image}}")
                return (containerImage[args[3]], true);
            if (args[0] == "pull")
                return ("Image is up to date for img:latest\n", true);
            if (args[0] == "image" && args[1] == "inspect" && args[3] == "{{.Id}}")
                return ("sha256:new", true);
            if (args[0] == "image" && args[1] == "inspect")
                return ("sha256:new <no value>", true);
            return ("", false);
        }

        var c = new RunContext { Repo = "", RepoUrl = "", Run = Run };
        var a = new DockerPull { Ref = "img:latest", Container = "a" };
        var b = new DockerPull { Ref = "img:latest", Container = "b" };

        Assert.Null(a.Exec(c));
        Assert.Null(b.Exec(c));
        Assert.True(c.ShortCircuit);
    }
}
