using CyclopsMini.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using CyclopsMini.Filters;
using CyclopsMini.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DbContext (SQLite)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=cyclops.db"));

// Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ProblemDetails (RFC 7807) - global hata cevapları için
builder.Services.AddProblemDetails();

// Services
builder.Services.AddScoped<IMetricsService, MetricsService>();

// Auth: JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"] ?? "dev-key");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// DB migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// Global exception handler -> ProblemDetails döner
app.UseExceptionHandler();

// Auth middleware
app.UseAuthentication();
app.UseAuthorization();

// Swagger middleware (Dev)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Demo login (sabit kullanıcı) -> JWT üretir
app.MapPost("/auth/login", (IConfiguration cfg, LoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { message = "Username/password required" });

    // Demo: her kullanıcıyı kabul et (gerçekte DB kontrolü yapılmalı)
    var jwt = cfg.GetSection("Jwt");
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, req.Username),
    };

    var token = new JwtSecurityToken(
        issuer: jwt["Issuer"],
        audience: jwt["Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddHours(4),
        signingCredentials: creds
    );

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { token = tokenString });
});

app.MapGet("/products", async (AppDbContext db) => await db.Products.ToListAsync());


app.MapPost("/products", async (AppDbContext db, ProductDto dto) =>
{
    var p = new Product { Sku = dto.Sku, Title = dto.Title, CostPrice = dto.CostPrice, CreatedAt = DateTime.UtcNow };
    db.Products.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{p.Id}", p);
}).AddEndpointFilter(new ValidationFilter<ProductDto>()).RequireAuthorization();


app.MapPost("/sales/import", async (AppDbContext db, List<SaleDto> sales) =>
{
    var entities = sales.Select(s => new Sale
    {
        ProductId = s.ProductId,
        Quantity = s.Quantity,
        UnitPrice = s.UnitPrice,
        MarketplaceFee = s.MarketplaceFee,
        ShippingCost = s.ShippingCost,
        SaleDate = s.SaleDate
    }).ToList();


    db.Sales.AddRange(entities);
    await db.SaveChangesAsync();
    return Results.Ok(new { inserted = entities.Count });
}).AddEndpointFilter(new ValidationListFilter<SaleDto>()).RequireAuthorization();


app.MapGet("/metrics/profit", async (AppDbContext db, IMetricsService metrics, DateTime from, DateTime to, string granularity) =>
{
    var data = await metrics.GetProfitAsync(db, from, to, granularity);
    return Results.Ok(data);
});


app.MapGet("/metrics/top-products", async (AppDbContext db, IMetricsService metrics, int limit) =>
{
    var data = await metrics.GetTopProductsAsync(db, limit);
    return Results.Ok(data);
});

app.Run();

// DTO'lar ve Modeller
public record LoginRequest(string Username, string Password);

public record ProductDto(
    [Required, StringLength(64)] string Sku,
    [Required, StringLength(200)] string Title,
    [Range(0.01, double.MaxValue, ErrorMessage = "CostPrice must be greater than 0")] decimal CostPrice
);

public record SaleDto(
    [Range(1, int.MaxValue, ErrorMessage = "ProductId must be positive")] int ProductId,
    [Range(1, 1000000)] int Quantity,
    [Range(0.0, double.MaxValue)] decimal UnitPrice,
    [Range(0.0, double.MaxValue)] decimal MarketplaceFee,
    [Range(0.0, double.MaxValue)] decimal ShippingCost,
    DateTime SaleDate
);