using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// =========================
// SERVICES
// =========================
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("RoleHeader", new OpenApiSecurityScheme
    {
        Name = "role",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Enter role: admin / lab / donor"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "RoleHeader"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=fertility.db"));

var app = builder.Build();

// =========================
// MIDDLEWARE
// =========================
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

// =========================
// ROLE MIDDLEWARE
// =========================
app.Use(async (context, next) =>
{
    var role = context.Request.Headers["role"].FirstOrDefault()?.ToLower();
    context.Items["Role"] = role ?? "unknown";
    await next();
});

string GetRole(HttpContext ctx) =>
    ctx.Items["Role"]?.ToString() ?? "unknown";

// =========================
// USER API
// =========================
app.MapPost("/users", async (User user, AppDbContext db) =>
{
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Ok(user);
});

app.MapGet("/users", async (HttpContext ctx, AppDbContext db) =>
{
    if (GetRole(ctx) != "admin")
        return Results.Unauthorized();

    return Results.Ok(await db.Users.ToListAsync());
});

// =========================
// PATIENT API
// =========================
app.MapPost("/patients", async (HttpContext ctx, Patient patient, AppDbContext db) =>
{
    var role = GetRole(ctx);

    if (role != "admin" && role != "lab")
        return Results.Unauthorized();

    db.Patients.Add(patient);
    await db.SaveChangesAsync();

    return Results.Created($"/patients/{patient.Id}", patient);
});

app.MapGet("/patients", async (HttpContext ctx, AppDbContext db) =>
{
    var role = GetRole(ctx);

    if (role != "admin" && role != "lab")
        return Results.Unauthorized();

    return Results.Ok(await db.Patients.ToListAsync());
});

app.MapDelete("/patients/{id}", async (HttpContext ctx, int id, AppDbContext db) =>
{
    if (GetRole(ctx) != "admin")
        return Results.Unauthorized();

    var patient = await db.Patients.FindAsync(id);

    if (patient == null)
        return Results.NotFound();

    db.Patients.Remove(patient);
    await db.SaveChangesAsync();

    return Results.Ok();
});

// =========================
// DONOR API
// =========================
app.MapPost("/donors", async (HttpContext ctx, Donor donor, AppDbContext db) =>
{
    if (GetRole(ctx) != "admin")
        return Results.Unauthorized();

    db.Donors.Add(donor);
    await db.SaveChangesAsync();
    return Results.Ok(donor);
});

// =========================
// EMBRYO API
// =========================
app.MapPost("/embryos", async (HttpContext ctx, Embryo embryo, AppDbContext db) =>
{
    var role = GetRole(ctx);

    if (role != "admin" && role != "lab")
        return Results.Unauthorized();

    var patient = await db.Patients.FindAsync(embryo.PatientId);
    var donor = await db.Donors.FindAsync(embryo.DonorId);

    if (patient == null || donor == null)
        return Results.BadRequest("Invalid PatientId or DonorId");

    if (string.IsNullOrWhiteSpace(embryo.Status))
        embryo.Status = "Stored";

    db.Embryos.Add(embryo);
    await db.SaveChangesAsync();

    return Results.Created($"/embryos/{embryo.Id}", embryo);
});

app.MapGet("/embryos", async (HttpContext ctx, AppDbContext db) =>
{
    var role = GetRole(ctx);

    if (role != "admin" && role != "lab")
        return Results.Unauthorized();

    return Results.Ok(await db.Embryos
        .Include(e => e.Patient)
        .Include(e => e.Donor)
        .ToListAsync());
});

app.MapGet("/donor/{donorId}/embryos", async (HttpContext ctx, int donorId, AppDbContext db) =>
{
    if (GetRole(ctx) != "donor")
        return Results.Unauthorized();

    var data = await db.Embryos
        .Where(e => e.DonorId == donorId)
        .Select(e => new
        {
            e.Id,
            e.Status
        })
        .ToListAsync();

    return Results.Ok(data);
});

app.MapGet("/", () => "Fertility API running 🚀");

app.Run();

// =========================
// MODELS
// =========================
public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string TreatmentStage { get; set; } = "";
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class Donor
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
}

public class Embryo
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public int DonorId { get; set; }
    public Donor Donor { get; set; } = null!;

    public string Status { get; set; } = "";
}

// =========================
// DB CONTEXT
// =========================
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Donor> Donors { get; set; }
    public DbSet<Embryo> Embryos { get; set; }
}