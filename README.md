# Customer International Payments Portal (Secure)

**Student:** Muhammad Shaikh   
**Repo:** `insy7314-poe-part-3-muhammad-shaikh`

A secure full-stack demo of an international payments portal with **customer** and **employee** sides, hardened against common web attacks and wired into a **DevSecOps** pipeline (CircleCI + security scanners + linting).

https://youtu.be/ikxKAMWed58
---

## Table of Contents

- [Overview](#overview)
- [Technology Stack](#technology-stack)
- [Features](#features)
- [How to Run the App (Local)](#how-to-run-the-app-local)
- [API Overview](#api-overview)
- [Data Model](#data-model)
- [Field Validation Rules](#field-validation-rules)
- [Security Architecture](#security-architecture)
  - [Authentication & Password Security](#authentication--password-security)
  - [Session Security & CSRF](#session-security--csrf)
  - [Input Validation & SQL Injection](#input-validation--sql-injection)
  - [XSS & Clickjacking](#xss--clickjacking)
  - [Transport Security (MitM)](#transport-security-mitm)
  - [Brute Force / Abuse Protection](#brute-force--abuse-protection)
- [Employee Portal & Static Login](#employee-portal--static-login)
- [DevSecOps Pipeline (CircleCI)](#devsecops-pipeline-circleci)
- [Changelog – Improvements from Part 2](#changelog--improvements-from-part-2)
- [Limitations / Not Implemented](#limitations--not-implemented)
- [How to Demonstrate in the Video](#how-to-demonstrate-in-the-video)

---

## Overview

The **Customer International Payments Portal** allows:

- **Customers** to:
  - Register securely.
  - Log in using a hardened process.
  - Capture **international payments** (amount, currency, payee account, SWIFT/BIC).
  - View their own payment history and status.

- **Employees** to:
  - Log in with **pre-seeded employee accounts** (no open registration).
  - View all customer payments in different states.
  - **Verify** payee details & SWIFT code.
  - **Submit** verified items to “SWIFT” (simulated).

The system is designed to show secure coding practices and defence against:

- Session hijacking  
- Clickjacking  
- SQL Injection  
- Cross-Site Scripting (XSS)  
- CSRF  
- Man-in-the-Middle (MitM)  
- Brute-force / automated abuse  
- Secret leakage in the repo

---

## Technology Stack

**Backend (API)**  
- ASP.NET Core 8 (C#)  
- Entity Framework Core (SQLite)  
- ASP.NET `PasswordHasher<T>` (PBKDF2 + per-user salt)  

**Frontend (Client)**  
- React (Vite) – SPA (Single Page Application)  
- Plain CSS for styling (`App.css`)  

**Database**  
- SQLite (local file: `bankportal.db`)  

**DevSecOps / Tooling**  
- **CircleCI** pipeline:
  - `.NET` build + (optional) tests
  - React build
  - Security scans
- **Security tools**:
  - **Gitleaks** – secret scanning
  - **Semgrep** – static analysis (OWASP Top 10 + secrets + custom rules)
  - **Trivy** – filesystem scan (vulns, misconfig, secrets)
  - `dotnet list package --vulnerable`
  - `npm audit --audit-level=high`
  - `npm run lint` (ESLint over React client)

---

## Features

### Customer Side

- Registration with **strict input validation** (both client-side RegEx and server-side data annotations).
- Login using:
  - Username
  - Account number
  - Password
- Create international payment:
  - Amount (validated and stored as **cents** to avoid floating point issues).
  - Currency (allow-list: `ZAR`, `USD`, `EUR`, `GBP`, `AUD`, `CAD`, `JPY`, `CNY`).
  - Provider (default: `SWIFT`).
  - Payee account number.
  - SWIFT/BIC code.
- See **own payments** with status:
  - `PendingVerification`
  - `Verified`
  - `SubmittedToSwift`

### Employee Side

- Pre-seeded **employee users** – **no registration** form for employees.
- Login with same login endpoint (server checks `IsEmployee` flag).
- Employee portal:
  - Filter grid by status.
  - Verify payments (sets `IsVerified`, `VerifiedByEmployeeId`, `VerifiedAt`).
  - Submit verified payments to SWIFT (sets `SubmittedToSwift`, `SubmittedAt`).

---

## How to Run the App (Local)

### 1. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/)  
- Node.js 20+ and npm (only needed if you want to rebuild the client)  

### 2. Clone the repo

bash
git clone <your-clone-url>
cd insy7314-poe-part-3-muhammad-shaikh
3. Build the React client (only if you need to rebuild)
bash
Copy code
cd bankportal.client
npm install
npm run build
This produces a static bundle that the ASP.NET server can serve from wwwroot (depending on how you copied/build assets in your solution).

4. Run the ASP.NET Core API
bash
Copy code
cd ../BankPortal.Server
dotnet run
Check the console for the actual URL, typically something like:

https://localhost:7xxx

Browse to that HTTPS URL – the React SPA will load and talk to the API on the same origin.

Note: EF Core Migrate() is called on startup, so the SQLite database schema is created/updated automatically.

**API Overview**
High-level endpoints (not all fields shown):

Auth
GET /api/auth/csrf

Issues readable XSRF-TOKEN cookie for SPA.

GET /api/auth/me

Returns { id, username, isEmployee } based on HttpOnly AUTH cookie.

POST /api/auth/register

Customer registration. Body: RegisterDto.

POST /api/auth/login

Login with CSRF token. Body: LoginDto.

POST /api/auth/logout

Clears AUTH cookie.

Payments (Customer)
GET /api/payments

Returns logged-in customer’s payments.

POST /api/payments

Creates a payment for the current user.

Fields include amount, currency, payeeAccount, swiftBic, idempotencyKey.

Uses idempotency key to avoid duplicate submissions.

**Employee Payments**
GET /api/employee/payments?status=PendingVerification|Verified|SubmittedToSwift

Returns payments with status filter.

POST /api/employee/payments/{id}/verify

Marks payment as verified by the current employee.

POST /api/employee/payments/{id}/submit

Marks verified payment as submitted to SWIFT.

**Data Model**
User
Key fields (simplified):

int Id

string FullName

string IdNumber

string AccountNumber

string Username

string PasswordHash

bool IsEmployee – differentiates staff from customers.

Passwords are never stored in plain text; only PasswordHash is stored, created using ASP.NET Core’s PasswordHasher<User> (PBKDF2 + salt).

**Field Validation Rules**
Registration (Customer)
Server-side (C# [RegularExpression] / [StringLength]) and Client-side (React RegEx) are aligned:

FullName

RegEx: ^[A-Za-z ,.'-]{2,60}$

Only letters, spaces, comma, period, apostrophe, hyphen; length 2–60.

IdNumber

RegEx: ^[0-9A-Za-z-]{6,20}$

Letters, digits, hyphen; 6–20 chars.

AccountNumber

RegEx: ^[0-9]{8,20}$

Digits only; 8–20 chars.

Username

RegEx: ^[a-zA-Z0-9_.-]{3,30}$

Letters, digits, underscore, dot, hyphen; 3–30 chars.

Password

Length: 8–64 (enforced client + server).

This is allow-listing, not just weak “contains” checks.

Security Architecture
Authentication & Password Security
Passwords are hashed using ASP.NET Core PasswordHasher<User>:

Algorithm: PBKDF2 with per-user salt and configurable iteration count.

No plaintext or reversible passwords anywhere in the DB.

Login:

AuthController.Login uses _hasher.VerifyHashedPassword.

On success, sets HttpOnly cookie:

Name: AUTH

Value: user id

Flags: HttpOnly = true, Secure = true, SameSite = Lax.

Why it matters:

Prevents attackers from getting cleartext passwords even if DB is leaked.

HttpOnly cookie prevents JavaScript reading the auth token (protects against XSS stealing session).


**Session Security & CSRF**
Anti-Forgery (IAntiforgery) configured in Program.cs:

HeaderName = "X-CSRF-TOKEN".

Cookie is Secure + SameSite = Lax.

Frontend gets a readable XSRF-TOKEN cookie from GET /api/auth/csrf, then sends it in the X-CSRF-TOKEN header.

Sensitive endpoints (login, logout, payments, employee actions) require a valid token.

Protects against:

CSRF: attacker website cannot forge logged-in user actions because:

Cookies alone aren’t enough; CSRF token must match.

SameSite=Lax restricts when cookies are sent cross-site.

Input Validation & SQL Injection
Entity Framework Core uses parameterised SQL by default.

No dynamic string concatenation for queries.

All critical fields (names, account numbers, etc.):

Whitelisted via server-side data annotations.

Mirrored in frontend RegEx with live validation and error messages.

Protects against:

SQL Injection (combined with EF Core and strict input formats).

Random special characters and scripts being stored as data.

XSS & Clickjacking
In Program.cs a middleware adds security headers:

X-Content-Type-Options: nosniff

X-Frame-Options: DENY

Referrer-Policy: no-referrer

Permissions-Policy: geolocation=(), microphone=(), camera=()

Content-Security-Policy:

default-src 'self';

frame-ancestors 'none';

object-src 'none';

base-uri 'self';

Combined with React’s default escaping, this greatly reduces:

Reflected and stored XSS (no inline scripts, tight CSP, React auto-escaping).

Clickjacking (frame-ancestors none + X-Frame-Options DENY).

Transport Security (MitM)
app.UseHsts(); and app.UseHttpsRedirection(); force HTTPS.

Cookies (AUTH, antiforgery) have Secure = true.

Protects against:

Man-in-the-Middle attacks where HTTP traffic could otherwise be sniffed or modified.

Session cookies traveling in the clear.

Brute Force / Abuse Protection
Rate limiting in Program.cs:

Policy "auth" – used for login:

~10 requests per minute.

Policy "general" and "payment" – used for other endpoints.

Protects against:

Online brute-force attempts on login.

Abusive automation / accidental flood.

**Employee Portal & Static Login**
Employees are pre-created in the database (seed or manual insert).

User.IsEmployee = true marks staff accounts.

There is no employee registration form in the UI (requirement: static login).

The same login endpoint is used, but the frontend:

Shows a special “Employee Payments” button only when isEmployee = true from /api/auth/me or login response.

Example seeded employee (your actual values may differ):

Username: ops1

Account number: 99999999

Password: Employee@123

If these are changed in code/seed, update the README & demo accordingly.

**Employee functions:**

View all payments filtered by status.

For each row:

Verify: sets IsVerified, Status = "Verified", VerifiedByEmployeeId, VerifiedAt.

Submit to SWIFT: sets SubmittedToSwift, Status = "SubmittedToSwift", SubmittedAt.

**DevSecOps Pipeline (CircleCI)**
The repo contains a CircleCI config (.circleci/config.yml) that runs on pushes to GitHub.

Jobs
backend_build_test

Image: mcr.microsoft.com/dotnet/sdk:8.0

Steps:

Checkout code.

Restore NuGet packages (with cache).

dotnet build for BankPortal.Server.

(Optional) dotnet test if a test project exists.

Store test results (if any) as artifacts.

frontend_build

Installs Node.js 20.

Restores npm dependencies using npm ci.

Builds the React client via npm run build.

Caches ~/.npm.

Stores bankportal.client/dist as CircleCI artifacts.

security_scans

Reuses dotnet_sdk image, installs required tools:

Node.js 20

Python3 + pip

Gitleaks (secret scanning):

Scans working directory.

Outputs gitleaks.sarif artifact.

Semgrep (SAST):

Runs semgrep ci with:

p/owasp-top-ten

p/secrets

Local .semgrep.yml

Outputs semgrep.sarif.

.NET Vulnerable Packages:

dotnet list package --vulnerable on BankPortal.Server.

npm audit:

npm audit --audit-level=high in bankportal.client.

Trivy:

trivy fs --scanners vuln,secret,misconfig --severity HIGH,CRITICAL ...

Outputs trivy.sarif.

All reports are stored under /tmp/artifacts as CircleCI artifacts.

**Linting (Client)**
bankportal.client/package.json adds:

json
Copy code
"scripts": {
  "dev": "vite",
  "build": "vite build",
  "lint": "eslint .",
  "preview": "vite preview"
}
A basic .eslintrc.cjs config exists so you can run:



cd bankportal.client
npm run lint
This satisfies the “lint tests” part of the pipeline requirement.

**Changelog – Improvements from Part 2**
From Part 2 to Part 3, the following improvements were made:

**Security Hardening**

Added employee portal with proper separation between customers and staff.

Extended Payment model with IsVerified, SubmittedToSwift, VerifiedByEmployeeId, VerifiedAt, SubmittedAt.

Ensured employee actions are protected by:

Authenticated AUTH cookie.

Anti-forgery token.

Server-side checks for IsEmployee.

Improved React frontend with strict allow-list RegEx and live field-level validation.

**Session & Auth
**
Clarified and documented the use of HttpOnly + Secure cookies.

AuthController.Me and AuthController.Login now return isEmployee, enabling safe UI branching on the client.

**UI/UX Improvements**

Replaced raw, unstyled components with a card-based layout (.app-shell, .app-card).

Added a clear toolbar with:

“Register (Customer)”

“Login”

“Make Payment”

“My Payments”

“Employee Payments” (for staff).

Improved forms with consistent labels, hints, and inline error messages.

**Employee Workflow
**
Implemented full verification & submission flow:

Employee sees a table of payments by status.

“Verify” button marks payment as verified and records who did it.

“Submit to SWIFT” marks it as completed.

The states clearly match the description: pending → verified → submitted.

**DevSecOps / Pipeline**

Migrated away from restricted images to public dotnet SDK base image.

Added:

Gitleaks secrets scan.

Semgrep SAST for both C# and JS.

Trivy filesystem scan (vulns/misconfig/secrets).

npm audit --audit-level=high.

dotnet list package --vulnerable.

ESLint script for client (npm run lint).

**Documentation**

Produced this comprehensive README explaining:

Architecture

Security design

DevSecOps tooling

Improvements from Part 2

