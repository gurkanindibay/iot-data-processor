using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;

namespace SimpleFunctionApp;

public class SimpleHttpFunction
{
    private readonly ILogger<SimpleHttpFunction> _logger;

    public SimpleHttpFunction(ILogger<SimpleHttpFunction> logger)
    {
        _logger = logger;
    }

    [Function("SimpleHttpFunction")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        string name = req.Query["name"];

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        if (!string.IsNullOrEmpty(requestBody))
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);
            name = name ?? data?.GetValueOrDefault("name");
        }

        string responseMessage = string.IsNullOrEmpty(name)
            ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            : $"Hello, {name}!";

        return new OkObjectResult(responseMessage);
    }
}
