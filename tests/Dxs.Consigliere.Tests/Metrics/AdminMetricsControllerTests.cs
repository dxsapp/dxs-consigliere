using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.Metrics;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Moq;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 A2 M2 + M3 — bounded-lastN + Raven round-trip via mocked
/// IDocumentStore. Pins:
/// - <c>lastN = 0</c> / negative returns empty history.
/// - <c>lastN</c> exceeding retention is clamped to retention.
/// - <c>lastN</c> exceeding the hard ceiling is clamped to the
///   ceiling.
/// </summary>
public class AdminMetricsControllerTests
{
    // A2-followup N2 fix: the controller's clamp logic was extracted
    // to AdminMetricsController.ClampLastN — a testable static helper.
    // The theory below exercises the SAME helper the production code
    // path uses (not a parallel reimplementation), so it actually
    // covers the controller's behaviour.
    [Theory]
    [InlineData(null, 720, 0)]    // lastN omitted → no history
    [InlineData(0, 720, 0)]       // lastN=0 → no history
    [InlineData(-5, 720, 0)]      // negative → no history (clamped to 0)
    [InlineData(int.MinValue, 720, 0)] // extreme negative → no history
    [InlineData(50, 720, 50)]     // below retention → unclamped
    [InlineData(720, 720, 720)]   // equal retention → unclamped
    [InlineData(721, 720, 720)]   // one above retention → clamped to retention
    [InlineData(5000, 720, 720)]  // far above retention → clamped to retention
    [InlineData(50, 0, 50)]       // retention=0 → fall back to hard ceiling (1440)
    [InlineData(1500, 0, 1440)]   // retention=0 + above ceiling → clamped to ceiling
    [InlineData(1500, 2000, 1440)] // retention > ceiling + far-above → ceiling wins
    [InlineData(int.MaxValue, 720, 720)] // int.MaxValue → retention wins
    [InlineData(int.MaxValue, 0, 1440)]  // int.MaxValue + no retention → hard ceiling
    public void ClampLastN_HelperReturnsExpectedBoundedValue(int? requestedLastN, int retention, int expectedBounded)
    {
        var bounded = AdminMetricsController.ClampLastN(requestedLastN, retention);
        Assert.Equal(expectedBounded, bounded);
    }

    [Fact]
    public void HardCeilingConstant_IsDocumentedValue()
    {
        // Pin the hard ceiling so a future relaxation requires
        // explicit code change + audit re-pass.
        Assert.Equal(1440, AdminMetricsController.HardLastNCeiling);
    }
}
