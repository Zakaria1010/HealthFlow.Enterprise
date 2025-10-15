## 🧠 HealthFlow Enterprise POC - Complete Sequence Diagram

```mermaid
sequenceDiagram
title HealthFlow Enterprise POC - Complete Data Flow
participant Web as Web Frontend
participant API as API Gateway
participant PS as Patient Service
participant AS as Analytics Service
participant BW as Background Worker
participant RMQ as RabbitMQ
participant SQL as SQL Server
participant CDB as Cosmos DB
participant IoT as Azure IoT Hub

Note over Web, IoT: 1. PATIENT REGISTRATION FLOW
Web->>API: POST /api/patients
activate API
API->>PS: Forward Create Patient Request
activate PS
PS->>SQL: BEGIN TRANSACTION
PS->>SQL: INSERT INTO Patients
SQL->>PS: Patient Created
PS->>SQL: COMMIT
PS->>RMQ: Publish PatientCreated Event
activate RMQ
PS->>Web: SignalR: PatientCreated (Real-time)
PS->>API: HTTP 201 Created
API->>Web: Return Patient Data
deactivate PS
deactivate API

Note over Web, IoT: 2. ASYNC EVENT PROCESSING FLOW
RMQ->>BW: Deliver PatientCreated Event
activate BW
BW->>BW: Channel.WriteAsync() (Bounded Channel)

par Parallel Processing with Task.Run
    BW->>BW: Worker-1: ProcessMessageAsync()
    BW->>CDB: Store AnalyticsEvent
    BW->>AS: POST /api/analytics/events
    activate AS
    AS->>Web: SignalR: EventProcessed
    deactivate AS
and
    BW->>BW: Worker-2: ProcessMessageAsync()
    BW->>CDB: Store AnalyticsEvent
    BW->>AS: POST /api/analytics/events
    activate AS
    AS->>Web: SignalR: EventProcessed
    deactivate AS
end
deactivate BW

Note over Web, IoT: 3. REAL-TIME DASHBOARD UPDATES
Web->>AS: GET /api/analytics/dashboard
activate AS
AS->>CDB: Query Events (Last 7 Days)
CDB->>AS: Return Analytics Data
AS->>Web: Dashboard JSON Response
deactivate AS

loop Continuous Real-time Updates
    AS->>Web: SignalR: Live Metrics Update
    PS->>Web: SignalR: Patient Status Change
end

Note over Web, IoT: 4. PATIENT STATUS UPDATE FLOW
Web->>API: PUT /api/patients/{id}/status
activate API
API->>PS: Forward Status Update
activate PS
PS->>SQL: UPDATE Patients SET Status
SQL->>PS: Update Confirmed
PS->>RMQ: Publish PatientStatusUpdated
activate RMQ
PS->>Web: SignalR: StatusUpdated
PS->>API: HTTP 200 OK
API->>Web: Update Confirmation
deactivate PS
deactivate API

RMQ->>BW: Deliver StatusUpdated Event
activate BW
BW->>BW: Channel.WriteAsync()
BW->>CDB: Store Status Change Event
BW->>AS: Notify Analytics Update
deactivate BW

Note over Web, IoT: 5. IOT DEVICE DATA PROCESSING
IoT->>BW: Medical Device Telemetry
activate BW
BW->>RMQ: Publish VitalSignsEvent
activate RMQ
RMQ->>BW: Consume VitalSignsEvent
BW->>CDB: Store Device Data
BW->>AS: POST /api/analytics/vitals
activate AS
alt Critical Condition Detected
    AS->>Web: SignalR: Critical Alert
    AS->>PS: POST /api/patients/{id}/critical
    activate PS
    PS->>SQL: Update Patient Status to Critical
    PS->>Web: SignalR: PatientCritical
    deactivate PS
end
deactivate AS
deactivate BW

Note over Web, IoT: 6. HEALTH MONITORING & CI/CD
API->>PS: GET /health (Health Check)
activate PS
PS->>SQL: SELECT 1 (DB Health)
PS->>RMQ: Connection Check
PS->>API: Healthy Status
deactivate PS
API->>AS: GET /health (Health Check)
activate AS
AS->>CDB: Query Health Check
AS->>API: Healthy Status
deactivate AS
Note over Web, IoT: Azure DevOps CI/CD Pipeline
Note right of API: Build -> Test -> Docker -> Deploy to Azure