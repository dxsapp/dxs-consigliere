using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class TransactionControllerStateTests
{
    [Fact]
    public async Task ReturnsTransactionStatePayload()
    {
        var id = new string('a', 64);
        var response = new TransactionStateResponse
        {
            TxId = id,
            Known = true,
            LifecycleStatus = "confirmed",
            Authoritative = true,
            SeenBySources = ["node"],
            PayloadAvailable = true
        };

        var queryService = new Mock<ITransactionQueryService>();
        queryService.Setup(x => x.GetTransactionStateAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new TransactionController();

        var result = await controller.GetTransactionState(id, queryService.Object, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TransactionStateResponse>(ok.Value);

        Assert.Equal(id, payload.TxId);
        Assert.Equal("confirmed", payload.LifecycleStatus);
        Assert.True(payload.PayloadAvailable);
    }

    [Fact]
    public async Task ReturnsNotFoundForMissingTxState()
    {
        var id = new string('b', 64);
        var queryService = new Mock<ITransactionQueryService>();
        queryService.Setup(x => x.GetTransactionStateAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionQueryException(TransactionQueryErrorKind.NotFound, "Not found"));

        var controller = new TransactionController();

        var result = await controller.GetTransactionState(id, queryService.Object, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
        Assert.Equal("Not found", objectResult.Value);
    }
}
