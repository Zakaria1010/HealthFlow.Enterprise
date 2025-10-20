import React, { useEffect, useState } from 'react'
import { 
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
  PieChart, Pie, Cell, BarChart, Bar, AreaChart, Area 
} from 'recharts';
import * as signalR from '@microsoft/signalr'
import { AddPatientForm } from './AddPatientForm'
import './Dashboard.css'

interface DashboardData {
  totalPatients: number;
  averageWaitTime: number;
  criticalPatients: number;
  admittedToday: number;
  totalEvents: number;
  eventsByType: { [key: string]: number };
  patientStatusDistribution: { [key: string]: number };
  hourlyAdmissions: { hour: string; admissions: number }[];
  waitTimeTrend: { date: string; waitTime: number }[];
  systemHealth?: SystemHealth;
}

interface SystemHealth {
  patientService: string;
  analyticsService: string;
  backgroundWorker: string;
  database: string;
  messageQueue: string;
  lastChecked: string;
}

interface RealTimeEvent {
  id: string;
  patientId: string;
  eventType: string;
  timestamp: Date;
  payload: any;
  service?: string;
}

interface Patient {
  id: string;
  firstName: string;
  lastName: string;
  status: string;
}

export enum PatientStatus {
  Admitted = 0,
  Discharged = 1,
  InTreatment = 2,
  Critical = 3
}

export const PatientStatusDisplay: Record<PatientStatus, string> = {
  [PatientStatus.Admitted]: "Admitted",
  [PatientStatus.Discharged]: "Discharged",
  [PatientStatus.InTreatment]: "InTreatment",
  [PatientStatus.Critical]: "Critical",
}

type ConnectionStatus = 'connected' | 'connecting' | 'disconnected' | 'reconnecting';

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8', '#82CA9D'];

export const Dashboard: React.FC = () => {
  const [dashboardData, setDashboardData] = useState<DashboardData | null>(null);
  const [realTimeEvents, setRealTimeEvents] = useState<RealTimeEvent[]>([]);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected');
  const [isLoading, setIsLoading] = useState(true);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());
  const [selectedPatient, setSelectedPatient] = useState<string | null>(null);
  const [patients, setPatients] = useState<Patient[]>([]);
  const [patientEvents, setPatientEvents] = useState<{ [key: string]: RealTimeEvent[] }>({});

  // Add this function to refresh data when patient is added
  const handlePatientAdded = () => {
    loadDashboardData();
    loadPatients();
    // The real-time events will automatically update via SignalR
  }

  useEffect(() => {
    initializeSignalR();
    loadDashboardData();
    loadPatients();
    
    const interval = setInterval(loadDashboardData, 30000); // Refresh every 30 seconds
    
    return () => {
      connection?.stop();
      clearInterval(interval);
    };
  }, []);

  const initializeSignalR = async () => {
    setConnectionStatus('connecting');
    
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5158/analyticsHub')
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Return delay in milliseconds before next retry
          return Math.min(retryContext.previousRetryCount * 1000, 10000);
        }
      })
      .build();

    try {
      await newConnection.start();
      setConnectionStatus('connected');
      console.log('Connected to Analytics Hub');

      // Setup event handlers
      newConnection.on('EventProcessed', (event: RealTimeEvent) => {
        console.log('New event received:', event);
        setRealTimeEvents(prev => [event, ...prev.slice(0, 9)]); // Keep last 10 events
        setLastUpdated(new Date());
        
        // Add to patient-specific events if subscribed
        if (selectedPatient === event.patientId) {
          setPatientEvents(prev => ({
            ...prev,
            [event.patientId]: [event, ...(prev[event.patientId] || []).slice(0, 19)] // Keep last 20 events per patient
          }));
        }

        // Refresh dashboard data when important events occur
        if (['PatientCreated', 'PatientStatusUpdated', 'PatientCritical'].includes(event.eventType)) {
          loadDashboardData();
        }
      });

      newConnection.on('PatientEvent', (event: RealTimeEvent) => {
        console.log('Patient-specific event:', event);
        // This event is only received if subscribed to this patient
        setPatientEvents(prev => ({
          ...prev,
          [event.patientId]: [event, ...(prev[event.patientId] || []).slice(0, 19)]
        }));
      });

      newConnection.on('LiveMetricsUpdate', (update: any) => {
        setDashboardData(prev => prev ? { ...prev, ...update } : null);
        setLastUpdated(new Date());
      });

      newConnection.on('BroadcastMessage', (message: any) => {
        console.log('Broadcast message:', message);
        // Show system notification
        showNotification(message);
      });

      newConnection.on('ConnectionEstablished', (data: any) => {
        console.log('Connection established:', data);
        setConnectionStatus('connected');
      });

      newConnection.onreconnecting((error) => {
        console.log('SignalR reconnecting due to error:', error);
        setConnectionStatus('reconnecting');
      });

      newConnection.onreconnected((connectionId) => {
        console.log('SignalR reconnected with connection ID:', connectionId);
        setConnectionStatus('connected');
        // Resubscribe to patient events after reconnection
        if (selectedPatient) {
          subscribeToPatient(selectedPatient);
        }
      });

      newConnection.onclose((error) => {
        console.log('SignalR connection closed:', error);
        setConnectionStatus('disconnected');
      });

      setConnection(newConnection);
    } catch (err) {
      console.error('SignalR Connection Error: ', err);
      setConnectionStatus('disconnected');
    }
  };

  const loadDashboardData = async () => {
    try {
      setIsLoading(true);
      const response = await fetch('http://localhost:5158/api/analytics/dashboard');
      const data = await response.json();
      setDashboardData(data);
      setLastUpdated(new Date());
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const loadPatients = async () => {
    try {
      // This would typically come from the Patient Service
      const response = await fetch('http://localhost:5003/api/patients');
      const data = await response.json();
      setPatients(data);
    } catch (error) {
      console.error('Failed to load patients:', error);
      // Mock data for demonstration
      setPatients([
        { id: '1', firstName: 'John', lastName: 'Doe', status: 'Admitted' },
        { id: '2', firstName: 'Jane', lastName: 'Smith', status: 'InTreatment' },
        { id: '3', firstName: 'Bob', lastName: 'Johnson', status: 'Critical' },
      ]);
    }
  };

  const subscribeToPatient = async (patientId: string) => {
    if (connection && connectionStatus === 'connected') {
      try {
        await connection.invoke('SubscribeToPatient', patientId);
        console.log(`Subscribed to patient ${patientId}`);
        setSelectedPatient(patientId);
        
        // Load historical events for this patient
        loadPatientEvents(patientId);
      } catch (error) {
        console.error('Failed to subscribe to patient:', error);
      }
    }
  };

  const unsubscribeFromPatient = async (patientId: string) => {
    if (connection && connectionStatus === 'connected') {
      try {
        await connection.invoke('UnsubscribeFromPatient', patientId);
        console.log(`Unsubscribed from patient ${patientId}`);
        setSelectedPatient(null);
      } catch (error) {
        console.error('Failed to unsubscribe from patient:', error);
      }
    }
  };

  const loadPatientEvents = async (patientId: string) => {
    try {
      const response = await fetch(`http://localhost:5158/api/analytics/patients/${patientId}/events`);
      const events = await response.json();
      setPatientEvents(prev => ({
        ...prev,
        [patientId]: events.slice(0, 20) // Keep only recent events
      }));
    } catch (error) {
      console.error(`Failed to load events for patient ${patientId}:`, error);
    }
  };

  const showNotification = (message: any) => {
    // Simple browser notification (requires permission)
    if ('Notification' in window && Notification.permission === 'granted') {
      new Notification('HealthFlow Alert', {
        body: message.message,
        icon: '/favicon.ico'
      });
    }
    
    // Or show in-app notification
    console.log('System Notification:', message);
  };

  const getEventTypeColor = (eventType: string): string => {
    const colorMap: { [key: string]: string } = {
      'PatientCreated': '#0088FE',
      'PatientStatusUpdated': '#00C49F',
      'PatientCritical': '#FF8042',
      'VitalSignsUpdated': '#FFBB28',
      'PatientAdmitted': '#8884D8',
      'PatientDischarged': '#82CA9D',
      'DeviceAlert': '#FF6B6B',
      'SystemAlert': '#4ECDC4'
    };
    return colorMap[eventType] || '#CCCCCC';
  };

  const getConnectionStatusColor = (status: ConnectionStatus): string => {
    const colorMap = {
      'connected': '#27ae60',
      'connecting': '#f39c12',
      'reconnecting': '#f39c12',
      'disconnected': '#e74c3c'
    };
    return colorMap[status];
  };

  if (isLoading && !dashboardData) {
    return (
      <div className="dashboard-loading">
        <div className="loading-spinner"></div>
        <p>Loading HealthFlow Dashboard...</p>
      </div>
    );
  }

  if (!dashboardData) {
    return (
      <div className="dashboard-error">
        <h2>Unable to load dashboard data</h2>
        <button onClick={loadDashboardData}>Retry</button>
      </div>
    );
  }

  const pieChartData = Object.entries(dashboardData.eventsByType).map(([name, value]) => ({
    name,
    value
  }));

  const statusDistributionData = Object.entries(dashboardData.patientStatusDistribution || {}).map(([name, value]) => ({
    name,
    value
  }));

  const currentPatientEvents = selectedPatient ? patientEvents[selectedPatient] || [] : [];

  return (
    <div className="dashboard-container">
      {/* Header */}
      <div className="dashboard-header">
        <div className="header-left">
          <h1>HealthFlow Analytics Dashboard</h1>
          <p className="last-updated">Last updated: {lastUpdated.toLocaleTimeString()}</p>
        </div>
        <div className="header-right">
          <div className="connection-status">
            <div 
              className="status-indicator"
              style={{ backgroundColor: getConnectionStatusColor(connectionStatus) }}
            ></div>
            <span className="status-text">
              {connectionStatus.charAt(0).toUpperCase() + connectionStatus.slice(1)}
            </span>
          </div>
          <button onClick={loadDashboardData} className="refresh-btn" disabled={isLoading}>
            {isLoading ? 'Refreshing...' : 'Refresh Data'}
          </button>
        </div>
      </div>
      {/* Add Patient Form - Add this section */}
      <AddPatientForm onPatientAdded={handlePatientAdded} />
      {/* Patient Selection */}
      <div className="patient-selection">
        <h3>Monitor Patient</h3>
        <div className="patient-buttons">
          {patients.map(patient => (
            <button
              key={patient.id}
              className={`patient-btn ${selectedPatient === patient.id ? 'active' : ''}`}
              onClick={() => selectedPatient === patient.id ? unsubscribeFromPatient(patient.id) : subscribeToPatient(patient.id)}
            >
              {patient.firstName} {patient.lastName}
              <span className={`patient-status ${(PatientStatusDisplay[Number(patient.status) as PatientStatus]).toLowerCase()}`}>
                {PatientStatusDisplay[Number(patient.status) as PatientStatus]}
              </span>
            </button>
          ))}
        </div>
      </div>
      {/* KPI Grid */}
      <div className="kpi-grid">
        <div className="kpi-card primary">
          <div className="kpi-icon">👥</div>
          <div className="kpi-content">
            <h3>Total Patients</h3>
            <p className="kpi-value">{dashboardData.totalPatients}</p>
            <p className="kpi-trend">+{dashboardData.admittedToday} today</p>
          </div>
        </div>

        <div className="kpi-card success">
          <div className="kpi-icon">⏱️</div>
          <div className="kpi-content">
            <h3>Avg Wait Time</h3>
            <p className="kpi-value">{dashboardData.averageWaitTime.toFixed(1)}m</p>
            <p className="kpi-trend">Target: 30m</p>
          </div>
        </div>

        <div className="kpi-card warning">
          <div className="kpi-icon">🚨</div>
          <div className="kpi-content">
            <h3>Critical Patients</h3>
            <p className="kpi-value">{dashboardData.criticalPatients}</p>
            <p className="kpi-trend">Monitoring</p>
          </div>
        </div>

        <div className="kpi-card info">
          <div className="kpi-icon">📊</div>
          <div className="kpi-content">
            <h3>Total Events</h3>
            <p className="kpi-value">{dashboardData.totalEvents}</p>
            <p className="kpi-trend">Real-time processing</p>
          </div>
        </div>
      </div>

      {/* Patient-specific Events */}
      {selectedPatient && currentPatientEvents.length > 0 && (
        <div className="patient-events-section">
          <h3>
            Real-time Events for {patients.find(p => p.id === selectedPatient)?.firstName}{' '}
            {patients.find(p => p.id === selectedPatient)?.lastName}
            <button 
              className="unsubscribe-btn"
              onClick={() => unsubscribeFromPatient(selectedPatient)}
            >
              Unsubscribe
            </button>
          </h3>
          <div className="events-feed">
            {currentPatientEvents.map((event, index) => (
              <div key={`${event.id}-${index}`} className="event-item">
                <div 
                  className="event-type-indicator"
                  style={{ backgroundColor: getEventTypeColor(event.eventType) }}
                ></div>
                <div className="event-content">
                  <div className="event-header">
                    <span className="event-type">{event.eventType}</span>
                    <span className="event-time">
                      {new Date(event.timestamp).toLocaleTimeString()}
                    </span>
                  </div>
                  <div className="event-details">
                    {event.service && <span className="event-service">From: {event.service}</span>}
                    {event.payload && (
                      <span className="event-payload">
                        {JSON.stringify(event.payload)}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Charts Section */}
      <div className="charts-grid">
        {/* Wait Time Trend */}
        <div className="chart-card">
          <h3>Average Wait Time Trend</h3>
          <ResponsiveContainer width="100%" height={300}>
            <AreaChart data={dashboardData.waitTimeTrend || []}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis label={{ value: 'Minutes', angle: -90, position: 'insideLeft' }} />
              <Tooltip />
              <Area type="monotone" dataKey="waitTime" stroke="#0088FE" fill="#0088FE" fillOpacity={0.3} />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        {/* Events by Type */}
        <div className="chart-card">
          <h3>Events by Type</h3>
          <ResponsiveContainer width="100%" height={300}>
            <PieChart>
              <Pie
                data={pieChartData}
                cx="50%"
                cy="50%"
                labelLine={false}
                label={({ name, percent }) => `${name} (${(percent * 100).toFixed(0)}%)`}
                outerRadius={80}
                fill="#8884d8"
                dataKey="value"
              >
                {pieChartData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                ))}
              </Pie>
              <Tooltip />
              <Legend />
            </PieChart>
          </ResponsiveContainer>
        </div>

        {/* Patient Status Distribution */}
        <div className="chart-card">
          <h3>Patient Status Distribution</h3>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={statusDistributionData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="name" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Bar dataKey="value" fill="#00C49F" />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Hourly Admissions */}
        <div className="chart-card">
          <h3>Today's Admissions by Hour</h3>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={dashboardData.hourlyAdmissions || []}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="hour" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="admissions" stroke="#FF8042" activeDot={{ r: 8 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Real-time Events Feed */}
      <div className="events-section">
        <h3>Real-time Events Feed</h3>
        <div className="events-feed">
          {realTimeEvents.length > 0 ? (
            realTimeEvents.map((event, index) => (
              <div key={`${event.id}-${index}`} className="event-item">
                <div 
                  className="event-type-indicator"
                  style={{ backgroundColor: getEventTypeColor(event.eventType) }}
                ></div>
                <div className="event-content">
                  <div className="event-header">
                    <span className="event-type">{event.eventType}</span>
                    <span className="event-time">
                      {new Date(event.timestamp).toLocaleTimeString()}
                    </span>
                  </div>
                  <div className="event-details">
                    Patient: {event.patientId}
                    {event.service && <span className="event-service"> • Service: {event.service}</span>}
                    {event.payload && (
                      <span className="event-payload">
                        {JSON.stringify(event.payload)}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))
          ) : (
            <div className="no-events">
              <p>Waiting for real-time events...</p>
              <p>Events will appear here as they occur</p>
            </div>
          )}
        </div>
      </div>

      {/* System Status */}
      <div className="system-status">
        <h3>System Status</h3>
        <div className="status-grid">
          <div className="status-item">
            <span className="status-label">SignalR Connection</span>
            <span 
              className="status-badge" 
              style={{ 
                backgroundColor: getConnectionStatusColor(connectionStatus) + '20',
                color: getConnectionStatusColor(connectionStatus)
              }}
            >
              {connectionStatus}
            </span>
          </div>
          <div className="status-item">
            <span className="status-label">Patient Service</span>
            <span className="status-badge healthy">Healthy</span>
          </div>
          <div className="status-item">
            <span className="status-label">Analytics Service</span>
            <span className="status-badge healthy">Healthy</span>
          </div>
          <div className="status-item">
            <span className="status-label">Background Worker</span>
            <span className="status-badge healthy">Processing</span>
          </div>
        </div>
      </div>
    </div>
  );
};