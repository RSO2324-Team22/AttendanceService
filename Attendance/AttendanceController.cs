using Microsoft.AspNetCore.Mvc;

namespace AttendanceService.Attendance;

[ApiController]
[Route("[controller]")]
public class AttendanceController : ControllerBase {
    private ILogger<AttendanceController> _logger;
    
    public AttendanceController(ILogger<AttendanceController> logger) {
        this._logger = logger;
    }    
}

