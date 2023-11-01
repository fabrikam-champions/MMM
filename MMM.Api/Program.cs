using Microsoft.EntityFrameworkCore;
using MMM.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MessagesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MainConnectionString")));

builder.Services.AddControllers(mvcOptions =>
{
    mvcOptions.InputFormatters.Add(new TextSingleValueFormatter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
