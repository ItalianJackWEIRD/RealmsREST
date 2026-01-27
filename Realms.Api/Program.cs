using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Realms.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Postgres");
    opt.UseNpgsql(cs);
});

/*
 * Firebase Auth:
 * - L'app Android manda: Authorization: Bearer <FirebaseIdToken>
 * - L'UID sta nel claim "user_id"
 *
 * Config:
 * - FIREBASE_PROJECT_ID in appsettings.Development.json o env var su Cloud Run
 */
var firebaseProjectId =
    builder.Configuration["FIREBASE_PROJECT_ID"]
    ?? throw new InvalidOperationException("Missing FIREBASE_PROJECT_ID config");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true,
            NameClaimType = "user_id" // così User.Identity.Name = uid
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "Realms API is running");

app.Run();
