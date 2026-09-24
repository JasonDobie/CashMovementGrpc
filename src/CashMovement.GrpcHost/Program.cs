using CashMovement.GrpcHost.Services;
using CashMovement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var connectionString = builder.Configuration.GetConnectionString("CashMovement")
    ?? throw new InvalidOperationException("ConnectionStrings:CashMovement is not configured.");

builder.Services.AddCashMovementInfrastructure(connectionString);

var app = builder.Build();

app.MapGrpcService<CashMovementGrpcService>();
app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Cash Movement gRPC Service</title>
    <style>body{font-family:Arial,sans-serif;margin:40px;line-height:1.6}h1{margin-bottom:8px}.status{font-weight:bold}code{background:#f2f2f2;padding:2px 5px;border-radius:3px}</style>
</head>
<body>
    <h1>Cash Movement gRPC Service</h1>
    <p class="status">The service is running successfully.</p>
    <p>This HTTPS endpoint is the service health/landing page. Use a gRPC client to call the RPC methods.</p>
    <ul>
        <li><code>SubmitMovement</code></li>
        <li><code>GetBalance</code></li>
        <li><code>ExportStatement</code></li>
    </ul>
</body>
</html>
""", "text/html"));

app.Run();

public partial class Program { }
