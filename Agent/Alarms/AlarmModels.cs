using System.IO;
namespace NI.Agent.Alarms;

public enum AlarmRepeat { Once, Daily, Weekdays }
public sealed record AlarmRecord(string Id, string OperationId, string Title, DateTimeOffset NextDue,
    string TimeZoneId, TimeOnly LocalTime, AlarmRepeat Repeat, string State, DateTimeOffset UpdatedAt,
    string? LastOccurrence = null, string? Delivery = null);
public sealed record AlarmDatabase(int Version, List<AlarmRecord> Alarms);
public sealed record AlarmSpec(DateTimeOffset Due, TimeOnly LocalTime, AlarmRepeat Repeat, string TimeZoneId);
public interface IAlarmScheduler
{
    void Schedule(AlarmRecord alarm);
    bool IsScheduled(AlarmRecord alarm);
    void Cancel(string id);
}
public sealed record AlarmDelivery(string Id, string Title, DateTimeOffset Due, bool Missed);
