using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace NI.Agent;

public sealed record PermissionGrant(HashSet<string> ReadScopes, HashSet<string> WriteScopes,
    bool Alarms, bool Media, bool Volume, bool UndoRequested, bool AlarmRead = false, HashSet<string>? AlarmActions = null)
{
    public static PermissionGrant FromUser(string request)
    {
        var text = request.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        bool HasVerb(string phrase, string alternatives) => Regex.IsMatch(phrase, @"(?<![\p{L}])(?:" + alternatives + @")(?![\p{L}])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        bool Verb(string alternatives) => HasVerb(text, alternatives);
        var reads = new HashSet<string>(StringComparer.Ordinal); var writes = new HashSet<string>(StringComparer.Ordinal);
        var denied = new HashSet<string>(StringComparer.Ordinal); var alarmActions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clause in Regex.Split(text, @"[;!?\n]|\.(?=\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            var scopes = new List<string>();
            if (clause.Contains("masaüst")) scopes.Add("desktop");
            if (clause.Contains("indirilen")) scopes.Add("downloads");
            foreach (var scope in scopes)
            {
                if (HasVerb(clause, "dokunma|girme|erişme|okuma|değiştirme|taşıma|düzenleme|ayırma")) { denied.Add(scope); continue; }
                reads.Add(scope);
                if (HasVerb(clause, "düzenle|taşı|ayır|oluştur") && !clause.Contains("sadece incele")) writes.Add(scope);
            }
            if (new[] { "alarm", "hatırlat", "sayaç" }.Any(clause.Contains))
            {
                if (HasVerb(clause, "kur|hatırlat|başlat")) alarmActions.Add("create");
                if (HasVerb(clause, "tekrarla") || HasVerb(clause, @"ileri\s+al")) alarmActions.Add("update");
                if (HasVerb(clause, @"iptal\s+et")) alarmActions.Add("cancel");
                if (HasVerb(clause, "ertele")) alarmActions.Add("snooze");
            }
        }
        reads.ExceptWith(denied); writes.ExceptWith(denied);
        var alarmMention = new[] { "alarm", "hatırlat", "sayaç" }.Any(text.Contains);
        return new(reads, writes, alarmActions.Count > 0,
            new[] { "spotify", "medya", "müzik", "duraklat" }.Any(text.Contains) && Verb("durdur|duraklat"),
            text.Contains("ses") && Verb("indir|ayarla|yap|getir|düşür"), Verb(@"geri\s+al"), alarmMention, alarmActions);
    }
}

public enum Authorization { Allowed, MissingScope, NeedsApproval }
public static class PermissionPolicy
{
    public static Authorization Check(ToolDefinition tool, JsonElement args, PermissionGrant grant)
    {
        if (tool.Name == "files_undo_operation") return grant.UndoRequested ? Authorization.Allowed : Authorization.NeedsApproval;
        if (tool.Scope == "files")
        {
            if (!args.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.String) return Authorization.MissingScope;
            var name = scope.GetString()!;
            if (!grant.ReadScopes.Contains(name)) return Authorization.MissingScope;
            return tool.Risk is ToolRisk.Reversible && !grant.WriteScopes.Contains(name) ? Authorization.NeedsApproval : Authorization.Allowed;
        }
        if (tool.Scope == "alarms" && tool.Risk == ToolRisk.ReadOnly && !grant.AlarmRead) return Authorization.MissingScope;
        if (tool.Risk is ToolRisk.ReadOnly or ToolRisk.Prepare) return Authorization.Allowed;
        return tool.Scope switch
        {
            "alarms" when grant.Alarms && grant.AlarmActions?.Contains(tool.Name["alarms_".Length..]) == true => Authorization.Allowed,
            "media" when grant.Media => Authorization.Allowed,
            "volume" when grant.Volume => Authorization.Allowed,
            _ => Authorization.NeedsApproval
        };
    }
    public static string? ValidateArguments(JsonElement args, JsonElement schema)
    {
        if (args.ValueKind != JsonValueKind.Object) return "Parametreler JSON nesnesi olmalı.";
        if (schema.TryGetProperty("required", out var required))
            foreach (var name in required.EnumerateArray())
                if (!args.TryGetProperty(name.GetString()!, out _)) return $"Eksik parametre: {name.GetString()}";
        var properties = schema.GetProperty("properties");
        foreach (var property in args.EnumerateObject())
        {
            if (!properties.TryGetProperty(property.Name, out var rule)) return $"Bilinmeyen parametre: {property.Name}";
            var type = rule.GetProperty("type").GetString();
            var valid = type switch
            {
                "string" => property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length <= 256,
                "integer" => property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out _),
                "boolean" => property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => false
            };
            if (!valid) return $"Geçersiz parametre türü/boyutu: {property.Name}";
            if (rule.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(x => x.ToString() == property.Value.ToString()))
                return $"Desteklenmeyen parametre: {property.Name}";
        }
        return null;
    }
}
