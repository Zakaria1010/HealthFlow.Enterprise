## Expected Message Flow

When you create a patient, you should see this sequence:

Patient Service → Creates patient in SQL Server

Patient Service → Publishes PatientCreated to patient.events

Background Worker → Consumes from patient-processing queue

Background Worker → Stores in Cosmos DB ProcessedEvents

Background Worker → Publishes to analytics.events

Analytics Service → Consumes from analytics-processing queue

Analytics Service → Stores in Cosmos DB Events

Analytics Service → Sends real-time update to dashboard

Dashboard → Updates in real-time via SignalR