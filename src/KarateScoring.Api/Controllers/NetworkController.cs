using FkcScoring.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace KarateScoring.Api.Controllers;

/// <summary>Expose l'adresse réseau locale du poste, pour que l'organisateur sache quelle URL donner aux postes tatami.</summary>
[ApiController]
[Route("api/network-info")]
public class NetworkController : ControllerBase
{
    [HttpGet]
    public ActionResult<NetworkInfoDto> Get()
    {
        var firstUrl = NetworkConfig.ResolveUrls().Split(';')[0];
        var port = Uri.TryCreate(firstUrl, UriKind.Absolute, out var uri) ? uri.Port : NetworkConfig.DefaultPort;
        return new NetworkInfoDto(port, NetworkConfig.LocalIPv4Addresses());
    }
}
