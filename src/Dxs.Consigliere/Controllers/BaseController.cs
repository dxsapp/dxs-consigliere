using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

public class BaseController: ControllerBase
{
    protected IActionResult NotFound<T>(T result) => StatusCode(StatusCodes.Status404NotFound, result);
    
    protected IActionResult Conflict<T>(T result) => StatusCode(StatusCodes.Status409Conflict, result);

    protected IActionResult BadRequest<T>(T result) => StatusCode(StatusCodes.Status400BadRequest, result);
    
    protected IActionResult InternalError<T>(T result) => StatusCode(StatusCodes.Status500InternalServerError, result);
}