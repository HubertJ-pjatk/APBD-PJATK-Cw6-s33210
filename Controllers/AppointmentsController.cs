using Cw6.DTOs;
using Microsoft.AspNetCore.Mvc;
using Cw6.Services;

namespace Cw6.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAppointments([FromQuery] string? status, [FromQuery] string? patientLastName)
    {
        var appointments = await service.GetAppointmentsAsync(status, patientLastName);
        return Ok(appointments);
    }
    
    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointment([FromRoute] int idAppointment)
    {
        try
        {
            var appointment = await service.GetAppointmentAsync(idAppointment);
            return Ok(appointment);
        }
        catch (Exceptions.NotFoundException e)
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> AddAppointment([FromBody] CreateAppointmentRequestDto request)
    {
        try
        {
            await service.AddAppointmentAsync(request);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (Exceptions.NotFoundException e)
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exceptions.ConflictException e)
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
    }
    
    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> UpdateAppointment(int idAppointment, [FromBody] UpdateAppointmentRequestDto request)
    {
        try
        {
            await service.UpdateAppointmentAsync(idAppointment, request);
            return Ok(); // Zadanie mówi o 200 OK
        }
        catch (Exceptions.NotFoundException e)
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exceptions.ConflictException e)
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> DeleteAppointment(int idAppointment)
    {
        try
        {
            await service.DeleteAppointmentAsync(idAppointment);
            return NoContent(); // 204 No Content po udanym usunięciu
        }
        catch (Exceptions.NotFoundException e)
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exceptions.ConflictException e)
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
    }
}