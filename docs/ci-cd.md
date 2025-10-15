## 🚀 Azure DevOps CI/CD Pipeline

```mermaid
sequenceDiagram
title Azure DevOps CI/CD Pipeline
participant Dev as Developer
participant Git as GitHub
participant ADO as Azure DevOps
participant ACR as Azure Container Registry
participant AKS as Azure Kubernetes
participant AppS as App Services

Dev->>Git: Push Code
Git->>ADO: Trigger Pipeline
activate ADO

ADO->>ADO: Restore & Build
ADO->>ADO: Run Tests
ADO->>ADO: Security Scan

alt Tests Pass
    ADO->>ACR: Build & Push Docker Images
    ACR->>AKS: Deploy to Kubernetes
    ACR->>AppS: Deploy to App Services
    
    AKS->>ADO: Deployment Status
    AppS->>ADO: Deployment Status
    ADO->>Dev: Deployment Success
else Tests Fail
    ADO->>Dev: Build Failed - Notification
end
deactivate ADO
