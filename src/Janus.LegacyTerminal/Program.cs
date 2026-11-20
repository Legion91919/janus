using Janus.LegacyTerminal;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFakeTerminal();
var app = builder.Build();
app.MapFakeTerminal();
app.Run();
