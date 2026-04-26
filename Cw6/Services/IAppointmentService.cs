using Cw6.DTOs;

namespace Cw6.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);
    
    Task<AppointmentDetailsDto> GetAppointmentAsync(int idAppointment);
    
    Task AddAppointmentAsync(CreateAppointmentRequestDto request);
    
    Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request);
    
    Task DeleteAppointmentAsync(int idAppointment);
}