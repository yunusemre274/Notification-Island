using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NI.Agent.Alarms;

public static class TurkishAlarmParser
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    public static AlarmSpec Parse(string request, DateTimeOffset now, string zoneId, AlarmRecord? existing = null, bool snooze = false)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var text = request.ToLower(Turkish);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        if (text.Contains("buçuk") || text.Contains("çeyrek") || text.Contains("haftaya") || text.Contains("gelecek"))
            throw new AgentException("AMBIGUOUS_TIME", "Tarihi ve saati açık biçimde belirtin; örneğin yarın 08.30.");
        var recurrence = text.Contains("hafta içi") || text.Contains("haftaiçi") ? AlarmRepeat.Weekdays
            : text.Contains("her gün") || text.Contains("her sabah") ? AlarmRepeat.Daily : existing?.Repeat ?? AlarmRepeat.Once;
        var relative = RelativeMinutes(text);
        if (relative.HasValue)
        {
            var basis = !snooze && existing != null && (text.Contains("ileri") || text.Contains("ertele")) ? existing.NextDue : now;
            var due = basis.ToUniversalTime().AddMinutes(relative.Value);
            if (due <= now) throw new AgentException("PAST_TIME", "Yeni alarm saati geçmişte; başka zaman belirtin.");
            var local = TimeZoneInfo.ConvertTime(due, zone);
            return new(local, TimeOnly.FromDateTime(local.DateTime), snooze ? AlarmRepeat.Once : recurrence, zoneId);
        }
        if (snooze) throw new AgentException("MISSING_TIME", "Ne kadar ertelememi istiyorsunuz?");
        int? hour = null; var minute = 0;
        var numeric = Regex.Match(text, @"(?<!\d)([0-2]?\d)[.:]([0-5]\d)(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (numeric.Success) { hour = int.Parse(numeric.Groups[1].Value); minute = int.Parse(numeric.Groups[2].Value); }
        else
        {
            var words = new Dictionary<string, int> { ["bir"] = 1, ["iki"] = 2, ["üç"] = 3, ["dört"] = 4, ["beş"] = 5, ["altı"] = 6, ["yedi"] = 7, ["sekiz"] = 8, ["dokuz"] = 9, ["on"] = 10, ["on bir"] = 11, ["on iki"] = 12 };
            var clock = Regex.Match(text, @"(?:sabah|akşam|öğlen|saat)\s+(on iki|on bir|sekiz|dokuz|yedi|altı|beş|dört|üç|iki|bir|on|\d{1,2})(?:[’']?[dt][ae]|[’']?[ae])?(?![a-zçğıöşü\d])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (clock.Success) hour = words.TryGetValue(clock.Groups[1].Value, out var word) ? word : int.Parse(clock.Groups[1].Value);
        }
        if (hour is > 23) throw new AgentException("AMBIGUOUS_TIME", "Geçerli bir saat belirtin.");
        if (hour == null && existing != null && recurrence != AlarmRepeat.Once) { hour = existing.LocalTime.Hour; minute = existing.LocalTime.Minute; }
        if (hour == null) throw new AgentException("MISSING_TIME", "Alarmı hangi tarih ve saate kurmamı istiyorsunuz?");
        if (text.Contains("akşam") && hour < 12) hour += 12;
        if (text.Contains("öğlen") && hour < 12) hour += 12;
        var time = new TimeOnly(hour.Value, minute);
        if (recurrence != AlarmRepeat.Once && !text.Contains("yarın"))
        {
            var next = NextOccurrence(time, recurrence, zone, now);
            return new(next, time, recurrence, zoneId);
        }
        if (!text.Contains("yarın") && !text.Contains("bugün") && !text.Contains("bu akşam") && !text.Contains("bu sabah")
            && !Regex.IsMatch(text, @"\d{4}-\d{2}-\d{2}|\d{2}\.\d{2}\.\d{4}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            throw new AgentException("MISSING_TIME", "Bu saati hangi gün için kurmamı istiyorsunuz?");
        var date = DateOnly.FromDateTime(localNow.DateTime);
        if (text.Contains("yarın")) date = date.AddDays(1);
        var explicitDate = Regex.Match(text, @"(?<!\d)(\d{4}-\d{2}-\d{2}|\d{2}\.\d{2}\.\d{4})(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (explicitDate.Success)
        {
            if (!DateOnly.TryParseExact(explicitDate.Value, new[] { "yyyy-MM-dd", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                throw new AgentException("AMBIGUOUS_TIME", "Geçerli bir tarih belirtin.");
        }
        var instant = ToInstant(date.ToDateTime(time), zone);
        if (instant <= now) throw new AgentException("PAST_TIME", "Bu saat geçmişte. Hangi tarih için kurmamı istiyorsunuz?");
        return new(instant, time, recurrence, zoneId);
    }
    public static int? RelativeMinutes(string text)
    {
        if (text.Contains("yarım saat")) return 30;
        var match = Regex.Match(text, @"(?<!\d)(\d{1,4})\s*(dakika|saat)(?:lık|lik|lüğ|liğ|lık|lik)?", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) return null;
        var value = int.Parse(match.Groups[1].Value) * (match.Groups[2].Value == "saat" ? 60 : 1);
        if (value < 1 || value > 10080) throw new AgentException("TIME_RANGE", "Göreli süre 1 dakika ile 7 gün arasında olmalı.");
        return value;
    }
    public static DateTimeOffset NextOccurrence(TimeOnly time, AlarmRepeat repeat, TimeZoneInfo zone, DateTimeOffset after)
    {
        var local = TimeZoneInfo.ConvertTime(after, zone);
        var start = DateOnly.FromDateTime(local.DateTime);
        for (var day = 0; day <= 14; day++)
        {
            var date = start.AddDays(day);
            if (repeat == AlarmRepeat.Weekdays && date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var wall = date.ToDateTime(time);
            // Nonexistent/ambiguous DST occurrences are skipped, never guessed.
            if (zone.IsInvalidTime(wall) || zone.IsAmbiguousTime(wall)) continue;
            var instant = ToInstant(wall, zone);
            if (instant > after) return instant;
        }
        throw new AgentException("TIMEZONE_RULE", "Güvenli sonraki alarm zamanı bulunamadı.");
    }
    private static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new AgentException("AMBIGUOUS_TIME", "Bu saat yaz saati geçişinde belirsiz veya mevcut değil; başka saat belirtin.");
        return new(local, zone.GetUtcOffset(local));
    }
}
