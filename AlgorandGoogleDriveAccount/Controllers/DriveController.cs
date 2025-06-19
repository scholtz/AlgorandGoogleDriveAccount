using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DriveController : ControllerBase
{
    private readonly IGoogleAuthProvider _auth;

    public DriveController(IGoogleAuthProvider auth)
    {
        _auth = auth;
    }

    [HttpGet("file")]
    public async Task<IActionResult> GetFile([FromQuery] string fileId)
    {
        var cred = await _auth.GetCredentialAsync();

        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = cred,
            ApplicationName = "MyDriveApp"
        });

        var request = service.Files.Get(fileId);
        var stream = new MemoryStream();
        request.MediaDownloader.ProgressChanged += progress =>
        {
            // Optional: log download progress
        };

        await request.DownloadAsync(stream);
        stream.Position = 0;

        var file = await request.ExecuteAsync();

        return File(stream, file.MimeType, file.Name);
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "https://localhost:44305/swagger/"
        }, GoogleOpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return SignOut(new AuthenticationProperties
        {
            RedirectUri = "https://localhost:44305/swagger"
        }, CookieAuthenticationDefaults.AuthenticationScheme);
    }
}