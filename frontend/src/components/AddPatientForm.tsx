import React, { useState } from 'react';
import './AddPatientForm.css';
import { PatientStatus, PatientStatusDisplay } from './Dashboard';

interface PatientFormData {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  medicalRecordNumber: string;
  status: PatientStatus;
}

interface AddPatientFormProps {
  onPatientAdded: () => void;
}

export const AddPatientForm: React.FC<AddPatientFormProps> = ({ onPatientAdded }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formData, setFormData] = useState<PatientFormData>({
    firstName: '',
    lastName: '',
    dateOfBirth: '',
    medicalRecordNumber: '',
    status: PatientStatus.Admitted
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    try {   
      // Convert the string "2" to a number, then to PatientStatus
      const numericStatus = Number(formData.status) as PatientStatus;

      const payload = {
            ...formData,
            status: numericStatus, // numeric, ready for backend
      }
      
      const response = await fetch('http://localhost:5003/api/patients', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      });

      if (response.ok) {
        // Reset form and close
        setFormData({
          firstName: '',
          lastName: '',
          dateOfBirth: '',
          medicalRecordNumber: '',
          status: PatientStatus.Admitted
        });
        setIsOpen(false);
        onPatientAdded();
        
        // Show success message
        alert('Patient added successfully!');
      } else {
        const error = await response.text();
        alert(`Error adding patient: ${error}`);
      }
    } catch (error) {
      console.error('Error adding patient:', error);
      alert('Error adding patient. Please check if the Patient Service is running.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    });
  };

  return (
    <div className="add-patient-section">
      <button 
        className="add-patient-btn"
        onClick={() => setIsOpen(true)}
      >
        + Add New Patient
      </button>

      {isOpen && (
        <div className="modal-overlay">
          <div className="modal-content">
            <div className="modal-header">
              <h2>Add New Patient</h2>
              <button 
                className="close-btn"
                onClick={() => setIsOpen(false)}
                disabled={isSubmitting}
              >
                ×
              </button>
            </div>

            <form onSubmit={handleSubmit} className="patient-form">
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="firstName">First Name *</label>
                  <input
                    type="text"
                    id="firstName"
                    name="firstName"
                    value={formData.firstName}
                    onChange={handleChange}
                    required
                    disabled={isSubmitting}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="lastName">Last Name *</label>
                  <input
                    type="text"
                    id="lastName"
                    name="lastName"
                    value={formData.lastName}
                    onChange={handleChange}
                    required
                    disabled={isSubmitting}
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="dateOfBirth">Date of Birth *</label>
                  <input
                    type="date"
                    id="dateOfBirth"
                    name="dateOfBirth"
                    value={formData.dateOfBirth}
                    onChange={handleChange}
                    required
                    disabled={isSubmitting}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="medicalRecordNumber">Medical Record Number *</label>
                  <input
                    type="text"
                    id="medicalRecordNumber"
                    name="medicalRecordNumber"
                    value={formData.medicalRecordNumber}
                    onChange={handleChange}
                    required
                    disabled={isSubmitting}
                    placeholder="e.g., MRN001"
                  />
                </div>
              </div>

              <div className="form-group">
                <label htmlFor="status">Status *</label>
                <select
                  id="status"
                  name="status"
                  value={formData.status}
                  onChange={handleChange}
                  disabled={isSubmitting}
                >
                  {Object.entries(PatientStatusDisplay).map(([key, label]) => (
                        <option key={key} value={key}>
                        {label}
                        </option>
                    ))}
                </select>
              </div>

              <div className="form-actions">
                <button
                  type="button"
                  className="cancel-btn"
                  onClick={() => setIsOpen(false)}
                  disabled={isSubmitting}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="submit-btn"
                  disabled={isSubmitting}
                >
                  {isSubmitting ? 'Adding Patient...' : 'Add Patient'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};