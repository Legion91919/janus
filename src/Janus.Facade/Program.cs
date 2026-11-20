using Janus.Facade;

var builder = WebApplication.CreateBuilder(args);
builder.AddBridgeFacade();
var app = builder.Build();
app.UseBridgeFacade();
app.Run();
