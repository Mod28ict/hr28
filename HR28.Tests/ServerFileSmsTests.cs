using HR28.Application.Common;
using HR28.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HR28.Tests;

/// <summary>The temporary "SMS to a server folder" test mode and its end date.</summary>
public class ServerFileSmsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hr28-sms-test-" + Guid.NewGuid().ToString("N"));

    public ServerFileSmsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private ServerFileSmsSender Sender(DateTime lastDay, DateTime today) =>
        new(_folder, lastDay, NullLogger<ServerFileSmsSender>.Instance, () => today);

    [Fact]
    public async Task Writes_the_message_to_the_folder_up_to_and_on_the_last_day()
    {
        var lastDay = new DateTime(2026, 10, 19);

        Assert.True(await Sender(lastDay, new DateTime(2026, 10, 12)).SendAsync("9607771234", "HR28 code: 482913."));
        Assert.True(await Sender(lastDay, lastDay).SendAsync("9607771234", "HR28 code: 111222."));

        var texts = Directory.GetFiles(_folder).Select(File.ReadAllText).ToList();
        Assert.Equal(2, texts.Count);
        Assert.Contains(texts, t => t.Contains("HR28 code: 482913.") && t.Contains("•••••••234") && !t.Contains("9607771234"));
        if (!OperatingSystem.IsWindows())
            foreach (var file in Directory.GetFiles(_folder))
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    [Fact]
    public async Task Refuses_after_the_last_day_and_writes_nothing()
    {
        var sent = await Sender(new DateTime(2026, 10, 19), new DateTime(2026, 10, 20)).SendAsync("9607771234", "HR28 code: 482913.");

        Assert.False(sent);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task Fails_safely_when_the_folder_is_missing()
    {
        var sender = new ServerFileSmsSender(Path.Combine(_folder, "missing"), MaldivesTime.Today, NullLogger<ServerFileSmsSender>.Instance);

        Assert.False(await sender.SendAsync("9607771234", "HR28 code: 482913."));
    }

    [Theory]
    [InlineData("relative/folder", "2026-10-19")]
    [InlineData("", "2026-10-19")]
    [InlineData(null, "19/10/2026")]
    [InlineData(null, "far-future")]
    public void Unusable_settings_stop_the_api_from_starting(string? folder, string until)
    {
        folder ??= _folder;
        if (until == "far-future")
            until = MaldivesTime.Today.AddDays(ServerFileSmsSender.MaxDaysAhead + 1).ToString("yyyy-MM-dd");

        Assert.Throws<InvalidOperationException>(() =>
            ServerFileSmsSender.FromSettings(folder, until, NullLogger<ServerFileSmsSender>.Instance));
    }
}
