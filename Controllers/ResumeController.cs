using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PDFResumenAI.API.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
public class ResumeController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ResumeController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ResumeRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Texto))
            return BadRequest(new { error = "Request vacío o campo 'Texto' requerido." });

        var apiKey = _configuration.GetValue<string>("OpenAI:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(500, new { error = "OpenAI API key no configurada. Añade 'OpenAI:ApiKey' en __User Secrets__ o en __Environment Variables__." });

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model = "gpt-4.1-mini",
            input = $"Resume el siguiente texto de forma clara y profesional:\n\n{request.Texto}"
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/responses", content);
        var responseString = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseString);

        string resumen = null;
        if (doc.RootElement.TryGetProperty("output", out var outputArray))
        {
            foreach (var item in outputArray.EnumerateArray())
            {
                if (item.TryGetProperty("content", out var contentArray))
                {
                    foreach (var contentItem in contentArray.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var textProp))
                        {
                            resumen = textProp.GetString();
                            break;
                        }
                    }
                }
                if (resumen != null) break;
            }
        }

        return Ok(new { resumen });
    }
}
