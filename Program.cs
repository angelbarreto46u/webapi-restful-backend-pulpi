using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using webapi_restful_backend_pulpi.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
/*builder.Services.AddDbContext<PulpiDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
*/

builder.Services.AddDbContext<PulpiDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null)));


var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("No se encontró la clave JWT. Configúrela mediante User Secrets o Jwt__Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe contener al menos 32 bytes aleatorios.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
    // Los permisos se consultan desde SQL Server en cada solicitud autenticada para que
    // un cambio de rol o la desactivación de una cuenta tenga efecto sin esperar a que expire el JWT.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var authorization =
            context.Request.Headers.Authorization.ToString();

            if (string.IsNullOrWhiteSpace(authorization) &&
                context.Request.Cookies.TryGetValue(
                "pulpi_access_token",
                out var cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        },

        OnTokenValidated = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.Fail("El token no contiene un identificador de usuario válido.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<PulpiDbContext>();
            var currentUser = await db.Usuarios.Include(u => u.Rol)
                .AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.Activo);
            if (currentUser?.Rol is null)
            {
                context.Fail("La cuenta no existe o está desactivada.");
                return;
            }

            if (context.Principal?.Identity is not ClaimsIdentity identity)
            {
                context.Fail("No se pudo validar la identidad del token.");
                return;
            }

            foreach (var roleClaim in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(roleClaim);
            foreach (var nameClaim in identity.FindAll(ClaimTypes.Name).ToList()) identity.RemoveClaim(nameClaim);
            foreach (var emailClaim in identity.FindAll(ClaimTypes.Email).ToList()) identity.RemoveClaim(emailClaim);
            identity.AddClaim(new Claim(ClaimTypes.Role, currentUser.Rol.Nombre));
            identity.AddClaim(new Claim(ClaimTypes.Name, currentUser.Username));
            identity.AddClaim(new Claim(ClaimTypes.Email, currentUser.Email));
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Pulpi API - Control de Gastos e Ingresos",
        Version = "v1",
        Description = "API RESTful del sistema financiero Pulpi. Los roles y los permisos se validan contra la base de datos."
    });
    /*c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingresa el token JWT sin escribir la palabra Bearer.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });*/
});

/*builder.Services.AddCors(options => options.AddPolicy("AllowReactApp", policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000")
        .AllowAnyHeader().AllowAnyMethod() .AllowCredentials()));*/


var vercelUrl = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173";

builder.Services.AddCors(options => options.AddPolicy("AllowReactApp", policy =>
    policy.WithOrigins(
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://localhost:3000",
            vercelUrl)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));        

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
