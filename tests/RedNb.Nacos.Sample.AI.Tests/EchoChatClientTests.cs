using Microsoft.Extensions.AI;
using RedNb.Nacos.Sample.AI.Chat;
using Xunit;

namespace RedNb.Nacos.Sample.AI.Tests;

public class EchoChatClientTests
{
    [Fact]
    public void Metadata_DeclaresEchoIdentifier()
    {
        var client = new EchoChatClient();
        Assert.Equal("echo", client.Metadata.ProviderName);
    }

    [Fact]
    public async Task GetResponseAsync_ReturnsAssistantRole()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.User, "hello")
        });

        Assert.NotNull(response);
        Assert.NotEmpty(response.Messages);
        Assert.Equal(ChatRole.Assistant, response.Messages[0].Role);
    }

    [Fact]
    public async Task GetResponseAsync_EchoesTheUserText()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(new[]
        {
            new ChatMessage(ChatRole.User, "Paris weather please")
        });

        var text = response.Messages[0].Text;
        Assert.Contains("Paris weather please", text);
    }

    [Fact]
    public async Task GetResponseAsync_AnnotatesToolCountWhenToolsProvided()
    {
        var client = new EchoChatClient();
        var options = new ChatOptions
        {
            Tools = new List<AITool>
            {
                CreateTestTool("get_weather"),
                CreateTestTool("get_time")
            }
        };

        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "go") }, options);

        Assert.Contains("2 tool", response.Messages[0].Text);
    }

    [Fact]
    public async Task GetResponseAsync_DoesNotThrowOnEmptyMessages()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(Array.Empty<ChatMessage>());
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetResponseAsync_DoesNotThrowOnNullOptions()
    {
        var client = new EchoChatClient();
        var response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") }, options: null);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsAtLeastOneUpdate()
    {
        var client = new EchoChatClient();
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "stream me") }))
        {
            updates.Add(update);
        }
        Assert.NotEmpty(updates);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var client = new EchoChatClient();
        client.Dispose();
        client.Dispose(); // must not throw
    }

    // The current Microsoft.Extensions.AI 9.x SDK exposes AITool as abstract
    // with read-only Name/Description and a protected no-arg constructor, so
    // we cannot subclass AITool and set Name the way the brief's TestTool
    // helper does. AIFunctionFactory.Create is the supported entry point
    // for constructing tool instances; AIFunction derives from AITool so the
    // produced instance is assignable to ChatOptions.Tools.
    private static AITool CreateTestTool(string name) =>
        AIFunctionFactory.Create(
            DummyToolMethod,
            name,
            description: $"Test tool named {name}",
            serializerOptions: null);

    private static string DummyToolMethod(string _) => "ok";
}
