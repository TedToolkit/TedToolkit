using TedToolkit.ModularPipelines.Build.Versioning;

namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class DailyReleaseVersionPolicyTests
{
    /// <summary>
    /// 验证同一天的成功与放弃记录都会消耗计数器，但不同日期不会影响结果。
    /// </summary>
    [Test]
    public async Task Should_allocate_the_next_consumed_daily_counter()
    {
        var result = new DailyReleaseVersionPolicy().Resolve(
            new DailyReleaseVersionRequest
            {
                Date = new DateOnly(2026, 7, 29),
                SourceRevision = "current",
                ConsumedVersions =
                [
                    new ConsumedReleaseVersionRecord
                    {
                        Version = "2026.7.28.9",
                        SourceRevision = "old",
                        Disposition = ConsumedReleaseDisposition.PackageSucceeded,
                    },
                    new ConsumedReleaseVersionRecord
                    {
                        Version = "2026.7.29",
                        SourceRevision = "first",
                        Disposition = ConsumedReleaseDisposition.PackageSucceeded,
                    },
                    new ConsumedReleaseVersionRecord
                    {
                        Version = "2026.7.29.3",
                        SourceRevision = "partial",
                        Disposition = ConsumedReleaseDisposition.Abandoned,
                    },
                ],
            });

        await Assert.That(result.Kind).IsEqualTo(ReleaseVersionResolutionKind.NewVersion);
        await Assert.That(result.Version).IsEqualTo("2026.7.29.4");
        await Assert.That(result.Counter).IsEqualTo(4);
    }

    /// <summary>
    /// 验证活动发布预留会要求恢复且不会返回可构建版本。
    /// </summary>
    [Test]
    public async Task Should_require_recovery_when_a_publication_is_active()
    {
        var result = new DailyReleaseVersionPolicy().Resolve(
            new DailyReleaseVersionRequest
            {
                Date = new DateOnly(2026, 7, 29),
                SourceRevision = "current",
                HasActivePublicationReservation = true,
            });

        await Assert.That(result.Kind)
            .IsEqualTo(ReleaseVersionResolutionKind.PublicationRecoveryRequired);
        await Assert.That(result.Version).IsNull();
        await Assert.That(result.Counter).IsNull();
    }

    /// <summary>
    /// 验证计数器达到 65534 后在执行任何构建前报告耗尽。
    /// </summary>
    [Test]
    public async Task Should_report_exhaustion_after_the_largest_assembly_counter()
    {
        var result = new DailyReleaseVersionPolicy().Resolve(
            new DailyReleaseVersionRequest
            {
                Date = new DateOnly(2026, 7, 29),
                SourceRevision = "current",
                ConsumedVersions =
                [
                    new ConsumedReleaseVersionRecord
                    {
                        Version = "2026.7.29.65534",
                        SourceRevision = "last",
                        Disposition = ConsumedReleaseDisposition.PackageSucceeded,
                    },
                ],
            });

        await Assert.That(result.Kind).IsEqualTo(ReleaseVersionResolutionKind.Exhausted);
        await Assert.That(result.Version).IsNull();
    }

    /// <summary>
    /// 验证非规范版本与重复的已消费版本会被拒绝。
    /// </summary>
    [Test]
    [Arguments("2026.07.29")]
    [Arguments("2026.7.29.0")]
    [Arguments("2026.7.29.65535")]
    public async Task Should_reject_invalid_or_duplicate_consumed_versions(string invalidVersion)
    {
        var request = new DailyReleaseVersionRequest
        {
            Date = new DateOnly(2026, 7, 29),
            SourceRevision = "current",
            ConsumedVersions =
            [
                new ConsumedReleaseVersionRecord
                {
                    Version = invalidVersion,
                    SourceRevision = "old",
                    Disposition = ConsumedReleaseDisposition.PackageSucceeded,
                },
            ],
        };

        await Assert.That(() => new DailyReleaseVersionPolicy().Resolve(request))
            .Throws<InvalidDataException>();
    }

    /// <summary>
    /// 验证同一修订的成功重跑仍分配更高计数器，且 Abandoned 仅消费版本而不声明成功。
    /// </summary>
    [Test]
    public async Task Should_increment_package_success_reruns_and_abandoned_attempts()
    {
        var policy = new DailyReleaseVersionPolicy();
        var result = policy.Resolve(
            new DailyReleaseVersionRequest
            {
                Date = new DateOnly(2026, 7, 29),
                SourceRevision = "same-revision",
                ConsumedVersions =
                [
                    new ConsumedReleaseVersionRecord
                    {
                        Version = "2026.7.29",
                        SourceRevision = "same-revision",
                        Disposition =
                            ConsumedReleaseDisposition.PackageSucceeded,
                    },
                    new ConsumedReleaseVersionRecord
                    {
                        Version = "2026.7.29.1",
                        SourceRevision = "same-revision",
                        Disposition =
                            ConsumedReleaseDisposition.Abandoned,
                    },
                ],
            });

        await Assert.That(result.Kind)
            .IsEqualTo(ReleaseVersionResolutionKind.NewVersion);
        await Assert.That(result.Version).IsEqualTo("2026.7.29.2");
        await Assert.That(result.Counter).IsEqualTo(2);
    }

    /// <summary>
    /// 验证未知消费处置与不足四位的年份不会生成不可用的程序集版本。
    /// </summary>
    [Test]
    public async Task Should_reject_invalid_dispositions_and_short_years()
    {
        var policy = new DailyReleaseVersionPolicy();

        await Assert.That(() => policy.Resolve(
                new DailyReleaseVersionRequest
                {
                    Date = new DateOnly(2026, 7, 29),
                    SourceRevision = "current",
                    ConsumedVersions =
                    [
                        new ConsumedReleaseVersionRecord
                        {
                            Version = "2026.7.29",
                            SourceRevision = "old",
                            Disposition = (ConsumedReleaseDisposition)999,
                        },
                    ],
                }))
            .Throws<InvalidDataException>();
        await Assert.That(() => policy.Resolve(
                new DailyReleaseVersionRequest
                {
                    Date = new DateOnly(999, 7, 29),
                    SourceRevision = "current",
                }))
            .Throws<InvalidDataException>();
    }
}