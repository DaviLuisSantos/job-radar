using JobRadar.Domain.Import;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Tests.Import;

public class PrototypeMappingTests
{
    [Theory]
    [InlineData("new", false, null)]
    [InlineData(null, false, null)]
    [InlineData("reviewed", false, ApplicationStatus.Reviewed)]
    [InlineData("applied", true, ApplicationStatus.Applied)]
    [InlineData("interview", true, ApplicationStatus.Interview)]
    [InlineData("rejected", false, ApplicationStatus.Dismissed)]
    [InlineData("rejected", true, ApplicationStatus.Declined)]
    [InlineData("new", true, ApplicationStatus.Applied)]
    [InlineData(" Applied ", true, ApplicationStatus.Applied)]
    public void Prototype_status_becomes_an_application(string? status, bool applied, ApplicationStatus? expected)
    {
        Assert.Equal(expected, PrototypeMapping.Status(status, applied));
    }
}
