using System.Data;
using Cw6.DTOs;
using Microsoft.Data.SqlClient;

namespace Cw6.Services;

public class AppointmentService(IConfiguration configuration) : IAppointmentService
{
    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        var appointments = new List<AppointmentListDto>();
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        
        command.Connection = connection;
        command.CommandText = """
                              SELECT 
                                  a.IdAppointment, 
                                  a.AppointmentDate, 
                                  a.Status, 
                                  a.Reason, 
                                  p.FirstName + N' ' + p.LastName AS PatientFullName, 
                                  p.Email AS PatientEmail
                              FROM dbo.Appointments a
                              JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                              WHERE (@Status IS NULL OR a.Status = @Status)
                                AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                              ORDER BY a.AppointmentDate;
                              """;
        
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = (object?)status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80).Value = (object?)patientLastName ?? DBNull.Value;

        await connection.OpenAsync();
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            });
        }

        return appointments;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = """
                              SELECT 
                                  a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
                                  p.FirstName + N' ' + p.LastName AS PatientFullName, 
                                  p.Email AS PatientEmail, p.PhoneNumber AS PatientPhoneNumber,
                                  d.FirstName + N' ' + d.LastName AS DoctorFullName, d.LicenseNumber,
                                  s.Name AS SpecializationName
                              FROM dbo.Appointments a
                              JOIN dbo.Patients p ON a.IdPatient = p.IdPatient
                              JOIN dbo.Doctors d ON a.IdDoctor = d.IdDoctor
                              JOIN dbo.Specializations s ON d.IdSpecialization = s.IdSpecialization
                              WHERE a.IdAppointment = @IdAppointment;
                              """;

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new Exceptions.NotFoundException($"Appointment with ID {idAppointment} not found.");
        }

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(0),
            AppointmentDate = reader.GetDateTime(1),
            Status = reader.GetString(2),
            Reason = reader.GetString(3),
            InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = reader.GetDateTime(5),
            PatientFullName = reader.GetString(6),
            PatientEmail = reader.GetString(7),
            PatientPhoneNumber = reader.GetString(8),
            DoctorFullName = reader.GetString(9),
            LicenseNumber = reader.GetString(10),
            SpecializationName = reader.GetString(11)
        };
    }

    public async Task AddAppointmentAsync(CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
        {
            throw new Exceptions.ConflictException("Appointment date cannot be in the past.");
        }

        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection = connection;
        
        command.CommandText = "SELECT IsActive FROM dbo.Patients WHERE IdPatient = @IdPatient";
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;

        var patientActiveObj = await command.ExecuteScalarAsync();
        if (patientActiveObj is null)
            throw new Exceptions.NotFoundException($"Patient with ID {request.IdPatient} not found.");
        if (!(bool)patientActiveObj)
            throw new Exceptions.ConflictException("Patient is not active.");

        command.Parameters.Clear();
        command.CommandText = "SELECT IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor";
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;

        var doctorActiveObj = await command.ExecuteScalarAsync();
        if (doctorActiveObj is null)
            throw new Exceptions.NotFoundException($"Doctor with ID {request.IdDoctor} not found.");
        if (!(bool)doctorActiveObj)
            throw new Exceptions.ConflictException("Doctor is not active.");

        command.Parameters.Clear();
        command.CommandText =
            "SELECT 1 FROM dbo.Appointments WHERE IdDoctor = @IdDoctor AND AppointmentDate = @AppointmentDate";
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;

        var conflictObj = await command.ExecuteScalarAsync();
        if (conflictObj is not null)
            throw new Exceptions.ConflictException("Doctor already has an appointment exactly at this time.");

        command.Parameters.Clear();
        command.CommandText = """
                              INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason) 
                              VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);
                              """;

        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection = connection;
        command.CommandText = "SELECT Status, AppointmentDate FROM dbo.Appointments WHERE IdAppointment = @Id";
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new Exceptions.NotFoundException($"Appointment {idAppointment} not found.");

        var currentStatus = reader.GetString(0);
        var currentDate = reader.GetDateTime(1);
        await reader.CloseAsync();
        command.Parameters.Clear();
        
        if (currentStatus == "Completed" && currentDate != request.AppointmentDate)
            throw new Exceptions.ConflictException("Cannot change the date of a completed appointment.");
        
        command.CommandText = "SELECT IsActive FROM dbo.Patients WHERE IdPatient = @IdPatient";
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        var pActive = await command.ExecuteScalarAsync();
        if (pActive is null) throw new Exceptions.NotFoundException("Patient not found.");
        if (!(bool)pActive) throw new Exceptions.ConflictException("Patient is inactive.");
        command.Parameters.Clear();
        
        command.CommandText = "SELECT IsActive FROM dbo.Doctors WHERE IdDoctor = @IdDoctor";
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        var dActive = await command.ExecuteScalarAsync();
        if (dActive is null) throw new Exceptions.NotFoundException("Doctor not found.");
        if (!(bool)dActive) throw new Exceptions.ConflictException("Doctor is inactive.");
        command.Parameters.Clear();
        command.CommandText = """
                              SELECT 1 FROM dbo.Appointments 
                              WHERE IdDoctor = @IdDoctor AND AppointmentDate = @Date AND IdAppointment <> @Id
                              """;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@Date", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        if (await command.ExecuteScalarAsync() is not null)
            throw new Exceptions.ConflictException("Doctor has another appointment at this time.");
        command.Parameters.Clear();
        command.CommandText = """
                              UPDATE dbo.Appointments SET 
                                  IdPatient = @IdPatient, IdDoctor = @IdDoctor, AppointmentDate = @Date, 
                                  Status = @Status, Reason = @Reason, InternalNotes = @Notes
                              WHERE IdAppointment = @Id
                              """;
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = request.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = request.IdDoctor;
        command.Parameters.Add("@Date", SqlDbType.DateTime2).Value = request.AppointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = request.Status;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = request.Reason;
        command.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
            (object?)request.InternalNotes ?? DBNull.Value;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection = connection;
        command.CommandText = "SELECT Status FROM dbo.Appointments WHERE IdAppointment = @Id";
        command.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        var status = await command.ExecuteScalarAsync();
        if (status is null) throw new Exceptions.NotFoundException("Appointment not found.");
        if (status.ToString() == "Completed")
            throw new Exceptions.ConflictException("Cannot delete completed appointments.");
        
        command.CommandText = "DELETE FROM dbo.Appointments WHERE IdAppointment = @Id";
        await command.ExecuteNonQueryAsync();
    }
}