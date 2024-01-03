using AttendanceService.Common;
using AttendanceService.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    [Route("member/[id]")]
    public async Task<IEnumerable<ConcertAttendance>> GetAttendancesForMember(int memberId) {
        return await this._dbContext.ConcertAttendances
            .Where(ca => ca.Member.Id == memberId)
            .ToListAsync();
    }

    [HttpGet]
    [Route("[id]")]
    public async Task<IEnumerable<ConcertAttendance>> GetAttendancesForConcert(int concertId) {
        return await this._dbContext.ConcertAttendances
            .Where(ca => ca.Concert.Id == concertId)
            .ToListAsync();
    }

    [HttpPost]
    [Route("[concertId]")]
    public async Task<IEnumerable<ConcertAttendance>> CreateAttendances(
            int concertId, 
            List<CreateAttendanceModel> models) {
        Concert concert = await this._dbContext.Concerts
            .Where(c => c.Id == concertId)
            .SingleAsync();

        List<int> memberIds = models
            .Select(m => m.MemberId)
            .ToList();

        List<Member> members = await this._dbContext.Members
            .Where(m => memberIds.Contains(m.Id))
            .ToListAsync();

        List<ConcertAttendance> attendances = new List<ConcertAttendance>();
        foreach (Member member in members) {
            CreateAttendanceModel model = models.Find(m => m.MemberId == member.Id);
            ConcertAttendance attendance = new ConcertAttendance() {
                Member = member,
                Concert = concert,
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
    [Route("[concertId]")]
    public async Task<IEnumerable<ConcertAttendance>> ModifyAttendances(
            int concertId, 
            List<CreateConcertAttendanceModel> models) {
        Concert concert = await this._dbContext.Concerts
            .Where(c => c.Id == concertId)
            .SingleAsync();

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
            CreateConcertAttendanceModel model = models.Find(m => m.MemberId == member.Id);
            ConcertAttendance attendance = attendances.Find(a => a.Member == member);
            attendance.IsPresent = model.IsPresent;
            attendance.ReasonForAbsence = model.ReasonForAbsence;
        }

        await this._dbContext.SaveChangesAsync();
        return attendances;
    }
}
