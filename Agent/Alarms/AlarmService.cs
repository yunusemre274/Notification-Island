using System.IO;
namespace NI.Agent.Alarms;

public sealed class AlarmService(string directory, IAlarmScheduler scheduler, Func<DateTimeOffset>? clock = null)
{
    private readonly string _directory = Path.GetFullPath(directory);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    public event EventHandler<AlarmDelivery>? Due;
    private string DatabasePath => Path.Combine(_directory, "alarms.json");
    private AlarmDatabase Read()
    {
        if (!File.Exists(DatabasePath)) return new(1, new());
        var db = DurableStore.Read<AlarmDatabase>(DatabasePath, 1_000_000);
        if (db.Version != 1 || db.Alarms.Count > 256) throw new InvalidDataException("Alarm deposu sürümü/boyutu desteklenmiyor; özgün kayıt korunur.");
        return db;
    }
    public IReadOnlyList<AlarmRecord> List()
    {
        using var lease = DurableStore.Lock(_directory);
        return Read().Alarms.OrderBy(x => x.NextDue).ToArray();
    }
    private OperationReceipts Receipts => new(Path.Combine(_directory, "receipts"));
    public ToolResult Create(string request, string title, string operationId, string zone) =>
        Receipts.Execute(operationId, "create", () => CreateCore(request, title, operationId, zone));
    private ToolResult CreateCore(string request, string title, string operationId, string zone)
    {
        using var lease = DurableStore.Lock(_directory);
        var db = Read();
        var existing = db.Alarms.FirstOrDefault(x => x.OperationId == operationId);
        if (existing != null)
        {
            if (existing.State == "Cancelled") return ToolResult.Fail("ALREADY_CANCELLED", "Bu işlemdeki alarm iptal edilmiş; yeni alarm oluşturulmadı.", operationId);
            return Register(db, existing, false);
        }
        if (db.Alarms.Count >= 256) return ToolResult.Fail("ALARM_LIMIT", "Alarm kayıt sınırına ulaşıldı. Eski kayıtları gözden geçirin.");
        if (title.Length > 120 || !Guid.TryParseExact(operationId, "N", out _)) return ToolResult.Fail("INVALID_ARGUMENTS", "Alarm adı/işlem kimliği geçersiz.");
        var spec = TurkishAlarmParser.Parse(request, _clock(), zone);
        var alarm = new AlarmRecord(Guid.NewGuid().ToString("N"), operationId, title, spec.Due, zone, spec.LocalTime, spec.Repeat, "PendingRegistration", _clock());
        db.Alarms.Add(alarm); Save(db); // Intent before scheduler side effect.
        return Register(db, alarm, true);
    }
    private ToolResult Register(AlarmDatabase db, AlarmRecord alarm, bool changed)
    {
        try
        {
            if (!scheduler.IsScheduled(alarm)) scheduler.Schedule(alarm);
            if (!scheduler.IsScheduled(alarm)) return ToolResult.Fail("SCHEDULE_NOT_VERIFIED", "Alarm kalıcı kaydı var; Windows zamanlama kaydı doğrulanamadı.", alarm.OperationId);
            Replace(db, alarm with { State = "Scheduled", UpdatedAt = _clock() }); Save(db);
            return new(true, "", $"Alarm zamanlaması doğrulandı: {TimeZoneInfo.ConvertTime(alarm.NextDue, TimeZoneInfo.FindSystemTimeZoneById(alarm.TimeZoneId)):dd.MM.yyyy HH:mm} ({alarm.Repeat}).", alarm.OperationId,
                new { alarmId = alarm.Id, alarm.NextDue, alarm.TimeZoneId, alarm.Repeat }, changed ? 1 : 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return ToolResult.Fail("SCHEDULER_UNAVAILABLE", "Alarm kayıtlı, fakat Windows zamanlaması başarısız: " + ex.GetType().Name + ". Yeniden açılışta uzlaştırılacak.", alarm.OperationId); }
    }
    public AlarmRecord Select(string request, string? suppliedId = null)
    {
        var alarms = List().Where(x => x.State is "Scheduled" or "PendingRegistration" or "Fired").ToArray();
        var text = request.ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        AlarmRecord? selected;
        if (text.Contains("yarınki"))
        {
            var matches = alarms.Where(x =>
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId);
                return TimeZoneInfo.ConvertTime(x.NextDue, zone).Date == TimeZoneInfo.ConvertTime(_clock(), zone).Date.AddDays(1);
            }).ToArray();
            selected = matches.Length == 1 ? matches[0] : null;
        }
        else if (text.Contains("az önce") || text.Contains("bu alarm") || text.Contains("son alarm")) selected = alarms.MaxBy(x => x.UpdatedAt);
        else if (suppliedId != null && request.Contains(suppliedId, StringComparison.Ordinal)) selected = alarms.SingleOrDefault(x => x.Id == suppliedId);
        else selected = null;
        if (selected == null || suppliedId != null && suppliedId != selected.Id)
            throw new AgentException("AMBIGUOUS_ALARM", "Hangi alarmı değiştirmemi istiyorsunuz? Alarm kimliğini veya ‘az önceki alarm’ gibi açık bir hedef belirtin.");
        return selected;
    }
    public ToolResult Update(string request, string? id, bool snooze, string operationId) =>
        Receipts.Execute(operationId, snooze ? "snooze" : "update", () => UpdateCore(request, id, snooze, operationId));
    private ToolResult UpdateCore(string request, string? id, bool snooze, string operationId)
    {
        var selected = Select(request, id);
        using var lease = DurableStore.Lock(_directory);
        var db = Read(); var alarm = db.Alarms.Single(x => x.Id == selected.Id);
        if (alarm.OperationId == operationId && alarm.State == "Scheduled") return Register(db, alarm, false);
        var spec = TurkishAlarmParser.Parse(request, _clock(), alarm.TimeZoneId, alarm, snooze);
        // Snooze preserves recurring wall-time. One delayed occurrence is scheduled now;
        // after delivery recurrence resumes from the original local rule.
        alarm = alarm with { NextDue = spec.Due, LocalTime = snooze ? alarm.LocalTime : spec.LocalTime,
            Repeat = snooze ? alarm.Repeat : spec.Repeat, OperationId = operationId, State = "PendingRegistration", UpdatedAt = _clock() };
        Replace(db, alarm); Save(db);
        return Register(db, alarm, true);
    }
    public ToolResult Cancel(string request, string? id, string operationId) =>
        Receipts.Execute(operationId, "cancel", () => CancelCore(request, id, operationId));
    private ToolResult CancelCore(string request, string? id, string operationId)
    {
        var selected = Select(request, id);
        using var lease = DurableStore.Lock(_directory);
        var db = Read(); var alarm = db.Alarms.Single(x => x.Id == selected.Id);
        alarm = alarm with { State = "Cancelled", UpdatedAt = _clock(), OperationId = operationId };
        Replace(db, alarm); Save(db); // Tombstone first, so stale helper cannot ring it.
        scheduler.Cancel(alarm.Id);
        if (scheduler.IsScheduled(alarm)) return ToolResult.Fail("CANCEL_NOT_VERIFIED", "İptal kaydı var, zamanlayıcı temizliği doğrulanamadı.", operationId);
        return new(true, "", "Alarm iptali doğrulandı.", operationId, new { alarmId = alarm.Id }, 1);
    }
    public AlarmDelivery? Fire(string id, long occurrenceUtcTicks)
    {
        AlarmDelivery? delivery = null;
        using (var lease = DurableStore.Lock(_directory))
        {
            var db = Read(); var alarm = db.Alarms.FirstOrDefault(x => x.Id == id);
            if (alarm == null || alarm.State == "Cancelled" || alarm.NextDue.UtcTicks != occurrenceUtcTicks || alarm.NextDue > _clock().AddSeconds(1)) return null;
            var key = occurrenceUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (alarm.LastOccurrence == key) return null;
            var missed = _clock() - alarm.NextDue;
            // Persist the claim before display. A crash in this gap is an explicitly uncertain delivery, not exactly-once.
            alarm = alarm with { LastOccurrence = key, State = "Fired", Delivery = "ClaimedDeliveryUnconfirmed", UpdatedAt = _clock() };
            delivery = missed <= TimeSpan.FromHours(24) ? new(id, alarm.Title, alarm.NextDue, missed > TimeSpan.FromMinutes(1)) : null;
            if (alarm.Repeat != AlarmRepeat.Once)
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(alarm.TimeZoneId);
                alarm = alarm with { NextDue = TurkishAlarmParser.NextOccurrence(alarm.LocalTime, alarm.Repeat, zone, _clock()), State = "PendingRegistration" };
            }
            else if (delivery == null) alarm = alarm with { Delivery = "MissedExpired" };
            Replace(db, alarm); Save(db);
            if (alarm.Repeat != AlarmRepeat.Once) _ = Register(db, alarm, false);
        }
        if (delivery != null) Due?.Invoke(this, delivery);
        return delivery;
    }
    public void ConfirmPresented(string id)
    {
        using var lease = DurableStore.Lock(_directory); var db = Read();
        var alarm = db.Alarms.FirstOrDefault(x => x.Id == id);
        if (alarm != null) { Replace(db, alarm with { Delivery = "PresentationRequested" }); Save(db); }
    }
    public IReadOnlyList<string> Reconcile()
    {
        var failures = new List<string>();
        foreach (var alarm in List())
        {
            try
            {
                if (alarm.State == "Cancelled")
                {
                    scheduler.Cancel(alarm.Id);
                    Receipts.Resolve(alarm.OperationId, new(true, "", "İptal zamanlayıcıyla uzlaştırıldı.", alarm.OperationId));
                    continue;
                }
                if (alarm.State == "Fired" && alarm.Repeat == AlarmRepeat.Once)
                { if (alarm.Delivery == "ClaimedDeliveryUnconfirmed") failures.Add("DELIVERY_UNCONFIRMED:" + alarm.Id); continue; }
                if (alarm.NextDue <= _clock()) { Fire(alarm.Id, alarm.NextDue.UtcTicks); continue; }
                using var lease = DurableStore.Lock(_directory);
                var result = Register(Read(), alarm, false);
                if (!result.Success) failures.Add(result.ErrorCode);
                else Receipts.Resolve(alarm.OperationId, result with { VerifiedChanges = 0 });
            }
            catch (Exception ex) { failures.Add(ex.GetType().Name); }
        }
        return failures;
    }
    private void Save(AlarmDatabase db) => DurableStore.Write(DatabasePath, db);
    private static void Replace(AlarmDatabase db, AlarmRecord alarm)
    {
        var index = db.Alarms.FindIndex(x => x.Id == alarm.Id);
        if (index < 0) throw new InvalidDataException("Alarm kaydı kayboldu.");
        db.Alarms[index] = alarm;
    }
}
