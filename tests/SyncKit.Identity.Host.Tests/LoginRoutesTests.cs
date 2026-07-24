using SyncKit.Identity;
using SyncKit.Identity.Host;
using Xunit;

namespace SyncKit.Identity.Host.Tests;

public class LoginRoutesTests {
    [Fact]
    public void ValidateMode_Redirect_ReturnsRedirect() {
        Assert.Equal("redirect", Program.ValidateMode("redirect"));
    }

    [Fact]
    public void ValidateMode_Inline_ReturnsInline() {
        Assert.Equal("inline", Program.ValidateMode("inline"));
    }

    [Fact]
    public void ValidateMode_UnknownOrNull_DefaultsToPopup() {
        Assert.Equal("popup", Program.ValidateMode("bogus"));
        Assert.Equal("popup", Program.ValidateMode(null));
    }

    [Fact]
    public void ResolveApp_KnownOrigin_ReturnsConfig() {
        var oauth = new SyncKit.Auth.AuthentikOAuth("https://auth.example.com", "id", "secret", "https://identity.example.com/auth/callback");
        var configs = new Dictionary<string, AppAuthConfig> {
            ["https://app.example.com"] = new AppAuthConfig("https://app.example.com", oauth),
        };

        var result = Program.ResolveApp("https://app.example.com/deep/path?x=1", configs);

        Assert.NotNull(result);
        Assert.Equal("https://app.example.com", result!.Origin);
    }

    [Fact]
    public void ResolveApp_UnknownOrigin_ReturnsNull() {
        var configs = new Dictionary<string, AppAuthConfig>();

        var result = Program.ResolveApp("https://unknown.example.com/x", configs);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveApp_EmptyOrMalformedUrl_ReturnsNull() {
        var configs = new Dictionary<string, AppAuthConfig>();

        Assert.Null(Program.ResolveApp("", configs));
        Assert.Null(Program.ResolveApp("not-a-url", configs));
    }

    [Fact]
    public void BuildFlowUrl_ProviderSlug_UsesIfFlowExecutorPath() {
        var result = Program.BuildFlowUrl("https://auth.example.com", "discord", "https://auth.example.com/application/o/authorize/?client_id=x");

        Assert.Equal(
            "https://auth.example.com/if/flow/discord-only-auth/?next=https%3A%2F%2Fauth.example.com%2Fapplication%2Fo%2Fauthorize%2F%3Fclient_id%3Dx",
            result);
    }

    [Fact]
    public void BuildRedirectCallbackUrl_NoExistingQueryString_UsesQuestionMark() {
        var result = Program.BuildRedirectCallbackUrl("https://app.example.com/dashboard", code: "abc123", error: null);

        Assert.Equal("https://app.example.com/dashboard?code=abc123", result);
    }

    [Fact]
    public void BuildRedirectCallbackUrl_ExistingQueryString_UsesAmpersand() {
        var result = Program.BuildRedirectCallbackUrl("https://app.example.com/dashboard?tab=2", code: "abc123", error: null);

        Assert.Equal("https://app.example.com/dashboard?tab=2&code=abc123", result);
    }

    [Fact]
    public void BuildRedirectCallbackUrl_Error_UsesErrorParam() {
        var result = Program.BuildRedirectCallbackUrl("https://app.example.com/dashboard", code: null, error: "login_failed");

        Assert.Equal("https://app.example.com/dashboard?error=login_failed", result);
    }

    private static readonly LinkOutcome Linked = new(Linked: true, Conflict: false, null, null);
    private static readonly LinkOutcome NotLinked = new(Linked: false, Conflict: false, null, null);
    private static readonly LinkOutcome Conflicted = new(Linked: false, Conflict: true, "someone-else", null);

    [Fact]
    public void ComputeLinkFlag_AllSucceed_ReturnsLinkedOk() {
        var result = Program.ComputeLinkFlag(Linked, [("discord", Linked), ("google", Linked)]);

        Assert.Equal("linked=ok", result);
    }

    [Fact]
    public void ComputeLinkFlag_OneSourceConflicts_NamesThatProvider() {
        var result = Program.ComputeLinkFlag(Linked, [("discord", Linked), ("google", Conflicted)]);

        Assert.Equal("linkConflict=google", result);
    }

    [Fact]
    public void ComputeLinkFlag_AuthentikRowConflicts_NoSourceConflict_NamesAuthentik() {
        var result = Program.ComputeLinkFlag(Conflicted, [("discord", Linked)]);

        Assert.Equal("linkConflict=authentik", result);
    }

    [Fact]
    public void ComputeLinkFlag_SourceConflictTakesPriorityOverAuthentikConflict() {
        var result = Program.ComputeLinkFlag(Conflicted, [("discord", Conflicted)]);

        Assert.Equal("linkConflict=discord", result);
    }

    [Fact]
    public void ComputeLinkFlag_NothingLinkedOrConflicted_ReturnsLinkError() {
        var result = Program.ComputeLinkFlag(NotLinked, [("discord", NotLinked)]);

        Assert.Equal("linkError=1", result);
    }

    [Fact]
    public void ComputeLinkFlag_EmptySourceOutcomes_FallsBackToAuthentikOutcome() {
        var result = Program.ComputeLinkFlag(Linked, []);

        Assert.Equal("linked=ok", result);
    }
}
