using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Realms.Api.Data;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;

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
            NameClaimType = "user_id" // cosi User.Identity.Name = uid
        };
    });

builder.Services.AddAuthorization();

// ============================================================================
// DEMO LOCALE: credenziali Google FINTE e usa-e-getta (NON collegate ad alcun
// account/progetto reale; la chiave RSA e' generata solo per questo). Servono
// unicamente a far COSTRUIRE StorageClient e UrlSigner senza internet, cosi i
// controller si attivano. Gli endpoint di lettura (mappa, feed, profili) non
// usano mai lo storage: queste credenziali non vengono mai realmente impiegate.
// L'unico effetto e' che l'upload foto in locale non funziona (atteso).
// In cloud, su Cloud Run, qui andrebbero le credenziali vere (ADC).
// ============================================================================
const string DemoSaJson = @"{""type"": ""service_account"", ""project_id"": ""realms-local-demo"", ""private_key_id"": ""demo-local-only"", ""private_key"": ""-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC34Mae3qFtv8S1\nBbUHkOe/67ZyHhZoYn27fGXHpzXMv8SLGyL3lxLgA/Yb0147rupQkzoqZ0gKT4gO\n8mtXC4r7w31WAi3XimdfPBa2PtJL9I591mJrj9GufmmG/RPMwbTRo89b4wK2uORb\nFdNwo/WP9/D7d8SdA3qXBhBCQWz19ghxn8sYZcuBaeaggKcnlqpr8YJFSyRBQVoE\nPcCVUW9yTPbt7C7pRlM2YQntJlWM9dedpesFBfbhVaBWZXjGsc/LAZZTeStrzIvD\nX67iOVVAr/jfiQLZH1CCjcCD5u5OHbKFHD6P1smVKJKJGqFIwydIqNDiNhIwIqOH\nlLOFqhKDAgMBAAECggEATfMhcD7z0yk9E/pv5F23CQK9TMJZgHXkVEMniIxnf715\n7fiaibhHHaVAQ0qHA6kilvba7Rfsj8ZoaOG2xGdVy2Xzr67rzRhUuygnfqnCD8YE\nU+86uNt2qdDYHowRxTcG1uppIMxrHZfi4oQtpu4yzw3uYERFGsTbRsOgx92heeDS\nvAUY0RxjAuY0/MCjJr0JD5qklicirqNgXXRGf3ichj9xeiB+GNTjS8s9sTIhqnOJ\nrwgq6NDYlglhUik16HrY4eEoyJV0fpFZ6HTee6hU1Mh9YRKo7R4YZNvqx+9ukLac\nwDqj8jLJyDVoy1zISkQ+JR1dr8El5F6+Sq8Rlqi1kQKBgQDtmuELG3vgi/37Al/D\nKTgt6E0PAf0OBjH6N3m9+wbxa48ewypyFMwNpNnvncPMrPkp4qShyITpueuRGnRG\nMTD1EwuT3HKJO0Nw6SZlOBEFWW1KtFfwMhi95Ve1omfaeUkoqcGOoC4mW+OBNeaz\nRXG1MeJTlfp/Q2tgxxgFjDUBkwKBgQDGHRNp5nJzmMz48l2dVqfMWyNySJB9/xxD\n4uSQsQy0d2zY8xjncsgfkjIJBj84vHtgN9Dej52ueN2XZTyC2WNpMdcD7+AocGb4\nYg0a5+HO2E6du/RmgqyuYwc+5vx6CCvh6CZE8ujGW75r9wMBGadUYNrMxWr4vBqX\nQhWv9nEBUQKBgFcF6bSKvWUxgLUlWnN9LlFKCqcbgFZZmIZfORyGyzUywrlum6Yq\nzc2VeiiTrLnTBHL9ynRin6OG76s2eC2ZKKgp8IyYKe7vILVC/0gFL964sRmyUZ7s\nijlKvUQOFmFjGJNnETgunJh6ASo61qMEJTBK8+zPOm7P/4zzfm3Ruzw3AoGBALg5\nJWpenqMbvc7pIWBDynlfbpDBJYvkhFYkUMKzwMq4GwGK3OesdqfU/K4jnvqVWmzY\numObTNeHERfNTf2nRKf3bqf8kYdJLpdeJi2U3wfHYSOQOe5xKT5oW76EcNbRbmz9\nwUhwUUDS4znmUmbdghoWjp/IHHb8BAYPr7cXBXJhAoGAI8zEX5ywBRCP+Wa/QDSR\n9m5KL3yX0qtkXbHLEV95HyNA/fNfqPCDCQQgFaIZ80QamsLveDb+sJ+80t5BHBi/\nOIfG2nSLuSayUNohDOjW80PFWjxaz3g9AghF5/P9vyaVgb1DFlePrl3G8HErTtb6\nU97h5gJEGv/hA7gwsotU51o=\n-----END PRIVATE KEY-----\n"", ""client_email"": ""local-demo@realms-local-demo.iam.gserviceaccount.com"", ""client_id"": ""000000000000000000000"", ""token_uri"": ""https://oauth2.googleapis.com/token""}";

var demoGoogleCred = GoogleCredential.FromJson(DemoSaJson);
var demoSaCred = (ServiceAccountCredential)demoGoogleCred.UnderlyingCredential;

builder.Services.AddSingleton(StorageClient.Create(demoGoogleCred));
builder.Services.AddSingleton(UrlSigner.FromServiceAccountCredential(demoSaCred));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
}
// Enable Swagger in all environments for testing
app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "Realms API is running");

app.Run();
