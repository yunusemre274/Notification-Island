using System.IO;
using System.Text.Json;
using NI.Agent.Alarms;
using NI.Agent.Files;

namespace NI.Agent.Tools;

public static class DesktopTools
{
    public static ToolRegistry Build(FileOrganizer files, AlarmService alarms, AgentOptions options,
        Func<string, JsonElement, ToolContext, CancellationToken, Task<ToolResult>>? system = null)
    {
        var tools = new ToolRegistry();
        var scope = ToolRegistry.String("İzinli kullanıcı klasörü", new[] { "desktop", "downloads" });
        void Add(string name, string description, string area, ToolRisk risk, string[] required, object properties,
            Func<JsonElement, ToolContext, CancellationToken, ToolResult> action, Func<JsonElement, string>? approval = null)
            => tools.Add(ToolRegistry.Create(name, description, area, risk, ToolRegistry.Schema(required, properties),
                (args, context, token) => Task.Run(() =>
                {
                    try { return action(args, context, token); }
                    catch (AgentException ex) { return ToolResult.Fail(ex.Code, ex.Message); }
                    catch (UnauthorizedAccessException) { return ToolResult.Fail("ACCESS_DENIED", "İşlem için dosya erişimi yok."); }
                    catch (IOException) { return ToolResult.Fail("IO_ERROR", "Dosya/depo kilitli veya erişilemiyor; belirsiz işlemi körlemesine yinelemeyin."); }
                    catch (ArgumentException) { return ToolResult.Fail("INVALID_ARGUMENTS", "Araç parametresi geçersiz."); }
                }, token), approval));
        Add("files_list", "Üst düzey dosyaları sayfalı listele.", "files", ToolRisk.ReadOnly, ["scope"],
            new { scope, offset = new { type = "integer" }, limit = new { type = "integer" } },
            (a, _, t) => files.List(S(a, "scope"), I(a, "offset", 0), I(a, "limit", 10), t));
        Add("files_read_text", "Seçili metin dosyasından sınırlı, güvenilmeyen veri oku.", "files", ToolRisk.ReadOnly, ["scope", "fileId"],
            new { scope, fileId = ToolRegistry.String("Listelemeden gelen kısa dosya kimliği") },
            (a, _, t) => files.ReadText(S(a, "scope"), S(a, "fileId"), t));
        Add("files_prepare_moves", "Dosya türüyle toplu, geri alınabilir taşıma planı hazırla.", "files", ToolRisk.Prepare, ["scope", "policy"],
            new { scope, policy = ToolRegistry.String("Sınıflandırma", ["type", "pdf-only", "documents-only", "images-only", "archives-only"]) },
            (a, c, t) => RequestedPolicy(c.UserRequest) is { } requested && requested == S(a, "policy")
                ? files.Prepare(S(a, "scope"), requested, Operation(c, "prepare:" + a.GetRawText()), t)
                : ToolResult.Fail("POLICY_MISMATCH", "İstenen dosya filtresiyle plan aynı değil. Tek kategori, PDF veya genel tür düzenlemesi açıkça seçilmeli."));
        Add("files_apply_moves", "Hazırlanmış planı doğrulayarak uygula. Üzerine yazma yok.", "files", ToolRisk.Reversible, ["scope", "operationId"],
            new { scope, operationId = ToolRegistry.String("Hazırlama sonucundaki işlem kimliği") },
            (a, c, t) => RequestedPolicy(c.UserRequest) == files.PlanPolicy(S(a, "operationId"))
                ? files.Apply(S(a, "scope"), S(a, "operationId"), t, c.Progress)
                : ToolResult.Fail("POLICY_MISMATCH", "Plan kullanıcının istediği dosya filtresine uymuyor; uygulanmadı."), a => files.ApprovalSummary(S(a, "operationId")));
        Add("files_undo_operation", "Son doğrulanmış dosya düzenlemesini güvenle geri al.", "files", ToolRisk.Reversible, [], new { },
            (_, c, t) => files.LatestUndoable() is { } latest ? files.Undo(latest.Id, t) : ToolResult.Fail("NO_UNDO", "Geri alınabilir işlem bulunamadı."),
            _ => files.LatestUndoable() is { } op ? $"{op.Root} kapsamındaki {op.Id} işlemi geri alınacak; değişen dosyalar ezilmeyecek." : "Geri alınabilir işlem yok.");
        Add("alarms_create", "Kullanıcının özgün isteğinden zaman çöz ve kalıcı alarm kur.", "alarms", ToolRisk.Reversible, [], new { title = ToolRegistry.String("Kısa alarm başlığı") },
            (a, c, _) => alarms.Create(c.UserRequest, a.TryGetProperty("title", out var title) ? title.GetString()! : "Notification Island alarmı", Operation(c, "alarm-create"), options.TimeZoneId));
        Add("alarms_list", "Kalıcı alarmların ilk 12 kaydını getir.", "alarms", ToolRisk.ReadOnly, [], new { },
            (_, _, _) => new(true, "", "Alarm kayıtları listelendi.", "", new { alarms = alarms.List().Take(12).Select(x => new { x.Id, x.Title, x.NextDue, x.Repeat, x.State }) }));
        foreach (var action in new[] { "update", "cancel", "snooze" })
        {
            var verb = action;
            Add("alarms_" + action, $"Kullanıcının açıkça seçtiği alarmı {action}.", "alarms", ToolRisk.Reversible, [], new { alarmId = ToolRegistry.String("İsteğe bağlı, kullanıcı isteğiyle eşleşen kimlik") },
                (a, c, _) =>
                {
                    var id = a.TryGetProperty("alarmId", out var supplied) ? supplied.GetString() : null;
                    return verb == "cancel" ? alarms.Cancel(c.UserRequest, id, Operation(c, "cancel"))
                        : alarms.Update(c.UserRequest, id, verb == "snooze", Operation(c, verb));
                });
        }
        if (system != null)
        {
            foreach (var name in new[] { "system_metrics", "media_pause", "audio_set_volume" })
            {
                var toolName = name;
                var parameters = name == "audio_set_volume" ? ToolRegistry.Schema(["percent"], new { percent = new { type = "integer" } }) : ToolRegistry.Schema([], new { });
                tools.Add(ToolRegistry.Create(name, name switch { "system_metrics" => "Gerçek sistem RAM/CPU ölçümünü getir.", "media_pause" => "Güncel medya oynatmasını duraklat ve doğrula.", _ => "Ana sesi yüzdeye ayarla ve gerçek değeri doğrula." },
                    name switch { "system_metrics" => "system", "media_pause" => "media", _ => "volume" }, name == "system_metrics" ? ToolRisk.ReadOnly : ToolRisk.SystemChange, parameters,
                    async (a, c, t) => await system(toolName, a, c, t)));
            }
        }
        return tools;
    }
    private static string? RequestedPolicy(string request)
    {
        var text = request.ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[\p{L}\d_-]+\.(?:pdf|png|jpe?g|docx?|xlsx?|zip|rar|7z)\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        if (text.Contains("pdf")) return "pdf-only";
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(?:png|jpg|jpeg|docx|xlsx|zip|rar|7z)\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return null;
        var document = text.Contains("belge"); var image = text.Contains("görsel") || text.Contains("resim"); var archive = text.Contains("arşiv");
        var count = (document ? 1 : 0) + (image ? 1 : 0) + (archive ? 1 : 0);
        return count is 0 or 3 ? "type" : count == 2 ? null : document ? "documents-only" : image ? "images-only" : "archives-only";
    }
    public static string Operation(ToolContext context, string key) => DurableStore.Fingerprint(context.TaskId.ToString("N") + ":" + key)[..32].ToLowerInvariant();
    private static string S(JsonElement args, string name) => args.GetProperty(name).GetString()!;
    private static int I(JsonElement args, string name, int fallback) => args.TryGetProperty(name, out var value) ? value.GetInt32() : fallback;
}
