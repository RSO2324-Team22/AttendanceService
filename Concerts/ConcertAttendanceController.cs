using AttendanceService.Common;
using AttendanceService.Database;
using AttendanceService.Members;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace AttendanceService.Concerts;

[ApiController]
[Route("concerts")]
public class ConcertAttendanceController : ControllerBase {
    private readonly ILogger<ConcertAttendanceController> _logger;
    private readonly AttendanceDbContext _dbContext;

    public ConcertAttendanceController(
            ILogger<ConcertAttendanceController> logger,
            AttendanceDbContext dbContext) {
        this._logger = logger;
        this._dbContext = dbContext;
    }

    [HttpGet]
    [Route("member/{memberId}")]
    [SwaggerOperation("GetAttendanceForMember")]
    public async Task<IEnumerable<ConcertAttendance>> GetAttendancesForMember(int memberId) {
        this._logger.LogInformation("Getting attendances for member {id}", memberId);
        return await this._dbContext.ConcertAttendances
            .Where(ca => ca.Member.Id == memberId)
            .ToListAsync();
    }

    [HttpGet]
    [Route("concert/{concertId}")]
    [SwaggerOperation("GetAttendancesForConcert")]
    public async Task<ActionResult<IEnumerable<ConcertAttendance>>> GetAttendancesForConcert(int concertId) {
        this._logger.LogInformation("Getting attendances for concert {id}", concertId);
        Concert? concert = await this._dbContext.Concerts
            .Where(c => c.Id == concertId)
            .SingleOrDefaultAsync();
        
        if (concert is null) {
            return NotFound();
        }

        List<ConcertAttendance> attendances = await this._dbContext.ConcertAttendances
            .Where(ca => ca.Concert.Id == concertId)
            .ToListAsync();

        return Ok(attendances);
    }

    [HttpPost]
    [Route("concert/{concertId}")]
    [SwaggerOperation("CreateAttendances")]
    public async Task<ActionResult<IEnumerable<ConcertAttendance>>> CreateAttendances(
            int concertId, 
            List<CreateAttendanceModel> models) {
        this._logger.LogInformation("Adding attendance for concert {id}", concertId);
        try
        {
            Concert? concert = await this._dbContext.Concerts
                .Where(c => c.Id == concertId)
                .SingleOrDefaultAsync();
            
            if (concert is null) {
                return NotFound();
            }

            List<int> memberIds = models
                .Select(m => m.MemberId)
                .ToList();

            List<Member> members = await this._dbContext.Members
                .Where(m => memberIds.Contains(m.Id))
                .ToListAsync();

            List<ConcertAttendance> attendances = new List<ConcertAttendance>();
            foreach (Member member in members) {
                CreateAttendanceModel? model = models.Find(m => m.MemberId == member.Id);
                if (model is null) {
                    this._logger.LogInformation("Attendance for concert {concertId} and member {memberId} was not given",
                                                concertId, member.Id);
                    return BadRequest($"Attendance for concert {concertId} and member {member.Id} was not given");
                }
                else {
                    ConcertAttendance attendance = new ConcertAttendance() {
                        Member = member,
                        Concert = concert,
                        IsPresent = model.IsPresent,
                        ReasonForAbsence = model.ReasonForAbsence
                    };
                    this._dbContext.Add(attendance);
                    attendances.Add(attendance);
                }
            }

            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Created new concert attendance");
            return CreatedAtAction(nameof(GetAttendancesForConcert),
                                   new { concertId = concertId });
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "There was an error adding concert attendance for concert {id}", concertId);
            return BadRequest($"There was an error adding concert attendance for concert {concertId}");
        }
    }

    [HttpPatch]
    [Route("concert/{concertId}")]
    [SwaggerOperation("EditAttendances")]
    public async Task<ActionResult<IEnumerable<ConcertAttendance>>> EditAttendances(
            int concertId, 
            List<CreateAttendanceModel> models) {
        this._logger.LogInformation("Editing attendance for concert {id}", concertId);
        try
        {
            Concert? concert = await this._dbContext.Concerts
                .Where(c => c.Id == concertId)
                .SingleOrDefaultAsync();

            if (concert is null) {
                return NotFound();
            }

            List<int> memberIds = models
                .Select(m => m.MemberId)
                .ToList();

            List<Member> members = await this._dbContext.Members
                .Where(m => memberIds.Contains(m.Id))
                .ToListAsync();

            List<ConcertAttendance> attendances = await this._dbContext.ConcertAttendances
                .Where(ca => ca.Concert.Id == concertId)
                .ToListAsync();

            foreach (Member member in members) {
                CreateAttendanceModel? model = models.Find(m => m.MemberId == member.Id);
                if (model is null) {
                    this._logger.LogInformation("Attendance for concert {concertId} and member {memberId} was not given",
                                                concertId, member.Id);
                    return BadRequest($"Attendance for concert {concertId} and member {member.Id} was not given");
                }
                else {
                    ConcertAttendance? attendance = attendances.Find(a => a.Member == member);
                    if (attendance is null) {
                        this._logger.LogInformation("Attendance for concert {concertId} and member {memberId} was not found",
                                                    concertId, member.Id);
                        return BadRequest($"Attendance for concert {concertId} and meber {member.Id} was not found");
                    }
                    else {
                        attendance.IsPresent = model.IsPresent;
                        attendance.ReasonForAbsence = model.ReasonForAbsence;
                    }
                }
            }

            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Edited concert attendance for concert {id}", concertId);
            return Ok(attendances);
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "There was an error editing concert attendance for concert {id}", concertId);
            return BadRequest($"There was an error deleting concert attendance {concertId}");
        }
    }
}
