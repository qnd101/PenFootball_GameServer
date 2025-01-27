using PenFootball_GameServer.Hubs;
using PenFootball_GameServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PenFootball_GameServer.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Policy;
using System.Net.Http;
using PenFootball_Server.Services;
using System.Text.Json;
using System.Net.NetworkInformation;
using PenFootball_GameServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ConnectionSettings>(builder.Configuration.GetSection("ConnectionSettings"));

var tokenkeysettings = new TokenKeySettings();
var entrancesettings = new EntranceSettings();
//먼저 메인 서버로부터 JWT 키를 받아와야 함
//이때 계정 이름과 비밀번호를 이용함
try
{
    var consettings = (builder.Configuration.GetSection("ConnectionSettings") ?? throw new Exception("main server path not found in config file"))
        .GetChildren()
        .ToDictionary(x => x.Key, x => x.Value ?? throw new Exception("Something wrong with config"));
    using var client = new HttpClient()
    {
        BaseAddress = new Uri(consettings["Path"])
    };
    var body = new { Username = consettings["Username"], Password = consettings["Password"] };
    var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    var response = await client.PostAsync("/api/servers/initialize", content);
    if (!response.IsSuccessStatusCode)
        throw new Exception($"POST operation failed. Error: {await response.Content.ReadAsStringAsync()}");
    var initdata = await response.Content.ReadFromJsonAsync<InitData>() ?? throw new Exception("Wrong Data Format Recieved");
    tokenkeysettings.Secret = initdata.Secret;
    entrancesettings.SettingsList = initdata.EntrancePolicy;
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    Environment.Exit(1); //오류나면 탈주
}

// Add services to the container.
//builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR().AddHubOptions<GameHub>(options =>
{
    options.EnableDetailedErrors = true;
}); ;
builder.Services.AddHostedService<TimerHostedService>();
builder.Services.AddSingleton<IGameDataService, GameDataService>();
builder.Services.AddSingleton<IPosterService, PosterService>();
builder.Services.AddSingleton<EntranceSettings>(entrancesettings);
builder.Services.AddSingleton<IGlobalChatService, GlobalChatService>();
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
var corsoriginsstring = builder.Configuration.GetValue<string>("CORSOrigins") ?? "";
//Remove After React project goes in static files
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins(builder.Configuration.GetValue<string>("CORSOrigins")?.Split(";") ?? throw new InvalidOperationException("CorsOrigins not found."))
                         .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                      });
}); builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = "penfootball-server",
        ValidAudience = "penfootball-frontend",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenkeysettings.Secret))
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully");
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }

    };
});

builder.Services.AddAuthorization();
var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
    //app.UseHttpsRedirection();
}

//app.UseStaticFiles();

app.UseRouting();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

//app.MapRazorPages();
app.MapHub<GameHub>("/gameHub");
app.MapControllers();

app.Run();
