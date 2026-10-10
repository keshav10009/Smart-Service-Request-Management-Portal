# Smart-Service-Request-Management-Portal

A production-style C# and ASP.NET Core web application for **Contoso Support Services** implementing the **Smart Service Request Management Portal (SSRMP)**.

## 🚀 Overview
The Smart Service Request Management Portal (SSRMP) enables employees and customers to submit service requests, attach supporting files, track progress in real time, communicate with support agents through chronological immutable activity timelines, and receive in-app and email notifications.

Support agents, team leads, and system administrators manage the request lifecycle through role-based dashboards with automated team routing, business-hour SLA breach monitoring, concurrency safety, and audit logging.

---

## 🏛 Clean Architecture
The solution is organized into layered projects:

```
Smart-Service-Request-Management-Portal/
├── src/
│   ├── ServicePortal.Domain/          # Core POCO entities, enums, workflow state machine rules
│   ├── ServicePortal.Application/     # DTOs, interfaces, SLA calculator, business services
│   ├── ServicePortal.Infrastructure/  # EF Core DbContext, migrations, seed data, background workers
│   └── ServicePortal.Web/             # ASP.NET Core MVC, modern responsive UI, controllers, security
├── tests/
│   ├── ServicePortal.UnitTests/       # 15+ xUnit tests for SLA, state machine, and business logic
│   └── ServicePortal.IntegrationTests/# Integration tests with isolated database and WebApplicationFactory
└── docs/                              # Architecture diagrams and system design documentation
```

---

## 👥 Pre-Configured Demo Accounts

| Role | Email | Password | Scope & Responsibilities |
|---|---|---|---|
| **Administrator** | `admin@contoso.com` | `Pass@123` | Full system access, users, teams, categories, SLA policies, audit logs |
| **Team Lead** | `lead.it@contoso.com` | `Pass@123` | IT Team triage, agent assignment, reopen/escalations, team SLA metrics |
| **Team Lead** | `lead.hr@contoso.com` | `Pass@123` | HR Team triage, agent assignment, SLA monitoring |
| **Support Agent** | `agent.john@contoso.com` | `Pass@123` | IT Support Agent; accepts assignments, updates to In Progress, resolves |
| **Support Agent** | `agent.sarah@contoso.com` | `Pass@123` | Facilities & General Support Agent |
| **Requester** | `requester1@contoso.com` | `Pass@123` | Verified employee; creates requests, tracks status, adds public comments |
| **Requester (Unverified)** | `unverified@contoso.com` | `Pass@123` | Account without email verification; restricted from submitting requests |

---

## ⚙️ Core Technical Features
- **SLA Engine**: Calculates due times using Mon-Fri 09:00 - 18:00 business hours, automatically skipping weekends and rolling off-hours forward.
- **Workflow State Machine**: Server-side enforced transitions (`New -> Assigned -> In Progress -> Waiting for Requester -> Resolved -> Closed`).
- **Immutable Comments & IDOR Defense**: Requesters are strictly isolated to their own requests; internal notes are never returned in queries or exports.
- **Automated Background Workers**:
  - `SlaBreachHostedService`: Proactively detects SLA breaches every minute and emits alerts.
  - `AutoCloseResolvedHostedService`: Automatically closes resolved tickets after 7 calendar days.
- **Concurrency Protection**: Optimistic concurrency tokens prevent silent overwrites.
- **Exporting**: Authorized server-side CSV export applying active filters and role permissions.
