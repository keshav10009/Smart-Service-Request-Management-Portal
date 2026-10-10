# Smart Service Request Management Portal (SSRMP) - Entity-Relationship Diagram

## Entity-Relationship Diagram (Mermaid)

```mermaid
erDiagram
    APPLICATION_USER ||--o{ SERVICE_REQUEST : "submits (Requester)"
    APPLICATION_USER ||--o{ SERVICE_REQUEST : "assigned to (Assignee)"
    APPLICATION_USER }o--|| SUPPORT_TEAM : "belongs to"
    APPLICATION_USER ||--o{ REQUEST_COMMENT : "authors"
    APPLICATION_USER ||--o{ NOTIFICATION : "receives"

    SUPPORT_TEAM ||--o{ CATEGORY : "routes"
    SUPPORT_TEAM ||--o{ SERVICE_REQUEST : "triage pool"

    CATEGORY ||--o{ SERVICE_REQUEST : "classifies"

    SERVICE_REQUEST ||--o{ REQUEST_COMMENT : "contains"
    SERVICE_REQUEST ||--o{ REQUEST_ATTACHMENT : "includes"
    SERVICE_REQUEST ||--o{ REQUEST_STATUS_HISTORY : "tracks transitions"
    SERVICE_REQUEST ||--o{ ASSIGNMENT_HISTORY : "tracks reassignments"
    SERVICE_REQUEST ||--o{ NOTIFICATION : "relates to"

    SLA_POLICY {
        int Id PK
        int Priority
        int ResponseTargetHours
        int ResolutionTargetHours
        bool IsActive
        datetime UpdatedAt
    }

    REFERENCE_SEQUENCE {
        int Year PK
        int LastNumber
        byte[] RowVersion
    }

    AUDIT_LOG {
        int Id PK
        string ActorId
        string ActorName
        string Action
        string EntityName
        string EntityId
        datetime Timestamp
        string Details
        string CorrelationId
        string IpAddress
    }

    APPLICATION_USER {
        string Id PK
        string DisplayName
        string Department
        bool IsActive
        int SupportTeamId FK
        datetime CreatedAt
        string Email
        bool EmailConfirmed
    }

    SUPPORT_TEAM {
        int Id PK
        string Name
        string Description
        bool IsActive
        datetime CreatedAt
    }

    CATEGORY {
        int Id PK
        string Name
        string Description
        int SupportTeamId FK
        bool IsActive
        datetime CreatedAt
    }

    SERVICE_REQUEST {
        int Id PK
        string ReferenceNumber UK
        string Title
        string Description
        string RequesterId FK
        int CategoryId FK
        int SupportTeamId FK
        string AssigneeId FK
        int Priority
        int Status
        int PreferredContactMethod
        datetime CreatedAt
        datetime FirstResponseDueAt
        datetime ResolutionDueAt
        datetime ResolvedAt
        datetime ClosedAt
        bool IsSlaBreached
        string ResolutionSummary
        int ResolutionCategory
        string ReopenReason
        string CorrectionNotes
        byte[] RowVersion
    }

    REQUEST_COMMENT {
        int Id PK
        int ServiceRequestId FK
        string AuthorId FK
        bool IsInternal
        string Body
        datetime CreatedAt
    }

    REQUEST_ATTACHMENT {
        int Id PK
        int ServiceRequestId FK
        string OriginalFileName
        string StoredFileName
        string ContentType
        long FileSizeBytes
        string UploaderId FK
        datetime CreatedAt
    }

    REQUEST_STATUS_HISTORY {
        int Id PK
        int ServiceRequestId FK
        int FromStatus
        int ToStatus
        string ChangedById FK
        string Reason
        datetime CreatedAt
    }

    ASSIGNMENT_HISTORY {
        int Id PK
        int ServiceRequestId FK
        string PreviousAssigneeId FK
        string NewAssigneeId FK
        string AssignedById FK
        string Reason
        datetime CreatedAt
    }

    NOTIFICATION {
        int Id PK
        string RecipientId FK
        int Type
        string Title
        string Message
        int RelatedRequestId FK
        bool IsRead
        datetime CreatedAt
    }
```

## Modeling Decisions & Indexing
1. **Concurrency Tokens**: `ServiceRequest.RowVersion` and `ReferenceSequence.RowVersion` are marked as concurrency tokens.
2. **Indexes**:
   - `ServiceRequests.ReferenceNumber` (Unique Index)
   - `ServiceRequests.Status`, `Priority`, `CategoryId`, `AssigneeId`, `RequesterId`, `CreatedAt`, `ResolutionDueAt`, `IsSlaBreached`
   - `AuditLogs.Timestamp`, `CorrelationId`, `[EntityName, EntityId]`
   - `Notifications.[RecipientId, IsRead]`
3. **Delete Behaviors**:
   - `ServiceRequest` deletions cascade to `RequestComment`, `RequestAttachment`, and `RequestStatusHistory`.
   - `ApplicationUser` delete is `Restrict` on tickets and comments to preserve full audit trails.
