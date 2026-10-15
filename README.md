# SAWP - School Administrative & Welfare Platform

**Capstone Project:** SEP490 - Group 45  
**Topic:** Primary School Administrative and Boarding Welfare Management Platform  
**Live Demo / Repository Reference:** [https://github.com/NguyenNHA1702/SAWP-Demo](https://github.com/NguyenNHA1702/SAWP-Demo)  
**Detailed Developer Documentation:** See [Documents/DEVELOPER_GUIDE.md](file:///d:/VS_Studio/G45_SAWP/Documents/DEVELOPER_GUIDE.md)

---

## 1. Project Overview

**SAWP (School Administrative and Welfare Platform)** is a specialized school administrative prototype designed for primary schools. It streamlines daily educational administration, boarding lunch service management, class schedules, attendance tracking, and school administrative requests (leave requests, class transfer requests, official document issuance) between parents, teachers, office staff, service staff, and the school board.

### Core Portals & Actors
1. **Parent Portal:** Track student attendance, submit leave requests, register/cancel school lunches, view announcements.
2. **Teacher Portal:** Manage class roster, perform daily attendance, verify leave requests, check homeroom timetable.
3. **Office Staff Portal (Giáo vụ):** Manage student/guardian profiles, handle grade assignments, import data via Excel, process document requests.
4. **Service Staff Portal (Bếp / Bán trú):** Monitor daily lunch counts, track special diets, finalize daily meal reports before cutoff times.
5. **School Board Portal (Ban Giám hiệu):** Executive dashboards, overview analytics (attendance, boarding welfare), approval of class transfers.
6. **System Admin Portal:** User account provisioning, role-based access control (RBAC), and immutable security audit logs.

---

## 2. Architecture & Tech Stack

### Backend
- **Framework:** ASP.NET Core 8 Web API (.NET 8.0 LTS)
- **Database & ORM:** Microsoft SQL Server (LocalDB / Express), Entity Framework Core 8 (Code-First)
- **Architecture Pattern:** Clean / Onion Architecture with Contract-First separation:
  - `Core`: Domain Entities, Enums, DTOs, Repository & UnitOfWork Interfaces (Zero external dependencies).
  - `Services`: Business Logic Service Interfaces & Implementations, Excel Importers (`ClosedXML`).
  - `Infrastructure`: EF Core `ApplicationDbContext`, Fluent API Configurations, Repositories, Migrations, External Integrations (`MailKit`, File Storage).
  - `API`: ASP.NET Core Controllers, Custom Middlewares (JWT, Exception Handling, Data Scope Filter), Swagger OpenAPI, and DI Composition Root.

### Frontend
- **Framework:** React 18, TypeScript, Vite
- **HTTP Client:** Axios with centralized interceptors (Bearer token handling)
- **Icons:** Lucide React
- **Design System:** Playful Geometric (Memphis Modern Pop style tailored for primary educational platforms)

---

## 3. Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- [Node.js](https://nodejs.org/) (v18+ or v20+) and `npm`
- Microsoft SQL Server LocalDB (`(localdb)\mssqllocaldb`) or SQL Server Express

### Backend Setup
```bash
# Navigate to Backend folder
cd SAWP.Backend

# Restore dependencies & build solution
dotnet restore
dotnet build

# Apply database migrations to SQL Server LocalDB
dotnet ef database update --project Infrastructure --startup-project API

# Run Web API
dotnet run --project API
```
- Swagger UI will be available at: `https://localhost:7001/swagger` (or configured HTTP port).

### Frontend Setup
```bash
# Navigate to Frontend folder
cd sawp-frontend

# Install dependencies
npm install

# Start Vite dev server
npm run dev
```
- Local dev application will be available at: `http://localhost:5173`.

---

## 4. Documentation & Guidelines
For in-depth project structure, file locations, coding conventions, and standard AI UI generation prompt, refer to:
- 📖 [Documents/DEVELOPER_GUIDE.md](file:///d:/VS_Studio/G45_SAWP/Documents/DEVELOPER_GUIDE.md)
