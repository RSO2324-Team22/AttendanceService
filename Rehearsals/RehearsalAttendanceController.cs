using AttendanceService.Common;
using AttendanceService.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace AttendanceService.Rehearsals;

[ApiController]
[Route("rehearsals")]
public class RehearsalAttendanceController : ControllerBase {
    private readonly ILogger<RehearsalAttendanceController> _logger;
    private readonly AttendanceDbContext _dbContext;

    public RehearsalAttendanceController(
            ILogger<RehearsalAttendanceController> logger,
            AttendanceDbContext dbContext) {
        this._logger = logger;
        this._dbContext = dbContext;
    }

    [HttpGet]
    [Route("member/{memberId}")]
    [SwaggerOperation("GetAttendancesForMember")]
    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesForMember(int memberId) {
        return await this._dbContext.RehearsalAttendances
            .Where(a => a.Member.Id == memberId)
            .ToListAsync();
    }

    [HttpGet]
    [Route("{rehearsalId}")]
    [SwaggerOperation("GetAttendancesForRehearsal")]
    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesForRehearsal(int rehearsalId) {
        return await this._dbContext.RehearsalAttendances
            .Where(a => a.Rehearsal.Id == rehearsalId)
            .ToListAsync();
    }

    [HttpPost]
    [Route("{rehearsalId}")]
    [SwaggerOperation("CreateAttendances")]
    public async Task<ActionResult<IEnumerable<RehearsalAttendance>>> CreateAttendances(
            int rehearsalId, 
            List<CreateAttendanceModel> models) {
        this._logger.LogInformation("Adding attendance for rehearsal {id}", rehearsalId);
        try
        {
            Rehearsal rehearsal = await this._dbContext.Rehearsals
                .Where(r => r.Id == rehearsalId)
                .SingleAsync();

            List<int> memberIds = models
                .Select(m => m.MemberId)
                .ToList();

            List<Member> members = await this._dbContext.Members
                .Where(m => memberIds.Contains(m.Id))
                .ToListAsync();

            List<RehearsalAttendance> attendances = new List<RehearsalAttendance>();
            foreach (Member member in members) {
                CreateAttendanceModel? model = models.Find(m => m.MemberId == member.Id);
                if (model is null) {
                    this._logger.LogInformation("Attendance for rehearsal {rehearsalId} and member {memberId} was not given",
                                                rehearsalId, member.Id);
                    return BadRequest($"Attendance for rehearsal {rehearsalId} and member {member.Id} was not given");
                }
                else {
                    RehearsalAttendance attendance = new RehearsalAttendance() {
                        Member = member,
                        Rehearsal = rehearsal,
                        IsPresent = model.IsPresent,
                        ReasonForAbsence = model.ReasonForAbsence
                    };
                    this._dbContext.Add(attendance);
                    attendances.Add(attendance);
                }
            }

            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Created new rehearsal attendance");
            return Created(nameof(Index), attendances);
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "There was an error adding attendance for rehearsal {id}", rehearsalId);
            return BadRequest("There was an error adding attendance for rehearsal {rehearsalId}");
        }
    }

    [HttpPatch]
    [Route("{rehearsalId}")]
    [SwaggerOperation("EditAttendances")]
    public async Task<ActionResult<IEnumerable<RehearsalAttendance>>> EditAttendances(
            int rehearsalId, 
            List<CreateAttendanceModel> models) {
        this._logger.LogInformation("Editing attendance for rehearsal {id}", rehearsalId);
        try
        {
            Rehearsal rehearsal = await this._dbContext.Rehearsals
                .Where(r => r.Id == rehearsalId)
                .SingleAsync();

            List<int> memberIds = models
                .Select(m => m.MemberId)
                .ToList();

            List<Member> members = await this._dbContext.Members
                .Where(m => memberIds.Contains(m.Id))
                .ToListAsync();

            List<RehearsalAttendance> attendances = await this._dbContext.RehearsalAttendances
                .Where(ca => ca.Rehearsal.Id == rehearsalId)
                .ToListAsync();

            foreach (Member member in members) {
                CreateAttendanceModel? model = models.Find(m => m.MemberId == member.Id);
                if (model is null) {
                    this._logger.LogInformation("Attendance for rehearsal {rehearsalId} and member {memberId} was not given",
                                                rehearsalId, member.Id);
                    return BadRequest($"Attendance for rehearsal {rehearsalId} and member {member.Id} was not given");
                }
                else {
                    RehearsalAttendance? attendance = attendances.Find(a => a.Member == member);
                    if (attendance is null) {
                        this._logger.LogInformation("Attendance for rehearsal {rehearsalId} and member {memberId} was not found",
                                                    rehearsalId, member.Id);
                        return BadRequest($"Attendance for rehearsal {rehearsalId} and member {member.Id} was not found");
                    }
                    else {
                        attendance.IsPresent = model.IsPresent;
                        attendance.ReasonForAbsence = model.ReasonForAbsence;
                    }
                }
            }

            await this._dbContext.SaveChangesAsync();
            this._logger.LogInformation("Edited attendance for rehearsal {id}", rehearsalId);
            return Ok(attendances);
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "There was an error editing attendance for rehearsal {id}", rehearsalId);
            return BadRequest($"There was an error deleting attendance for rehearsal {rehearsalId}");
        }
    }
}
