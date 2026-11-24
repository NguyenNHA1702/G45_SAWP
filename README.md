# SAWP - School Administrative & Welfare Platform

**Capstone Project:** SEP490 - Group 45  
**Topic:** Primary School Administrative and Boarding Welfare Management Platform  
**Live Demo / Repository Reference:** [https://github.com/NguyenNHA1702/SAWP-Demo](https://github.com/NguyenNHA1702/SAWP-Demo)  
**Detailed Developer Documentation:** See [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md)

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
- [MailHog](https://github.com/mailhog/MailHog) – local SMTP server that catches outgoing email (see [Email (MailHog)](#email-mailhog))

### Local Configuration (`appsettings`)

Configuration files live in `SAWP.Backend/API/`:

| File | Tracked by Git? | Purpose |
|------|-----------------|---------|
| `appsettings.json` | Yes | Shared base/default settings. **Do not edit it for your machine.** |
| `appsettings.Development.json` | Yes | Shared settings for the Development environment. **Do not edit it for your machine.** |
| `appsettings.Local.example.json` | Yes | Template for your personal overrides. |
| `appsettings.Local.json` | **No** (git-ignored) | Your own machine-specific overrides (e.g. connection string). Optional. |

Files are loaded in this order, and later files override earlier ones key by key:
`appsettings.json` -> `appsettings.Development.json` -> `appsettings.Local.json`
(the launch profiles set `ASPNETCORE_ENVIRONMENT=Development`; the last file is added in `Program.cs`). Put only the keys you need to change in `appsettings.Local.json`. Because it is git-ignored, `git status` stays clean and pulls never touch it.

**First-time setup:**
```bash
cd SAWP.Backend/API
cp appsettings.Local.example.json appsettings.Local.json
```
Then edit `appsettings.Local.json` to match your local SQL Server, e.g. LocalDB `(localdb)\mssqllocaldb`, or Docker/Express with your own `User Id`/`Password`. Never commit real passwords or production keys.

**Rules for the team:**
- Only change `appsettings.json` / `appsettings.Development.json` when a setting must change for **everyone**, and commit it.
- If you add a setting that people commonly override, add it to `appsettings.Local.example.json` too.
- If you previously edited `appsettings.Development.json` for your machine, move those edits into `appsettings.Local.json`, then run `git checkout -- SAWP.Backend/API/appsettings.Development.json` to discard them before pulling.

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

### Email (MailHog)

In Development the backend sends all email to MailHog, which keeps it in memory and shows it in a web inbox.
Nothing reaches real recipients. The settings are in `appsettings.Development.json` (`Smtp` section:
`localhost:1025`, no TLS, no login). Run MailHog natively; do not use Docker for it.

1. Download the binary for your OS from the [MailHog v1.0.1 release](https://github.com/mailhog/MailHog/releases/tag/v1.0.1):
   - Windows: `MailHog_windows_amd64.exe`
   - Linux: `MailHog_linux_amd64` (then `chmod +x MailHog_linux_amd64`)
   - macOS: `brew install mailhog`
2. Start it before running the API (no arguments needed):
   ```bash
   ./MailHog_linux_amd64
   ```
   SMTP listens on port `1025`, the web inbox on port `8025`.
3. Open `http://localhost:8025` to read the emails the API sent.

Activation and password recovery emails (UC-66) contain a link to the web client. The link uses the address the
user is on (the request's `Origin`, `X-Forwarded-Host` or `Host`) only if it is listed in
`AccountLinks:FrontendOrigins`; otherwise the first entry (`http://localhost:5173` in `appsettings.Development.json`).
A request can name any host, so an unlisted address is never put in a link (password-reset poisoning). New staff accounts
start in Pending Activation without a password: open the activation email in MailHog to set the first password.
The demo accounts seeded in Development keep `Password@123`.

If MailHog is not running, "Quên mật khẩu" answers MSG59 ("Không thể gửi email lúc này...") and new accounts
are created without their activation email; the holder can request a new link later from "Quên mật khẩu".
To send through a real SMTP server instead, put its `Smtp` settings in `appsettings.Local.json`.
For production, see [docs/EMAIL_SERVER_SETUP.md](docs/EMAIL_SERVER_SETUP.md) (one-script setup:
`deploy/mail-server/sawp-mail-server.sh`).

### Startup Data and Secrets (outside Development)

On every start the API applies pending migrations and seeds the roles. What else it needs depends on the environment:

| Setting | Development | Any other environment |
|---|---|---|
| `Jwt:Key` (signs login tokens) | Taken from `appsettings.Development.json` | **Required**, at least 32 bytes, e.g. `Jwt__Key=$(openssl rand -base64 48)`. The app stops if it is missing. |
| `AccountLinks:FrontendOrigins` (addresses of the web client that emailed links may use; the first is the default) | `["http://localhost:5173"]` | **Required**, e.g. `AccountLinks__FrontendOrigins__0=https://sawp.example.edu.vn`, `AccountLinks__FrontendOrigins__1=https://sawp.truong.vn`. The app stops if the list is empty or an entry is not `http(s)://host[:port]`. Set `AllowedHosts` to the same host names (e.g. `AllowedHosts=sawp.example.edu.vn;sawp.truong.vn`) so other `Host` headers are rejected. |
| `Seed:DemoData` | Always seeded (demo accounts use `Password@123`) | `false` by default. Set `true` only on a demo server, never in production. |
| `Seed:Admin:Username`, `Seed:Admin:Email`, `Seed:Admin:Password` | Not needed (the demo `admin` exists) | Creates the first Admin account while none exists. The password must meet the password policy. |

First production start:

1. Set `Jwt__Key`, `Seed__Admin__Username`, `Seed__Admin__Email` and `Seed__Admin__Password` as environment variables
   (or in a secret store). Never put them in a committed file.
2. Start the API. The log shows `Created the first admin account ...`.
3. Remove `Seed__Admin__Password` and restart. Log in as that admin and create the other accounts in the app.

If no Admin exists and `Seed:Admin` is not set, the log warns `No admin account exists ...`.

Full procedure, security checklist and lockout recovery: [docs/FIRST_ADMIN_SETUP.md](docs/FIRST_ADMIN_SETUP.md).

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

## 4. Standard System Messages & Error Codes (SRS MSG01 - MSG50)

The system adheres strictly to the **Report 3 SRS Message Dictionary** (Section 5.3), standardizing all user feedback into 50 predefined codes (`MSG01` to `MSG50`).

### Backend Developer Usage (`Core.Constants.SystemMessages`):
```csharp
using Core.Constants;
using Core.DTOs;

// 1. Returning standardized responses from Services / Controllers:
return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);  // Toast (Success): "Lưu thông tin thành công."
return ApiResponse<bool>.Ok(true, SystemMessages.MSG18);  // Toast (Success): "Đã lưu điểm danh."
return ApiResponse<bool>.Fail(SystemMessages.MSG01);      // Error: "Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ."
return ApiResponse<bool>.Fail(SystemMessages.MSG27);      // Error: "Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này."

// 2. Dynamic Dictionary lookup by string key:
SystemMessage? msg = SystemMessages.GetByCode("MSG01");   // null-safe case-insensitive lookup
SystemMessage direct = SystemMessages.All["MSG08"];
```

### Frontend Developer Usage (`SYSTEM_MESSAGES`):
Located in `sawp-frontend/src/constants/messages.ts`:
```tsx
import { SYSTEM_MESSAGES, getSystemMessage } from '@/constants/messages';

// 1. Dictionary lookup by key:
const sysMsg = getSystemMessage("MSG08"); // or direct: SYSTEM_MESSAGES["MSG08"]

// 2. Showing standard notifications or toasts:
const code = response.data.code; // e.g. "MSG08"
const msg = code ? getSystemMessage(code) : undefined;
toast.success(msg?.content ?? response.data.message);
```

For the complete catalog of all 50 codes, contexts, and messages, consult [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md#3-standard-system-messages--error-codes-srs-msg01---msg50).

---

## 5. Documentation & Guidelines
For in-depth project structure, file locations, coding conventions, and standard AI UI generation prompt, refer to:
- 📖 [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md)
