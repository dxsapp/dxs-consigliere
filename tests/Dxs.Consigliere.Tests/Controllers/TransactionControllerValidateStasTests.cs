using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class TransactionControllerValidateStasTests
{
    [Fact]
    public async Task ReturnsBadRequestForMalformedId()
    {
        var queryService = new Mock<ITransactionQueryService>();
        queryService.Setup(x => x.ValidateStasTransactionAsync("abc", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionQueryException(
                TransactionQueryErrorKind.BadRequest,
                "Malformed transaction id: \"abc\""
            ));

        var controller = new TransactionController();

        var result = await controller.ValidateStasTransaction("abc", queryService.Object);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReturnsNotFoundWhenTransactionMissing()
    {
        var id = new string('a', 64);
        var queryService = new Mock<ITransactionQueryService>();
        queryService.Setup(x => x.ValidateStasTransactionAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionQueryException(TransactionQueryErrorKind.NotFound, "Not found"));

        var controller = new TransactionController();

        var result = await controller.ValidateStasTransaction(id, queryService.Object);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ReturnsTeapotWhenTransactionIsNotStas()
    {
        var id = new string('b', 64);
        var queryService = new Mock<ITransactionQueryService>();
        queryService.Setup(x => x.ValidateStasTransactionAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionQueryException(
                TransactionQueryErrorKind.NotStas,
                "This is not a STAS transaction"
            ));

        var controller = new TransactionController();

        var result = await controller.ValidateStasTransaction(id, queryService.Object);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(418, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReturnsValidateStasResponseWithDstasFields()
    {
        var response = new ValidateStasResponse(
            false,
            new string('c', 64),
            true,
            false,
            false,
            "freeze",
            2,
            true,
            "token-id-1",
            [],
            [],
            "valid",
            true,
            []
        );

        var queryService = new Mock<ITransactionQueryService>();
        queryService.Setup(x => x.ValidateStasTransactionAsync(response.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new TransactionController();

        var result = await controller.ValidateStasTransaction(response.Id, queryService.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ValidateStasResponse>(ok.Value);

        Assert.False(payload.AskLater);
        Assert.Equal(response.Id, payload.Id);
        Assert.True(payload.IsLegal);
        Assert.Equal("freeze", payload.EventType);
        Assert.Equal(2, payload.SpendingType);
        Assert.True(payload.OptionalDataContinuity);
        Assert.Equal("token-id-1", payload.TokenId);
        Assert.Equal("valid", payload.ValidationStatus);
        Assert.True(payload.B2GResolved);
        Assert.Empty(payload.MissingDependencies);
    }
}
