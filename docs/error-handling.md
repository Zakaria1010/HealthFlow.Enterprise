## 🛡️ Error Handling & Resilience Flow

sequenceDiagram
title Error Handling & Retry Mechanisms
participant Client as Client
participant Service as Microservice
participant DB as Database
participant MQ as RabbitMQ
participant DLQ as Dead Letter Queue

Client->>Service: API Request
activate Service

alt Normal Flow
    Service->>DB: Database Operation
    DB->>Service: Success
    Service->>MQ: Publish Message
    MQ->>Service: Ack
    Service->>Client: 200 OK
else Database Failure
    Service->>DB: Database Operation
    DB->>Service: Error
    Service->>Service: Retry Logic (3 attempts)
    Service->>Client: 503 Service Unavailable
else Message Queue Failure
    Service->>MQ: Publish Message
    MQ->>Service: Nack
    Service->>DLQ: Store Failed Message
    Service->>Client: 202 Accepted (Async)
    Note right of Service: Message will be retried later
end
deactivate Service
