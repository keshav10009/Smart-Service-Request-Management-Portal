using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ServicePortal.Infrastructure.Services;
using Xunit;

namespace ServicePortal.UnitTests.Validation;

public class FileStorageServiceTests
{
    private readonly FileStorageService _storageService;

    public FileStorageServiceTests()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FileStorage:Path"]).Returns(Path.Combine(Path.GetTempPath(), "ServicePortalUploadsTest"));

        var loggerMock = new Mock<ILogger<FileStorageService>>();
        _storageService = new FileStorageService(configMock.Object, loggerMock.Object);
    }

    [Theory]
    [InlineData("document.pdf", true)]
    [InlineData("screenshot.PNG", true)]
    [InlineData("data.xlsx", true)]
    [InlineData("readme.txt", true)]
    [InlineData("archive.zip", true)]
    [InlineData("malicious.exe", false)]
    [InlineData("script.bat", false)]
    [InlineData("trojan.dll", false)]
    [InlineData("powershell.ps1", false)]
    [InlineData("", false)]
    public void IsAllowedExtension_ValidatesSafely(string fileName, bool expected)
    {
        var result = _storageService.IsAllowedExtension(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1024, true)]
    [InlineData(10 * 1024 * 1024, true)] // 10MB
    [InlineData((10 * 1024 * 1024) + 1, false)] // >10MB
    [InlineData(0, false)]
    [InlineData(-100, false)]
    public void IsAllowedSize_EnforcesLimits(long size, bool expected)
    {
        var result = _storageService.IsAllowedSize(size);
        Assert.Equal(expected, result);
    }
}
