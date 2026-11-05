using EliteMud.Api.Endpoints;
using EliteMud.Application;
using EliteMud.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var cs = builder.Configuration.GetConnectionString("EliteMud") ??
         throw new InvalidOperationException("connectionStrings:EliteMud missing");
builder.Services.AddData(cs);
builder.Services.AddApplication();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();
app.MapAccountEndpoints();

app.Run();
