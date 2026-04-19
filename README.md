# Fertility Management Dashboard

A full-stack prototype for managing fertility clinic workflows — including patients, donors, and embryos — built with **.NET 8 Minimal APIs, SQLite, and a lightweight frontend dashboard**.

---

## Live Demo

https://fertility-app-production.up.railway.app

---

##  Overview

This project demonstrates:
- Backend system design using Minimal APIs
- Role-based access control (Admin / Lab / Donor)
- End-to-end CRUD operations
- A simple UI interacting with live APIs

---

##  Features

### Role-Based Access (Header-driven)

| Role   | Permissions |
|--------|------------|
| Admin  | Full access (create, view, delete) |
| Lab    | Manage patients & embryos |
| Donor  | View embryo status |

---

###  Core Functionalities

- Add patients  
- View patient list  
- Delete patients (Admin only)  
- Track embryos linked to patients & donors  
- Middleware-based role authorization  
- Interactive dashboard UI  

---

##  Tech Stack

- **Backend:** .NET 8 Minimal APIs  
- **Database:** SQLite + Entity Framework Core  
- **Frontend:** HTML, CSS, Vanilla JS  
- **API Docs:** Swagger UI  
- **Deployment:** Railway  

---

##  Project Structure

```
FertilityApp/
├── Program.cs
├── fertility.db
├── wwwroot/
│   └── index.html   # Dashboard UI
├── Migrations/
├── Dockerfile
└── appsettings.json
```

---

##  How It Works

###  Flow

1. User selects a role from UI
2. UI sends requests with `role` header
3. Backend middleware extracts role
4. Access control enforced per endpoint

---

##  Role Middleware

```csharp
app.Use(async (context, next) =>
{
    var role = context.Request.Headers["role"].FirstOrDefault()?.ToLower();
    context.Items["Role"] = role ?? "unknown";
    await next();
});
