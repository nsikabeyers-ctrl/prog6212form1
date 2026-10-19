using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>
/// Fills an empty database with the same sample data as the Part 1 SQL script,
/// but with real password hashes so these accounts can actually log in.
/// Every seeded account uses the password in <see cref="DemoPassword"/>.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static void Seed(RaceDayDbContext db, IPasswordHasher hasher)
    {
        if (db.Organisers.Any() || db.Participants.Any())
        {
            return;
        }

        var hash = hasher.Hash(DemoPassword);

        var thabo = new Organiser { FullName = "Thabo Nkosi", Email = "thabo.nkosi@raceday.co.za", PasswordHash = hash, PhoneNumber = "0821234567" };
        var lindiwe = new Organiser { FullName = "Lindiwe Dube", Email = "lindiwe.dube@raceday.co.za", PasswordHash = hash, PhoneNumber = "0837654321" };
        db.Organisers.AddRange(thabo, lindiwe);

        var sipho = new Participant { FullName = "Sipho Mokoena", Email = "sipho.mokoena@example.com", PasswordHash = hash, DateOfBirth = new DateOnly(1998, 4, 12) };
        var amahle = new Participant { FullName = "Amahle Zulu", Email = "amahle.zulu@example.com", PasswordHash = hash, DateOfBirth = new DateOnly(2001, 9, 30) };
        db.Participants.AddRange(sipho, amahle);

        var parkRun = new Event
        {
            Organiser = thabo,
            Name = "Pretoria Park Run Classic",
            Description = "A community fun run through the Pretoria botanical gardens.",
            EventDate = new DateTime(2026, 11, 14, 7, 0, 0),
            Location = "Pretoria",
            DistanceKm = 10.0m,
            EventType = EventType.Run
        };
        var charityWalk = new Event
        {
            Organiser = thabo,
            Name = "Tembisa Charity Walk",
            Description = "A charity walk supporting local youth programmes.",
            EventDate = new DateTime(2026, 11, 21, 8, 0, 0),
            Location = "Tembisa",
            DistanceKm = 5.0m,
            EventType = EventType.Walk
        };
        var cycle = new Event
        {
            Organiser = lindiwe,
            Name = "Highveld Cycle Challenge",
            Description = "A scenic cycling route across the Highveld plains.",
            EventDate = new DateTime(2026, 12, 5, 6, 30, 0),
            Location = "Johannesburg",
            DistanceKm = 42.0m,
            EventType = EventType.Cycle
        };
        db.Events.AddRange(parkRun, charityWalk, cycle);

        var under20 = new Category { Event = parkRun, Name = "Under 20", MinAge = 10, MaxAge = 19, DistanceKm = 10.0m };
        var senior = new Category { Event = parkRun, Name = "Senior", MinAge = 20, MaxAge = 59, DistanceKm = 10.0m };
        var openWalk = new Category { Event = charityWalk, Name = "Open Walk", MinAge = 0, MaxAge = 99, DistanceKm = 5.0m };
        var half = new Category { Event = cycle, Name = "21km Half", MinAge = 16, MaxAge = 99, DistanceKm = 21.0m };
        var full = new Category { Event = cycle, Name = "42km Full", MinAge = 18, MaxAge = 99, DistanceKm = 42.0m };
        db.Categories.AddRange(under20, senior, openWalk, half, full);

        var siphoRun = new Enrolment { Participant = sipho, Event = parkRun, Category = senior, Status = EnrolmentStatus.Confirmed };
        var amahleRun = new Enrolment { Participant = amahle, Event = parkRun, Category = under20, Status = EnrolmentStatus.Confirmed };
        var siphoCycle = new Enrolment { Participant = sipho, Event = cycle, Category = half, Status = EnrolmentStatus.Pending };
        db.Enrolments.AddRange(siphoRun, amahleRun, siphoCycle);

        db.Results.AddRange(
            new Result { Enrolment = siphoRun, FinishTime = new TimeSpan(0, 48, 32), FinishPosition = 12, RecordedByOrganiser = thabo },
            new Result { Enrolment = amahleRun, FinishTime = new TimeSpan(0, 52, 10), FinishPosition = 15, RecordedByOrganiser = thabo });

        db.SaveChanges();
    }
}
