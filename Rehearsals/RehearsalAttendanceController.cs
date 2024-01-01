using System.Threading.Tasks;
using AttendanceService.Common;
using AttendanceService.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesForMember(int memberId) {
        return await this._dbContext.RehearsalAttendances
            .Where(a => a.Member.Id == memberId)
            .ToListAsync();
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesForRehearsal(int rehearsalId) {
        return await this._dbContext.RehearsalAttendances
            .Where(a => a.Rehearsal.Id == rehearsalId)
            .ToListAsync();
    }

    [HttpPost]
    [Route("[rehearsalId]")]
    public async Task<IEnumerable<RehearsalAttendance>> CreateAttendances(
            int rehearsalId, 
            List<CreateAttendanceModel> models) {
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
            CreateAttendanceModel model = models.Find(m => m.MemberId == member.Id);
            RehearsalAttendance attendance = new RehearsalAttendance() {
                Member = member,
                Rehearsal = rehearsal,
                IsPresent = model.IsPresent,
                ReasonForAbsence = model.ReasonForAbsence
            };
            this._dbContext.Add(attendance);
            attendances.Add(attendance);
        }

        await this._dbContext.SaveChangesAsync();
        return attendances;
    }

    [HttpPatch]
    [Route("[rehearsalId]")]
    public async Task<IEnumerable<RehearsalAttendance>> ModifyAttendances(
            int rehearsalId, 
            List<CreateAttendanceModel> models) {
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
            CreateAttendanceModel model = models.Find(m => m.MemberId == member.Id);
            RehearsalAttendance attendance = attendances.Find(a => a.Member == member);
            attendance.IsPresent = model.IsPresent;
            attendance.ReasonForAbsence = model.ReasonForAbsence;
        }

        await this._dbContext.SaveChangesAsync();
        return attendances;
    }
}
