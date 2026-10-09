using Sensor3.Contracts;
using Sensor3.Core;
namespace Sensor3.Tests;

[TestClass]
public sealed class BuildMetadataTests
{
    [TestMethod]
    public void TestThat_missing_git_date_is_unknown() => Assert.AreEqual("Unknown", BuildMetadata.SwedishTime(null));

    [TestMethod]
    public void TestThat_build_metadata_is_embedded()
    {
        var info = BuildMetadata.Load(typeof(BuildMetadataTests).Assembly);
        Assert.AreEqual("0.17.0", info.ApplicationVersion);
        Assert.IsNotNull(info.BuildDateUtc);
        Assert.AreEqual(TimeSpan.Zero, info.BuildDateUtc.Value.Offset);
        Assert.IsLessThanOrEqualTo(info.LatestImplementedIteration, info.LatestVerifiedIteration);
    }

    [TestMethod]
    public void TestThat_manifest_contains_all_iterations()
    {
        var entries = BuildMetadata.LoadIterations(typeof(BuildMetadataTests).Assembly);
        Assert.HasCount(18, entries);
        Assert.AreEqual(0, entries.Count(x => x.Status == IterationStatus.Verified));
    }

    [TestMethod]
    public void TestThat_invalid_manifest_is_rejected() =>
        Assert.ThrowsExactly<InvalidOperationException>(() => BuildMetadata.Validate([]));

    [TestMethod]
    public void TestThat_verified_status_requires_evidence_date()
    {
        var entries = BuildMetadata.LoadIterations(typeof(BuildMetadataTests).Assembly).ToArray();
        entries[0] = entries[0] with { Status = IterationStatus.Verified, VerifiedAtUtc = null };
        Assert.ThrowsExactly<InvalidOperationException>(() => BuildMetadata.Validate(entries));
    }

    [TestMethod]
    [DataRow("2026-01-01T12:00:00Z", "2026-01-01 13:00:00 +01:00", DisplayName = "Svensk vintertid")]
    [DataRow("2026-07-01T12:00:00Z", "2026-07-01 14:00:00 +02:00", DisplayName = "Svensk sommartid")]
    public void TestThat_utc_is_displayed_in_swedish_time(string utc, string expected) =>
        Assert.AreEqual(expected, BuildMetadata.SwedishTime(DateTimeOffset.Parse(utc)));
}
