## ⚙️ Background Worker - Channel Processing Flow

```mermaid
sequenceDiagram
title Background Worker - Channel Processing Flow
participant RMQ as RabbitMQ
participant Chan as Processing Channel
participant W1 as Worker-1
participant W2 as Worker-2
participant W3 as Worker-3
participant CDB as Cosmos DB
participant AS as Analytics Service

RMQ->>Chan: Message Received
activate Chan
Chan->>Chan: Channel.WriteAsync()

par Parallel Workers
    Chan->>W1: Channel.ReadAllAsync()
    activate W1
    W1->>CDB: Store Event
    W1->>AS: Update Analytics
    W1->>Chan: Processing Complete
    deactivate W1
and
    Chan->>W2: Channel.ReadAllAsync()
    activate W2
    W2->>CDB: Store Event
    W2->>AS: Update Analytics
    W2->>Chan: Processing Complete
    deactivate W2
and
    Chan->>W3: Channel.ReadAllAsync()
    activate W3
    W3->>CDB: Store Event
    W3->>AS: Update Analytics
    W3->>Chan: Processing Complete
    deactivate W3
end
deactivate Chan
