## 🔗 Component Interaction Overview

sequenceDiagram
title HealthFlow - Component Interaction Overview
participant U as User
participant F as Frontend
participant G as Gateway
participant P as Patient Service
participant A as Analytics Service
participant B as Background Worker
participant Q as RabbitMQ
participant S as SQL DB
participant C as Cosmos DB

U->>F: Access Dashboard
F->>A: Load Analytics Data
A->>C: Query Events
C->>A: Return Data
A->>F: Display Dashboard

U->>F: Register New Patient
F->>G: POST /patients
G->>P: Create Patient
P->>S: Store in SQL
P->>Q: Publish Event
P->>F: Confirm Creation

Q->>B: Process Event
B->>C: Store in Cosmos
B->>A: Update Analytics
A->>F: Real-time Update

loop Background Processing
    B->>B: Process Queue Items
    B->>C: Batch Updates
    B->>A: Aggregate Metrics
end
