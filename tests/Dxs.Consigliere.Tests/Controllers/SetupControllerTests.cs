using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Setup;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class SetupControllerTests
{
    [Fact]
    public async Task GetOptions_ReturnsWizardOptions()
    {
        var service = new Mock<ISetupWizardService>(MockBehavior.Strict);
        service.Setup(x => x.GetOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupOptionsResponse
            {
                Defaults = new SetupDefaultsResponse
                {
                    RawTxPrimaryProvider = "junglebus"
                }
            });

        var controller = new SetupController();
        var result = await controller.GetOptions(service.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<SetupOptionsResponse>(ok.Value);
        Assert.Equal("junglebus", payload.Defaults.RawTxPrimaryProvider);
    }

    [Fact]
    public async Task Complete_ReturnsConflict_WhenSetupAlreadyCompleted()
    {
        var service = new Mock<ISetupWizardService>(MockBehavior.Strict);
        service.Setup(x => x.CompleteAsync(It.IsAny<SetupCompleteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SetupWizardException("setup_already_completed", StatusCodes.Status409Conflict));

        var controller = new SetupController();
        var result = await controller.Complete(new SetupCompleteRequest(), service.Object);

        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }
}
