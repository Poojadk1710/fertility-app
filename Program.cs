using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=fertility.db"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Seed demo data if database is empty
    if (!db.Donors.Any())
    {
        var donor1 = new Donor { Code = "DNR-101" };
        var donor2 = new Donor { Code = "DNR-102" };

        db.Donors.AddRange(donor1, donor2);
        db.SaveChanges();

        var patient1 = new Patient { Name = "Pooja", Age = 23, TreatmentStage = "Consultation" };
        var patient2 = new Patient { Name = "Asha", Age = 29, TreatmentStage = "Fertilization" };

        db.Patients.AddRange(patient1, patient2);
        db.SaveChanges();

        db.Embryos.AddRange(
            new Embryo
            {
                PatientId = patient1.Id,
                DonorId = donor1.Id,
                Status = "Stored"
            },
            new Embryo
            {
                PatientId = patient2.Id,
                DonorId = donor2.Id,
                Status = "Transferred"
            }
        );

        db.SaveChanges();
    }
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

string GetRole(HttpContext ctx) =>
    ctx.Request.Headers["role"].FirstOrDefault()?.ToLower() ?? "unknown";

// =========================
// PATIENTS
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
// DONORS
// =========================
app.MapPost("/donors", async (HttpContext ctx, Donor donor, AppDbContext db) =>
{
    if (GetRole(ctx) != "admin")
        return Results.Unauthorized();

    db.Donors.Add(donor);
    await db.SaveChangesAsync();

    return Results.Created($"/donors/{donor.Id}", donor);
});

app.MapGet("/donors", async (HttpContext ctx, AppDbContext db) =>
{
    var role = GetRole(ctx);

    if (role != "admin" && role != "lab")
        return Results.Unauthorized();

    return Results.Ok(await db.Donors.ToListAsync());
});

// =========================
// EMBRYOS
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

// =========================
// DONOR VIEW
// =========================
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

// =========================
// ADMIN-ONLY RECORDS
// =========================
app.MapGet("/admin/records", async (HttpContext ctx, AppDbContext db) =>
{
    if (GetRole(ctx) != "admin")
        return Results.Unauthorized();

    var data = await db.Embryos
        .Include(e => e.Patient)
        .Include(e => e.Donor)
        .Select(e => new
        {
            EmbryoId = e.Id,
            PatientName = e.Patient.Name,
            DonorCode = e.Donor.Code,
            Status = e.Status
        })
        .ToListAsync();

    return Results.Ok(data);
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://*:{port}");

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

    public DbSet<Patient> Patients { get; set; }
    public DbSet<Donor> Donors { get; set; }
    public DbSet<Embryo> Embryos { get; set; }
}