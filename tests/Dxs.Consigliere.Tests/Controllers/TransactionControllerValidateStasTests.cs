using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto.Responses;

using Microsoft.AspNetCore.Mvc;

using Moq;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Tests.Controllers;

public class TransactionControllerValidateStasTests
{
    [Fact]
    public async Task ReturnsBadRequestForMalformedId()
    {
        var controller = new TransactionController();
        var store = Mock.Of<IDocumentStore>();

        var result = await controller.ValidateStasTransaction("abc", store);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReturnsNotFoundWhenTransactionMissing()
    {
        var (controller, store) = BuildControllerWithSession(metaTransaction: null);
        var id = new string('a', 64);

        var result = await controller.ValidateStasTransaction(id, store.Object);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ReturnsTeapotWhenTransactionIsNotStas()
    {
        var tx = new MetaTransaction
        {
            Id = new string('b', 64),
            IsIssue = false,
            AllStasInputsKnown = true,
            IsStas = false,
            IllegalRoots = [],
            TokenIds = [],
        };
        var (controller, store) = BuildControllerWithSession(tx);

        var result = await controller.ValidateStasTransaction(tx.Id, store.Object);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(418, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReturnsValidateStasResponseWithDstasFields()
    {
        var tx = new MetaTransaction
        {
            Id = new string('c', 64),
            IsIssue = false,
            AllStasInputsKnown = true,
            IsStas = true,
            IsRedeem = false,
            DstasEventType = "freeze",
            DstasSpendingType = 2,
            DstasOptionalDataContinuity = true,
            IllegalRoots = [],
            TokenIds = ["token-id-1"],
        };
        var (controller, store) = BuildControllerWithSession(tx);

        var result = await controller.ValidateStasTransaction(tx.Id, store.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateStasResponse>(ok.Value);

        Assert.False(response.AskLater);
        Assert.Equal(tx.Id, response.Id);
        Assert.True(response.IsLegal);
        Assert.Equal("freeze", response.EventType);
        Assert.Equal(2, response.SpendingType);
        Assert.True(response.OptionalDataContinuity);
        Assert.Equal("token-id-1", response.TokenId);
    }

    private static (TransactionController controller, Mock<IDocumentStore> store) BuildControllerWithSession(MetaTransaction? metaTransaction)
    {
        var session = new Mock<IAsyncDocumentSession>();
        session.Setup(x => x.LoadAsync<MetaTransaction>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metaTransaction);

        var store = new Mock<IDocumentStore>();
        store.Setup(x => x.OpenAsyncSession(It.IsAny<SessionOptions>()))
            .Returns(session.Object);

        return (new TransactionController(), store);
    }
}
