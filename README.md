# Smart-Service-Request-Management-Portal

A production-style C# and ASP.NET Core web application built for **Contoso Support Services** implementing the **Smart Service Request Management Portal (SSRMP)**.

---

## 🚀 1. Assignment Overview & Business Scenario
Contoso Support Services historically received IT, facilities, HR, and general support inquiries through uncoordinated email threads and spreadsheets, leading to duplicated tickets, improper triage, missed SLAs, and unmeasured service quality.

The **Smart Service Request Management Portal (SSRMP)** resolves these operational bottlenecks through:
- **Strict Role-Based Dashboards**: Tailored views for Requesters, Support Agents, Team Leads, and Administrators.
- **Automated Routing**: Categories mapped directly to responsible Support Teams.
- **Workflow State Machine**: Server-side transition validation across `New -> Assigned -> In Progress -> Waiting for Requester -> Resolved -> Closed`.
- **Business-Hour SLA Engine**: Monday to Friday (09:00 - 18:00) deadline calculations, excluding weekends and rolling non-working hours forward.
- **Proactive Background Workers**: Automated SLA breach detection and 7-day auto-closure of resolved tickets.
- **Enterprise Security**: IDOR defense, internal staff comment isolation, optimistic concurrency protection, and structured audit logs with correlation IDs.

---

## 🏛 2. Clean Architecture & Project Structure
The solution is structured into clean, decoupled layers adhering to Clean Architecture principles:

```
Smart-Service-Request-Management-Portal/
├── src/
│   ├── ServicePortal.Domain/          # Core POCO entities, Enums, and Workflow Transition Rules
│   │   ├── Entities/                  # ServiceRequest, ApplicationUser, SupportTeam, etc.
│   │   ├── Enums/                     # RequestStatus, RequestPriority, AppRoles, etc.
│   │   └── Rules/                     # WorkflowValidator state machine logic
│   ├── ServicePortal.Application/     # DTOs, Core Interfaces, and Business Logic
│   │   ├── DTOs/                      # RequestFilterDto, PagedResult, DashboardDtos, etc.
│   │   ├── Interfaces/                # ISlaCalculator, IServiceRequestService, etc.
│   │   └── Services/                  # SlaCalculator (Mon-Fri 09-18 engine)
│   ├── ServicePortal.Infrastructure/  # EF Core, Migrations, Background Workers, Services
│   │   ├── Data/                      # ApplicationDbContext, DbSeeder
│   │   ├── Migrations/                # EF Core Code First Migrations
│   │   ├── BackgroundJobs/            # SlaBreachHostedService, AutoCloseResolvedHostedService
│   │   └── Services/                  # ReferenceNumberGenerator, FileStorageService, etc.
│   └── ServicePortal.Web/             # ASP.NET Core MVC Web Application
│       ├── Controllers/               # Account, Dashboard, ServiceRequest, Admin, Notifications
│       ├── Middleware/                # CorrelationIdMiddleware, UserActiveCheckMiddleware
│       ├── Views/                     # Razor Views with responsive Bootstrap 5 styling
│       └── wwwroot/                   # Custom CSS design system, Bootstrap icons, scripts
├── tests/
│   ├── ServicePortal.UnitTests/       # 38 xUnit unit tests (SLA, workflow, file validation)
│   └── ServicePortal.IntegrationTests/# 7 xUnit integration tests (AC-01 through AC-10)
├── docs/
│   ├── architecture.md                # System design & architecture note
│   └── er-diagram.md                  # Detailed Mermaid Entity-Relationship diagram
├── README.md                          # Comprehensive setup & evaluation documentation
└── ServicePortal.sln                  # Master Visual Studio / .NET Solution
```

---

## 👥 3. Pre-Configured Demo Accounts

All seeded demo accounts share the password: `Pass@123`

| Role | Email | Password | Access Scope & Responsibilities |
|---|---|---|---|
| **Administrator** | `admin@contoso.com` | `Pass@123` | Full system access: users, roles, teams, categories, SLA policies, and audit logs. |
| **Team Lead** | `lead.it@contoso.com` | `Pass@123` | IT Team triage, agent assignment, reopen/escalations, team SLA metrics. |
| **Team Lead** | `lead.hr@contoso.com` | `Pass@123` | HR Team triage, agent assignment, workload oversight. |
| **Support Agent** | `agent.john@contoso.com` | `Pass@123` | IT Support Agent; accepts assignments, moves to In Progress, resolves requests. |
| **Support Agent** | `agent.sarah@contoso.com` | `Pass@123` | Facilities & Maintenance Support Agent. |
| **Requester (Verified)** | `requester1@contoso.com` | `Pass@123` | Verified employee (Alice); creates requests, tracks status, adds public comments. |
| **Requester (Unverified)**| `unverified@contoso.com` | `Pass@123` | Unconfirmed email; demonstrates **AC-01 requirement** (blocked from submitting). |

> **Tip**: An interactive **Quick Persona Switcher** toolbar is embedded at the top of every page in the application, allowing evaluators to switch between all roles with a single click without manually logging out or entering credentials.

---

## 🛠 4. Prerequisites & Setup Instructions

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
- SQL Server (LocalDB, SQL Express, or standard MSSQL instance) or SQLite.
- Git.

### Setup & Run Steps (From a Clean Machine)

```bash
# 1. Clone the repository
git clone https://github.com/keshav10009/Smart-Service-Request-Management-Portal.git
cd Smart-Service-Request-Management-Portal

# 2. Restore NuGet dependencies
dotnet restore ServicePortal.sln

# 3. Apply Entity Framework Core Migrations
dotnet ef database update --project src/ServicePortal.Infrastructure --startup-project src/ServicePortal.Web

# 4. Run the Web Application
dotnet run --project src/ServicePortal.Web --urls "http://localhost:5055"
```
Open your browser and navigate to: **`http://localhost:5055`**

---

## 🧪 5. Automated Testing Suite

The entire test suite executes via a single documented command:

```bash
dotnet test ServicePortal.sln
```

### Test Suite Summary: **45 Total Tests (45 Passed, 0 Failed, 0 Skipped)**
- **Unit Tests (38 Tests)**:
  - Business-hour SLA calculation within the same day.
  - Multi-day and weekend-spanning SLA calculations (Friday afternoon -> Monday).
  - Pre-workday and post-workday roll-forward logic.
  - Health state calculations (`Healthy`, `Approaching`, `Breached`, `Achieved`).
  - Workflow state machine: `New -> Assigned` permission rules.
  - Workflow state machine: Mandatory public comments on `Waiting for Requester`.
  - Workflow state machine: Mandatory resolution summary and category on `Resolved`.
  - Workflow state machine: 7-day calendar window for reopening resolved tickets.
  - Rejection of transitions on `Closed` tickets.
  - File attachment extension whitelisting and 10MB size constraints.
- **Integration Tests (7 Tests)**:
  - **AC-01**: Verified requester creates ticket and gets unique reference number (`SR-YYYY-NNNNNN`).
  - **AC-02**: IDOR prevention: Requester A cannot access Requester B's ticket.
  - **AC-03**: Internal comments are strictly hidden from requesters in views and exports.
  - **AC-04**: Invalid status transitions submitted directly to the server are rejected.
  - **AC-05**: Team leads can only assign tickets to active agents in their own team.
  - **AC-07**: Background worker automatically closes resolved tickets older than 7 days.
  - **AC-10**: Optimistic concurrency tokens prevent silent overwrites.

---

## 📋 6. Key Acceptance Criteria Validation Matrix

| Criterion ID | Requirement | Implementation & Validation Evidence |
|---|---|---|
| **AC-01** | Verified requester submits request & receives unique reference number. | Enforced in `ServiceRequestService.CreateRequestAsync`. Unverified users are blocked with error. Reference number format: `SR-YYYY-NNNNNN`. Tested in `AC01_VerifiedRequester_SubmitsValidRequest_ReceivesUniqueReferenceNumber`. |
| **AC-02** | Unauthorized user cannot view or modify another requester's ticket (IDOR defense). | Evaluated in `ServiceRequestService.GetByIdAsync` and `SearchRequestsAsync`. Unauthorized query returns `null`/`Forbid`. Tested in `AC02_IDOR_RequesterCannotAccessAnotherRequestersTicket`. |
| **AC-03** | Internal comments never appear in requester screens, exports, or responses. | `IsInternal` comments are stripped when the viewer is a Requester. Tested in `AC03_InternalComments_NeverAppearInRequesterViewsOrExports`. |
| **AC-04** | Invalid status transitions are rejected on the server. | Server-side validation via `WorkflowValidator.ValidateTransition`. Tested in `AC04_InvalidStatusTransitions_RejectedByServer`. |
| **AC-05** | Team lead can assign only to active agents in that team. | Checked in `ServiceRequestService.AssignRequestAsync` by comparing `targetAgent.SupportTeamId` to `request.SupportTeamId`. Tested in `AC05_TeamLead_CanOnlyAssignToActiveAgentInSameTeam`. |
| **AC-06** | SLA breach state calculated and recorded by policy. | Business-hour calculation engine in `SlaCalculator` + `SlaBreachHostedService` background worker. |
| **AC-07** | Automatic closure closes eligible resolved requests with history entry. | `AutoCloseResolvedHostedService` detects `ResolvedAt <= Now - 7 days` and marks `Closed` with history note. Tested in `AC07_BackgroundJob_AutoClosesEligibleResolvedRequests`. |
| **AC-08** | Search, filter, sorting, and pagination combined without exposing unauthorized records. | Implemented in `ServiceRequestService.SearchRequestsAsync` with server-side pagination, sorting, and role scoping. |
| **AC-09** | CSV export contains only authorized and filtered rows. | `ServiceRequestService.ExportCsvAsync` executes identical authorized queries and omits internal notes. |
| **AC-10** | Concurrent updates do not silently overwrite data; clear conflict message. | `[ConcurrencyCheck] RowVersion` token verified on transitions. Tested in `AC10_ConcurrentUpdates_ProduceConflictAndDoNotSilentlyOverwrite`. |
| **AC-11** | Errors logged with correlation ID without leaking sensitive technical details. | `CorrelationIdMiddleware` attaches `X-Correlation-Id`, structured logs record it, and friendly `/Home/Error` displays it safely. |
| **AC-12** | Meaningful automated test suite covering business rules and authorization. | 38 Unit Tests + 7 Integration Tests (45 total tests, all passing). |

---

## ⚡ 7. Edge Cases Handled

1. **Weekend and Off-Hours SLA Calculations**:
   - Tickets logged on Friday at 17:30 with an 8-hour target consume 30 minutes on Friday and advance the remaining 7.5 hours to Monday 09:00, expiring on Monday at 16:30.
   - Tickets created during Saturday or Sunday roll forward to Monday 09:00 before counting hours.
2. **Duplicate Request Warning with Acknowledgment**:
   - Requesters are prompted if an active ticket in the same category with a similar title was created in the last 7 days. Users can acknowledge and continue or cancel.
3. **Optimistic Concurrency & Lost Updates**:
   - Prevents stale browser tabs from overwriting new agent assignments or resolution details.
4. **Non-Blocking Notifications**:
   - In-app notification and email dispatch errors are caught in resilient try/catch blocks so secondary communication issues never roll back core business transactions.
5. **Directory Traversal in Attachments**:
   - Attachments are stored outside the public web root in `App_Data/Uploads` with GUID stored filenames, safe content-types, and sanitized download paths.

---

## 🤖 8. AI & Copilot Utilization Report

- **Architecture Scaffolding**: Leveraged AI tools to design a clean 4-tier domain-driven architecture following Microsoft .NET guidelines.
- **SLA Business Hours Engine**: Used AI pair programming to formulate test cases for boundary conditions (overnight, weekend hops, off-hour submissions).
- **Unit and Integration Test Synthesis**: Generated 45 comprehensive automated tests covering negative security assertions, IDOR boundaries, concurrency collisions, and automated workers.
- **Refactoring & Optimization**: Refactored concurrency token mapping to support cross-database compatibility (SQL Server and SQLite).

---

## 🔮 9. Known Limitations & Future Improvements
- **Public Holiday Calendar**: The current SLA calculator handles weekends; integrating national holiday calendars (e.g. via Nager.Date API) would further enrich SLA scheduling.
- **Real-Time Push Notifications**: In-app notifications currently poll on layout load; integrating ASP.NET Core SignalR would enable real-time push toast alerts.
- **Multi-Tenancy**: The current release supports one organization; partitioning by tenant ID could enable multi-organization SaaS deployments.
