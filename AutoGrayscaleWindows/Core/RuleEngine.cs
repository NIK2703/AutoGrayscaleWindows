using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AutoGrayscaleWindows.Models;
using Serilog;

namespace AutoGrayscaleWindows.Core;

/// <summary>
/// Результат оценки правил
/// </summary>
public class RuleEvaluationResult
{
    /// <summary>
    /// Найдено ли подходящее правило
    /// </summary>
    public bool RuleFound { get; set; }

    /// <summary>
    /// Действие, которое нужно выполнить
    /// </summary>
    public RuleAction Action { get; set; }

    /// <summary>
    /// Сработавшее правило
    /// </summary>
    public AppRule? MatchedRule { get; set; }

    /// <summary>
    /// Время выполнения оценки (мс)
    /// </summary>
    public long EvaluationTimeMs { get; set; }

    /// <summary>
    /// Использован ли кэш
    /// </summary>
    public bool FromCache { get; set; }
}

/// <summary>
/// Аргументы события совпадения правила
/// </summary>
public class RuleMatchedEventArgs : EventArgs
{
    /// <summary>
    /// Информация об окне
    /// </summary>
    public WindowInfo WindowInfo { get; set; } = WindowInfo.Empty;

    /// <summary>
    /// Сработавшее правило
    /// </summary>
    public AppRule MatchedRule { get; set; } = new();

    /// <summary>
    /// Действие
    /// </summary>
    public RuleAction Action { get; set; }
}

/// <summary>
/// Аргументы события отсутствия совпадения
/// </summary>
public class NoMatchEventArgs : EventArgs
{
    /// <summary>
    /// Информация об окне
    /// </summary>
    public WindowInfo WindowInfo { get; set; } = WindowInfo.Empty;

    /// <summary>
    /// Действие по умолчанию
    /// </summary>
    public RuleAction DefaultAction { get; set; }
}

/// <summary>
/// Класс для анализа и применения правил с кэшированием
/// </summary>
public class RuleEngine : IDisposable
{
    private List<AppRule> _rules = new();
    private bool _useWhitelist = false;
    private bool _disposed;

    // Кэш результатов оценки
    private readonly ConcurrentDictionary<string, RuleEvaluationResult> _evaluationCache = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(5); // Уменьшен для быстрой реакции на смену заголовков
    private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();

    // Максимальный размер кэша (увеличен для разных заголовков)
    private const int MaxCacheSize = 500;

    /// <summary>
    /// Событие при совпадении правила
    /// </summary>
    public event EventHandler<RuleMatchedEventArgs>? RuleMatched;

    /// <summary>
    /// Событие при отсутствии совпадения
    /// </summary>
    public event EventHandler<NoMatchEventArgs>? NoMatch;

    /// <summary>
    /// Количество активных правил
    /// </summary>
    public int ActiveRuleCount => _rules.Count;

    /// <summary>
    /// Устанавливает правила для анализа
    /// </summary>
    /// <param name="rules">Список правил</param>
    /// <param name="useWhitelist">Режим белого списка (true = whitelist, false = blacklist)</param>
    public void SetRules(List<AppRule> rules, bool useWhitelist)
    {
        _rules = rules ?? new List<AppRule>();
        _useWhitelist = useWhitelist;

        // Фильтруем активные правила
        _rules = _rules
            .Where(r => r.IsActive)
            .ToList();

        // Прекомпилируем regex для всех правил
        foreach (var rule in _rules.Where(r => r.MatchType == MatchType.Regex))
        {
            _ = rule.CompiledRegex;
        }

        // Очищаем кэш при изменении правил
        ClearCache();

        Log.Information("RuleEngine: загружено {Count} активных правил. Режим: {Mode}",
            _rules.Count, _useWhitelist ? "Whitelist" : "Blacklist");
    }

    /// <summary>
    /// Оценивает окно по правилам
    /// </summary>
    /// <param name="windowInfo">Информация об окне</param>
    /// <returns>Результат оценки</returns>
    public RuleEvaluationResult Evaluate(WindowInfo windowInfo)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (windowInfo == null || windowInfo == WindowInfo.Empty)
        {
            return new RuleEvaluationResult
            {
                RuleFound = false,
                Action = GetDefaultAction(),
                MatchedRule = null,
                EvaluationTimeMs = 0,
                FromCache = false
            };
        }

        // Проверяем кэш
        string cacheKey = GetCacheKey(windowInfo);
        if (TryGetFromCache(cacheKey, out var cachedResult))
        {
            cachedResult.FromCache = true;
            return cachedResult;
        }

        // Оцениваем правила
        bool ruleMatched = false;
        AppRule? matchedRule = null;

        foreach (var rule in _rules)
        {
            if (!rule.IsActive)
                continue;

            try
            {
                if (rule.Matches(windowInfo))
                {
                    ruleMatched = true;
                    matchedRule = rule;
                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при оценке правила {RuleName}", rule.DisplayName);
            }
        }

        stopwatch.Stop();

        // Определяем действие на основе режима и результата сопоставления
        // Blacklist mode (useWhitelist = false): правило найдено -> цветной, нет -> ч/б
        // Whitelist mode (useWhitelist = true): правило найдено -> ч/б, нет -> цветной
        RuleAction action;
        if (_useWhitelist)
        {
            // Whitelist: правило найдено -> включить grayscale, иначе выключить
            action = ruleMatched ? RuleAction.EnableGrayscale : RuleAction.DisableGrayscale;
        }
        else
        {
            // Blacklist: правило найдено -> выключить grayscale (цветной), иначе включить
            action = ruleMatched ? RuleAction.DisableGrayscale : RuleAction.EnableGrayscale;
        }

        var result = new RuleEvaluationResult
        {
            RuleFound = ruleMatched,
            Action = action,
            MatchedRule = matchedRule,
            EvaluationTimeMs = stopwatch.ElapsedMilliseconds,
            FromCache = false
        };

        if (ruleMatched && matchedRule != null)
        {
            Log.Debug("Найдено подходящее правило: {RuleName} -> {Action} ({Ms}мс)",
                matchedRule.DisplayName, action, stopwatch.ElapsedMilliseconds);
        }

        // Сохраняем в кэш
        SaveToCache(cacheKey, result);

        // Генерируем событие
        if (ruleMatched && matchedRule != null)
        {
            OnRuleMatched(windowInfo, matchedRule, action);
        }
        else
        {
            OnNoMatch(windowInfo, GetDefaultAction());
        }

        return result;
    }

    /// <summary>
    /// Получает действие по умолчанию на основе режима
    /// </summary>
    private RuleAction GetDefaultAction()
    {
        // Blacklist: по умолчанию grayscale включён
        // Whitelist: по умолчанию grayscale выключен (цветной)
        return _useWhitelist ? RuleAction.DisableGrayscale : RuleAction.EnableGrayscale;
    }

    /// <summary>
    /// Формирует ключ кэша для окна
    /// </summary>
    private string GetCacheKey(WindowInfo windowInfo)
    {
        // Используем AppId + WindowTitle + WindowClass как ключ, чтобы учитывать разные окна одного приложения
        var appId = windowInfo.AppId ?? windowInfo.ProcessName ?? windowInfo.Handle.ToString();
        var title = windowInfo.WindowTitle ?? "";
        var windowClass = windowInfo.WindowClass ?? "";
        return $"{appId}|{title}|{windowClass}";
    }

    /// <summary>
    /// Пытается получить результат из кэша
    /// </summary>
    private bool TryGetFromCache(string key, out RuleEvaluationResult result)
    {
        result = new RuleEvaluationResult();

        if (!_evaluationCache.TryGetValue(key, out var cachedResult))
            return false;

        if (!_cacheTimestamps.TryGetValue(key, out var timestamp))
            return false;

        // Проверяем TTL
        if (DateTime.Now - timestamp > _cacheTtl)
        {
            // Устаревшая запись
            _evaluationCache.TryRemove(key, out _);
            _cacheTimestamps.TryRemove(key, out _);
            return false;
        }

        result = cachedResult;
        return true;
    }

    /// <summary>
    /// Сохраняет результат в кэш
    /// </summary>
    private void SaveToCache(string key, RuleEvaluationResult result)
    {
        // Проверяем размер кэша
        if (_evaluationCache.Count >= MaxCacheSize)
        {
            // Удаляем старые записи (LRU)
            var oldestKey = _cacheTimestamps
                .OrderBy(kvp => kvp.Value)
                .FirstOrDefault()
                .Key;

            if (oldestKey != null)
            {
                _evaluationCache.TryRemove(oldestKey, out _);
                _cacheTimestamps.TryRemove(oldestKey, out _);
            }
        }

        _evaluationCache[key] = result;
        _cacheTimestamps[key] = DateTime.Now;
    }

    /// <summary>
    /// Очищает кэш
    /// </summary>
    public void ClearCache()
    {
        _evaluationCache.Clear();
        _cacheTimestamps.Clear();
        Log.Debug("Кэш RuleEngine очищен");
    }

    /// <summary>
    /// Генерирует событие RuleMatched
    /// </summary>
    private void OnRuleMatched(WindowInfo windowInfo, AppRule rule, RuleAction action)
    {
        RuleMatched?.Invoke(this, new RuleMatchedEventArgs
        {
            WindowInfo = windowInfo,
            MatchedRule = rule,
            Action = action
        });
    }

    /// <summary>
    /// Генерирует событие NoMatch
    /// </summary>
    private void OnNoMatch(WindowInfo windowInfo, RuleAction defaultAction)
    {
        NoMatch?.Invoke(this, new NoMatchEventArgs
        {
            WindowInfo = windowInfo,
            DefaultAction = defaultAction
        });
    }

    /// <summary>
    /// Проверяет валидность правила
    /// </summary>
    public static bool ValidateRule(AppRule rule, out string? errorMessage)
    {
        return rule.Validate(out errorMessage);
    }

    /// <summary>
    /// Получает все активные правила
    /// </summary>
    public IReadOnlyList<AppRule> GetActiveRules()
    {
        return _rules.AsReadOnly();
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        ClearCache();
        _rules.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~RuleEngine()
    {
        Dispose();
    }
}
