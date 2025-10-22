using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using HealthFlow.Services.Patients.Models;
using HealthFlow.Services.Patients.Data;
using HealthFlow.Shared.Messaging;
using HealthFlow.Services.Patients.Hubs;
using Microsoft.EntityFrameworkCore; 
using HealthFlow.Shared.Models;

namespace HealthFlow.Services.Patients.Controllers;
[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly PatientDbContext _context;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IHubContext<PatientHub> _hubContext;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        PatientDbContext context, 
        ILogger<PatientsController> logger,
        IMessagePublisher messagePublisher,
        IHubContext<PatientHub> hubContext)
    {
        _context = context;
        _messagePublisher = messagePublisher;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Patient>>> GetPatients(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Patients.AsQueryable();

            // Filter by status 
            if(!string.IsNullOrEmpty(status) && Enum.TryParse<PatientStatus>(status, true, out var statusFilter))
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            // Search by name or medical record number
            if(!string.IsNullOrEmpty(search)) 
            {
                query = query.Where(p => 
                    p.FirstName.Contains(search) || 
                    p.LastName.Contains(search) || 
                    p.MedicalRecordNumber.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var patients = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1)*pageSize)
                    .Take(pageSize)
                    .ToListAsync();

            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(patients);           
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patients");
            return StatusCode(500, "Error retrieving patients");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Patient>> GetPatient(Guid id)
    {
        try
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();
            return patient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {PatientId}", id);
            return StatusCode(500, "Error retrieving patient");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Patient>> CreatePatient(Patient patient, CancellationToken cancellationToken)
    {   try
        {
            // Validate MedicalRecordNumber uniqueness
            if (await _context.Patients.AnyAsync(p => p.MedicalRecordNumber == patient.MedicalRecordNumber))
            {
                return BadRequest("Medical Record Number must be unique");
            }

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
            
            var payload = new PatientCreatedPayload(
                patient.Id.ToString(),
                patient.FirstName,
                patient.LastName,
                patient.MedicalRecordNumber,
                patient.Status.ToString(),
                patient.DateOfBirth,
                patient.Age, 
                patient.CreatedAt);

            var message = PatientMessage.CreatePatientCreated(
                patient.Id.ToString(), 
                payload, 
                Guid.NewGuid().ToString());

            await _messagePublisher.PublishAsync("patient.events", "patient.created", message, cancellationToken);
            await _hubContext.Clients.All.SendAsync("PatientCreated", patient);
            
            _logger.LogInformation("Patient created: {PatientId} - {FullName}", patient.Id, patient.FullName);
            return CreatedAtAction(nameof(GetPatient), new { id = patient.Id }, patient);
        }   
        catch (Exception ex)
        {
            
            _logger.LogError(ex, "Error creating patient");
            return StatusCode(500, "Error creating patient");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePatient(Guid id, Patient patient, CancellationToken cancellationToken) 
    {
        try
        {
            if(id != patient.Id) 
            {
                return BadRequest("ID mismatch");
            }

            var existingPatient = await _context.Patients.FindAsync(id);
            if(existingPatient == null)
            {
                return NotFound();
            }

            // Check Medical record uniqueness 
            if(existingPatient.MedicalRecordNumber != patient.MedicalRecordNumber && 
                await _context.Patients.AnyAsync(p => p.MedicalRecordNumber == patient.MedicalRecordNumber))
            {
                return BadRequest("Medical Record Number must be unique");
            }

            // Update properties
            existingPatient.FirstName = patient.FirstName;
            existingPatient.LastName = patient.LastName;
            existingPatient.DateOfBirth = patient.DateOfBirth;
            existingPatient.MedicalRecordNumber = patient.MedicalRecordNumber;
            existingPatient.Status = patient.Status;

            await _context.SaveChangesAsync();

            // Publish update event
            var message = new PatientMessage(
                Guid.NewGuid(),
                patient.Id.ToString(),
                "PatientUpdated",
                DateTime.UtcNow,
                new {
                    patient.Id,
                    patient.FirstName,
                    patient.LastName,
                    patient.MedicalRecordNumber,
                    patient.Status
                });

            await _messagePublisher.PublishAsync("patient.events", message, cancellationToken);
            await _hubContext.Clients.All.SendAsync("PatientUpdated", patient);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating patient {PatientId}", id);
            return StatusCode(500, "Error updating patient");
            
        }
    } 

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdatePatientStatus(int id, PatientStatus status, CancellationToken cancellationToken) 
    {
        try
        {
            var patient = await _context.Patients.FindAsync(id);
            if(patient == null) 
            {
                return NotFound();
            }

            var oldStatus = patient.Status;
            patient.Status = status;
            await _context.SaveChangesAsync();

            // Create payload for status change event
            var payload = new PatientStatusChangedPayload(
                patient.Id.ToString(),
                oldStatus.ToString(),
                status.ToString(),
                patient.FullName,
                DateTime.UtcNow);

            // Publish status change event using PatientMessage
            var message = PatientMessage.CreatePatientStatusChanged(
                patient.Id.ToString(),
                oldStatus.ToString(),
                status.ToString(),
                Guid.NewGuid().ToString());

            await _messagePublisher.PublishAsync("patient.events", message, cancellationToken);
            await _hubContext.Clients.All.SendAsync("PatientStatusUpdated", new {patient.Id, status});

            _logger.LogInformation("Patient status updated: {PatientId} from {OldStatus} to {NewStatus}", patient.Id, oldStatus, status);
            
            return Ok(patient);
        }
        catch (Exception ex)
        {  
            _logger.LogError(ex, "Error updating patient status for {PatientId}", id);
            return StatusCode(500, "Error updating patient status");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(Guid id, CancellationToken cancellationToken) 
    {
        try
        {
            var patient = await _context.Patients.FindAsync(id);
            if(patient == null) return NotFound();

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();
            
            // Create payload for delete event
            var payload = new PatientDeletedPayload(
                patient.Id.ToString(),
                patient.FullName,
                patient.MedicalRecordNumber,
                DateTime.UtcNow);

            // Publish delete event using PatientMessage
            var message = PatientMessage.CreatePatientDeleted(
                patient.Id.ToString(),
                payload,
                Guid.NewGuid().ToString()) ;

            await _messagePublisher.PublishAsync("patient.events", message, cancellationToken);
            await _hubContext.Clients.All.SendAsync("PatientDeleted", new {patient.Id});

            _logger.LogInformation("Patient deleted: {PatientId} - {FullName}", patient.Id, patient.FullName);
            return NoContent();
        }
        catch (Exception ex)
        {          
            _logger.LogError(ex, "Error deleting patient {patientId}", id);
            return StatusCode(500, "Error deleting patient");
        }
    }
}