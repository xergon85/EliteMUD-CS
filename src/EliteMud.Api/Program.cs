using EliteMud.Data;
using EliteMud.Data.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var cs = builder.Configuration.GetConnectionString("EliteMud") ??
         throw new InvalidOperationException("connectionStrings:EliteMud missing");
builder.Services.AddDbContext<EliteMudDbContext>(options => options.UseSqlite(cs));
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

 app.MapGet("/accounts/{accountId:int}/characters",                                                                                                                                                                                                                                                                                                                               
      async (int accountId, IAccountRepository accounts, ICharacterRepository chars, CancellationToken ct) =>                                                                                                                                                                                                                                                                      
      {                                                                                                                                                                                                                                                                                                                                                                            
          if (await accounts.GetByIdAsync(accountId, ct) is null) return Results.NotFound();                                                                                                                                                                                                                                                                                       
          var list = await chars.GetByAccountIdAsync(accountId, ct);                                                                                                                                                                                                                                                                                                               
          return Results.Ok(list.Select(c => new CharacterSummary(c.CharacterId, c.Name, c.Race, c.CharacterClass, c.Level)));                                                                                                                                                                                                                                                     
      });                
app.UseHttpsRedirection();

app.Run();

internal record CharacterSummary(int Id, string Name, string Race, string Class, int Level);