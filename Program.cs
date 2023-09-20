using Microsoft.AspNetCore.Mvc;
using MyLoadBalancer.Router;
using Newtonsoft.Json;
using Supabase;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var supabaseUrl = builder.Configuration.GetSection("SUPABASE_URL").Value;
var supabaseKey = builder.Configuration.GetSection("SUPABASE_KEY").Value;
var options = new SupabaseOptions
{
    AutoConnectRealtime = true,
    AutoRefreshToken = true
};

builder.Services.AddSingleton<Supabase.Client>(_ => new Supabase.Client(supabaseUrl, supabaseKey, options));

builder.Services.AddSingleton<ServerFarm>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseMiddleware<RequestInterceptor>();
app.UseRouting();

app.Run();


